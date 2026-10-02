<#
.SYNOPSIS
    Build Greenshot from the checked out branch (normally "custom"), the same way the release workflow does.

.DESCRIPTION
    The solution contains C++ projects, so it needs the MSBuild of Visual Studio, the dotnet CLI can't build it.
    MSBuild is located with vswhere, so this works outside of a Developer PowerShell.
    The result is in src/Greenshot/bin/<Configuration>/net480, close Greenshot first when it runs from there.

.PARAMETER Configuration
    Release (default) or Debug

.PARAMETER AllowOtherBranch
    Build even when "custom" isn't checked out
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$AllowOtherBranch
)

$ErrorActionPreference = 'Stop'

Set-Location (git rev-parse --show-toplevel)

$branch = git branch --show-current
if ($branch -ne 'custom' -and -not $AllowOtherBranch) {
    Write-Host "'$branch' is checked out, which doesn't have all the features. Run 'git switch custom' first, or use -AllowOtherBranch." -ForegroundColor Yellow
    exit 1
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found, is Visual Studio installed?"
}
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild.exe not found with vswhere"
}

$solution = 'src/Greenshot.sln'
$outputDirectory = "src/Greenshot/bin/$Configuration/net480"

$running = Get-Process Greenshot -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*$($outputDirectory.Replace('/', '\'))*" }
if ($running) {
    Write-Host "Greenshot is running from $outputDirectory, close it first." -ForegroundColor Yellow
    exit 1
}

# Some build steps run scripts with Windows PowerShell, which can't load its own modules (e.g. Get-FileHash) with the PSModulePath of PowerShell 7
$env:PSModulePath = [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine')

# Like the release workflow: prepare first, this builds the build tasks before a project uses them
Write-Host "Preparing ($Configuration) with $msbuild" -ForegroundColor Cyan
& $msbuild $solution "/p:Configuration=$Configuration" /restore /t:PrepareForBuild /nodeReuse:false /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Preparing the build failed"
}

Write-Host "Building ($Configuration)" -ForegroundColor Cyan
& $msbuild $solution "/p:Configuration=$Configuration" /t:Build /nodeReuse:false /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "The build failed"
}

Write-Host ''
Write-Host "Done: $outputDirectory/Greenshot.exe ($(git rev-parse --short HEAD) on $branch)" -ForegroundColor Green
