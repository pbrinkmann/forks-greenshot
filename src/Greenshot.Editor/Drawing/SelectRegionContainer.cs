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

using System;
using System.Drawing;
using System.Runtime.Serialization;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;

namespace Greenshot.Editor.Drawing
{
    /// <summary>
    /// The rectangle of the "select region" tool, it marks an area of the image which can be copied or cut.
    /// It is only a visual aid for the user: it is never rendered in an export, can't be undone and is not stored.
    /// The <see cref="FieldType.FILL_COLOR"/> is what a cut leaves behind in the image.
    /// </summary>
    [Serializable]
    public class SelectRegionContainer : DrawableContainer
    {
        public SelectRegionContainer(ISurface parent) : base(parent)
        {
            Init();
        }

        protected override void OnDeserialized(StreamingContext streamingContext)
        {
            base.OnDeserialized(streamingContext);
            Init();
        }

        private void Init()
        {
            CreateDefaultAdorners();
        }

        protected override void InitializeFields()
        {
            AddField(GetType(), FieldType.FILL_COLOR, Color.Transparent);
        }

        public override void Draw(Graphics graphics, RenderMode renderMode)
        {
            // The region only helps the user, it must never end up in the result
            if (renderMode == RenderMode.EXPORT)
            {
                return;
            }

            var rect = new NativeRect(Left, Top, Width, Height).Normalize();

            // A white line with a black dashed line on top, so the border is visible on every background
            using Pen backgroundPen = new Pen(Color.White);
            using Pen dashedPen = new Pen(Color.Black)
            {
                DashPattern = new float[]
                {
                    3, 3
                }
            };
            graphics.DrawRectangle(backgroundPen, rect);
            graphics.DrawRectangle(dashedPen, rect);
        }

        /// <summary>
        /// A click or a very small drag doesn't select a region, the surface will discard the container
        /// </summary>
        public override bool InitContent()
        {
            return Math.Abs(Width) >= 5 || Math.Abs(Height) >= 5;
        }

        /// <summary>
        /// No context menu for the region, the clipboard actions are in the edit menu and available via keyboard
        /// </summary>
        public override bool HasContextMenu => false;

        /// <summary>
        /// Selecting a region doesn't change the image, so there is nothing to undo
        /// </summary>
        public override bool IsUndoable => false;
    }
}
