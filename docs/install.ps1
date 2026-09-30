<#
    install.ps1 - copy NeuroMita.HideSlots.dll into a NeuroMita install.

    This plugin is a companion to NeuroMita.CustomModels, so it does not install BepInEx: by the
    time you want this plugin, BepInEx and the model installer are already in place. It only copies
    one DLL and then checks that what this plugin depends on is actually there.

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

Write-Host "NeuroMita.HideSlots - installer" -ForegroundColor Cyan
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

# ---------- copy ----------
$dll = Join-Path $here 'BepInEx\plugins\NeuroMita.HideSlots.dll'
if (-not (Test-Path $dll)) { $dll = Join-Path $here 'NeuroMita.HideSlots.dll' }
if (-not (Test-Path $dll)) { throw "NeuroMita.HideSlots.dll not found next to this script." }

Copy-Item $dll $plugins -Force
Write-Host "installed: $(Split-Path $dll -Leaf) -> $plugins" -ForegroundColor Green

# ---------- check what this plugin needs ----------
# Stated plainly because installing this alone produces a confusing result: the default Mita with
# her clothing slots switched off, and no replacement body.
$customModels = Join-Path $plugins 'NeuroMita.CustomModels.dll'
Write-Host ""
if (Test-Path $customModels) {
    Write-Host "OK  : NeuroMita.CustomModels found, so there is a replacement body to finish." -ForegroundColor Green
} else {
    Write-Warning ("NeuroMita.CustomModels.dll is NOT in BepInEx\plugins.")
    Write-Host "      This plugin installs nothing by itself. Without the model installer there is no" -ForegroundColor Yellow
    Write-Host "      replacement body, and all you will see is the default Mita with her clothing" -ForegroundColor Yellow
    Write-Host "      slots hidden. Install CustomModels first, then the nude mod pack." -ForegroundColor Yellow
    Write-Host "      https://github.com/younai666/neuromita-custom-models" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Launch the game once to generate BepInEx\config\neuromita.hideslots.cfg" -ForegroundColor Cyan
Read-Host "Press Enter to close"
