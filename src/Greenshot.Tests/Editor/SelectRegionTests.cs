/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Xunit;

namespace Greenshot.Tests.Editor
{
    /// <summary>
    /// Tests for the select region tool: the region itself, copying/cutting it and the mouse handling.
    /// The clipboard is not used here, so the tests don't overwrite what the person running them has copied.
    /// </summary>
    public class SelectRegionTests
    {
        private const int ImageWidth = 100;
        private const int ImageHeight = 80;

        public SelectRegionTests()
        {
            TestEnvironment.EnsureInitialized();
        }

        private static Surface CreateSurface(PixelFormat pixelFormat = PixelFormat.Format24bppRgb)
        {
            var bitmap = new Bitmap(ImageWidth, ImageHeight, pixelFormat);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
            }

            return new Surface(bitmap);
        }

        private static SelectRegionContainer AddRegion(Surface surface, int x, int y, int width, int height)
        {
            var region = new SelectRegionContainer(surface)
            {
                Left = x,
                Top = y,
                Width = width,
                Height = height
            };
            surface.AddElement(region);
            return region;
        }

        private static void RaiseMouseEvent(Surface surface, string eventMethod, int x, int y)
        {
            // The events are raised via the protected methods, so the real handlers of the surface are used
            var method = typeof(Control).GetMethod(eventMethod, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(surface, new object[]
            {
                new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)
            });
        }

        private static void Drag(Surface surface, int fromX, int fromY, int toX, int toY)
        {
            RaiseMouseEvent(surface, "OnMouseDown", fromX, fromY);
            RaiseMouseEvent(surface, "OnMouseMove", toX, toY);
            RaiseMouseEvent(surface, "OnMouseUp", toX, toY);
        }

        private static Color GetPixel(Image image, int x, int y) => ((Bitmap) image).GetPixel(x, y);

        [Fact]
        public void SelectedRegion_WithoutRegion_IsNull()
        {
            using var surface = CreateSurface();

            Assert.Null(surface.SelectedRegion);
            Assert.False(surface.CopySelectedRegion());
            Assert.False(surface.CutSelectedRegion());
        }

        [Fact]
        public void SelectedRegion_IsClippedToTheImage()
        {
            using var surface = CreateSurface();
            AddRegion(surface, -10, -10, 30, 30);

            Assert.Equal((NativeRect?) new NativeRect(0, 0, 20, 20), surface.SelectedRegion);
        }

        [Fact]
        public void SelectedRegion_OutsideTheImage_IsNull()
        {
            using var surface = CreateSurface();
            AddRegion(surface, ImageWidth + 10, 10, 30, 30);

            Assert.Null(surface.SelectedRegion);
        }

        [Fact]
        public void Drag_WithSelectTool_CreatesRegion()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;

            Drag(surface, 10, 10, 60, 50);

