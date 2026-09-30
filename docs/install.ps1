<#
    install.ps1 - copy NeuroMita.NudeMita into a NeuroMita install.

    This plugin is self-contained: it reads the pack itself. It does not install BepInEx (install
    that first), and it does not need any other model plugin.

    Usage:
        powershell -File install.ps1
        powershell -File install.ps1 -GameDir "D:\Games\NeuroMita"
#>
[CmdletBinding()]
param(
    [string]$GameDir = ''
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

function Resolve-GameDir([string]$candidate) {
    if ([string]::IsNullOrWhiteSpace($candidate)) { return $null }
    $p = $candidate.Trim('"').TrimEnd('\')
    # Accept being handed NeuroMita.exe instead of the folder.
    if (Test-Path $p -PathType Leaf) { $p = Split-Path $p -Parent }
    if ((Test-Path (Join-Path $p 'NeuroMita.exe')) -and (Test-Path (Join-Path $p 'BepInEx'))) {
        return $p
    }
    return $null
}

Write-Host "NeuroMita.NudeMita - installer" -ForegroundColor Cyan
Write-Host ""

# ---------- locate the game ----------
$game = Resolve-GameDir $GameDir
if (-not $game) {
    foreach ($candidate in @((Split-Path $here -Parent), $here, (Get-Location).Path)) {
        $game = Resolve-GameDir $candidate
        if ($game) { break }
    }
}

if (-not $game) {
    Write-Host "Could not find the game automatically." -ForegroundColor Yellow
    Write-Host "Drag the game folder (or NeuroMita.exe) onto install.bat, or pass it explicitly:"
    Write-Host '    powershell -File install.ps1 -GameDir "D:\Games\NeuroMita"'
    Read-Host "`nPress Enter to close"
    exit 1
}
Write-Host "game     : $game"

$plugins = Join-Path $game 'BepInEx\plugins'
if (-not (Test-Path $plugins)) {
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
}

# ---------- copy the plugin ----------
$dlls = @(
    'NeuroMita.NudeMita.dll',
    'AssetsTools.NET.dll',
    'AssetsTools.NET.Texture.dll',
    'AssetRipper.TextureDecoder.dll'
)
$missing = @()
foreach ($name in $dlls) {
    $src = Join-Path $here "BepInEx\plugins\$name"
    if (-not (Test-Path $src)) { $src = Join-Path $here $name }
    if (-not (Test-Path $src)) { $missing += $name; continue }
    Copy-Item $src $plugins -Force
}
if ($missing.Count -gt 0) { throw "these file(s) are not next to this script: $($missing -join ', ')" }
Write-Host "installed: $($dlls.Count) file(s) -> $plugins" -ForegroundColor Green

# ---------- check the pack ----------
# Stated plainly, because the plugin has nothing to install without it and the game will simply look
# normal, which is a confusing way to find out.
$pack = Join-Path $plugins 'mita_nude'
Write-Host ""
if (Test-Path $pack) {
    Write-Host "OK  : the nude mod pack was found: $pack" -ForegroundColor Green
} else {
    Write-Warning "the nude mod pack is NOT in BepInEx\plugins."
    Write-Host "      This plugin is the loader and the fix-ups; the model itself is the pack." -ForegroundColor Yellow
    Write-Host "      Download the nude mod and put its bundle at:" -ForegroundColor Yellow
    Write-Host "          $pack" -ForegroundColor Yellow
    Write-Host "      (a folder also works, as long as one file inside it is a UnityFS container;" -ForegroundColor Yellow
    Write-Host "       point General.PackPath in the config elsewhere if you prefer another location.)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Launch the game once to generate BepInEx\config\neuromita.nudemita.cfg" -ForegroundColor Cyan
Read-Host "Press Enter to close"