            Assert.Equal((NativeRect?) new NativeRect(10, 10, 50, 40), surface.SelectedRegion);
            Assert.Single(surface.Elements);
            Assert.True(surface.HasSelectedElements);
        }

        [Fact]
        public void Click_WithSelectTool_DoesNotCreateRegion()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;

            Drag(surface, 20, 20, 20, 20);

            Assert.Null(surface.SelectedRegion);
            Assert.Empty(surface.Elements);
        }

        [Fact]
        public void Click_OutsideTheRegion_RemovesIt()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;
            Drag(surface, 10, 10, 60, 50);

            Drag(surface, 80, 70, 80, 70);

            Assert.Null(surface.SelectedRegion);
            Assert.Empty(surface.Elements);
        }

        [Fact]
        public void Drag_OutsideTheRegion_ReplacesIt()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;
            Drag(surface, 10, 10, 60, 50);

            Drag(surface, 70, 60, 90, 75);

            Assert.Equal((NativeRect?) new NativeRect(70, 60, 20, 15), surface.SelectedRegion);
            Assert.Single(surface.Elements);
        }

        [Fact]
        public void Drag_InsideTheRegion_MovesIt()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;
            Drag(surface, 10, 10, 60, 50);

            Drag(surface, 35, 30, 45, 40);

            Assert.Equal((NativeRect?) new NativeRect(20, 20, 50, 40), surface.SelectedRegion);
            Assert.Single(surface.Elements);
        }

        [Fact]
        public void LeavingTheSelectTool_RemovesTheRegion()
        {
            using var surface = CreateSurface();
            surface.DrawingMode = DrawingModes.Select;
            Drag(surface, 10, 10, 60, 50);

            surface.DrawingMode = DrawingModes.None;

            Assert.Null(surface.SelectedRegion);
            Assert.Empty(surface.Elements);
        }

        [Fact]
        public void UsingTheRegion_DoesNotMarkTheSurfaceAsModified()
        {
            using var surface = CreateSurface();
            // E.g. the image was just saved
            surface.Modified = false;
            surface.DrawingMode = DrawingModes.Select;

            // Create, move with the mouse, move with the keyboard, and replace
            Drag(surface, 10, 10, 60, 50);
            Drag(surface, 35, 30, 45, 40);
            Assert.True(surface.ProcessCmdKey(Keys.Right));
            Assert.Equal((NativeRect?) new NativeRect(21, 20, 50, 40), surface.SelectedRegion);
            Drag(surface, 5, 5, 8, 8);
            // Click somewhere else to remove it, and leave the tool
            Drag(surface, 80, 70, 80, 70);
            surface.DrawingMode = DrawingModes.None;

            Assert.False(surface.Modified);
        }

        [Fact]
        public void FillRegion_MarksTheSurfaceAsModified()
        {
            using var surface = CreateSurface();
            surface.Modified = false;

            surface.FillRegion(new NativeRect(10, 10, 20, 20), Color.Red);

            Assert.True(surface.Modified);
        }

        [Fact]
        public void Region_IsNotPartOfTheExport()
        {
            using var surface = CreateSurface();
            AddRegion(surface, 10, 10, 50, 40);

            using var exported = surface.GetImageForExport();

            // The image was completely white, so anything drawn for the region would show up.
            // Every pixel is checked: the border is dashed, so a single position could be in a gap.
            for (int y = 0; y < ImageHeight; y++)
            {
                for (int x = 0; x < ImageWidth; x++)
                {
                    Assert.True(Color.White.ToArgb() == GetPixel(exported, x, y).ToArgb(), $"The pixel at {x},{y} was changed by the region");
                }
            }
        }

        [Fact]
        public void Region_IsNotCloned()
        {
            using var surface = CreateSurface();
            var rectangle = new RectangleContainer(surface)
            {
                Left = 5,
                Top = 5,
                Width = 20,
                Height = 20
            };
            surface.AddElement(rectangle);
            AddRegion(surface, 10, 10, 50, 40);

            using var clone = (Surface) surface.Clone();

            Assert.IsType<RectangleContainer>(Assert.Single(clone.Elements));
            // The original still has the region
            Assert.Equal(2, surface.Elements.Count);
        }

        [Fact]
        public void DuplicateSelectedElements_OnlyRegionSelected_DoesNothing()
        {
            using var surface = CreateSurface();
            var region = AddRegion(surface, 10, 10, 50, 40);
            surface.SelectElement(region);

            surface.DuplicateSelectedElements();

            Assert.Single(surface.Elements);
            // The region must still be selected, so a copy or cut still works on it
            Assert.Same(region, Assert.Single(surface.SelectedElements));
        }

        [Fact]
        public void GetRegionForExport_ContainsTheElementsOnTop()
        {
            using var surface = CreateSurface();
            var rectangle = new RectangleContainer(surface)
            {
                Left = 10,
                Top = 10,
                Width = 50,
                Height = 40
            };
            rectangle.SetFieldValue(FieldType.FILL_COLOR, Color.Blue);
            rectangle.SetFieldValue(FieldType.SHADOW, false);
            surface.AddElement(rectangle);

            using var regionImage = surface.GetRegionForExport(new NativeRect(0, 0, 40, 30));

            Assert.Equal(new Size(40, 30), regionImage.Size);
            // Outside of the rectangle there is the image, inside is the blue fill
            Assert.Equal(Color.White.ToArgb(), GetPixel(regionImage, 5, 5).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), GetPixel(regionImage, 30, 20).ToArgb());
        }

        [Fact]
        public void FillRegion_WithTransparentColor_ReplacesPixelsWithTransparency()
        {
            // The default image of a capture has no alpha channel
            using var surface = CreateSurface(PixelFormat.Format24bppRgb);

            Assert.True(surface.FillRegion(new NativeRect(10, 10, 20, 20), Color.Transparent));

            Assert.True(Image.IsAlphaPixelFormat(surface.Image.PixelFormat));
            Assert.Equal(new Size(ImageWidth, ImageHeight), surface.Image.Size);
            Assert.Equal(0, GetPixel(surface.Image, 15, 15).A);
            // Outside of the region nothing changed
            Assert.Equal(Color.White.ToArgb(), GetPixel(surface.Image, 5, 5).ToArgb());
            Assert.Equal(Color.White.ToArgb(), GetPixel(surface.Image, 30, 30).ToArgb());
        }

        [Fact]
        public void FillRegion_WithOpaqueColor_KeepsThePixelFormat()
        {
            using var surface = CreateSurface(PixelFormat.Format24bppRgb);

            Assert.True(surface.FillRegion(new NativeRect(10, 10, 20, 20), Color.Red));

            Assert.Equal(PixelFormat.Format24bppRgb, surface.Image.PixelFormat);
            Assert.Equal(Color.Red.ToArgb(), GetPixel(surface.Image, 15, 15).ToArgb());
        }

        [Fact]
        public void FillRegion_IsUndoable()
        {
            using var surface = CreateSurface();
            Assert.False(surface.CanUndo);

            surface.FillRegion(new NativeRect(10, 10, 20, 20), Color.Red);
            Assert.True(surface.CanUndo);
            surface.Undo();

            Assert.Equal(Color.White.ToArgb(), GetPixel(surface.Image, 15, 15).ToArgb());
            Assert.False(surface.CanUndo);
        }

        [Fact]
        public void FillRegion_OutsideTheImage_DoesNothing()
        {
            using var surface = CreateSurface();

            Assert.False(surface.FillRegion(new NativeRect(ImageWidth + 10, ImageHeight + 10, 20, 20), Color.Red));

            Assert.False(surface.CanUndo);
        }

        [Fact]
        public void Region_HasFillColorField_ForTheCut()
        {
            using var surface = CreateSurface();
            var region = new SelectRegionContainer(surface);

            Assert.True(region.HasField(FieldType.FILL_COLOR));
            Assert.False(region.IsUndoable);
            Assert.False(region.HasContextMenu);
        }
    }
}
