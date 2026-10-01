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
# Stated plainly, because without the pack the plugin has nothing to install and the game simply
# looks normal, which is a confusing way to find out.
#
# The check mirrors what the plugin does: any file whose header is UnityFS, under any name, either
# straight in BepInEx\plugins, in a NudeMita subfolder, or in the game folder. The plugin does the
# stronger check (the body mesh has to match), which needs to parse the container and cannot be done
# from a batch file.
function Test-UnityFs([string]$path) {
    try {
        $fs = [System.IO.File]::OpenRead($path)
        try {
            $buf = New-Object byte[] 7
            if ($fs.Read($buf, 0, 7) -lt 7) { return $false }
            return ($buf[0] -eq 0x55 -and $buf[1] -eq 0x6E -and $buf[2] -eq 0x69 -and
                    $buf[3] -eq 0x74 -and $buf[4] -eq 0x79 -and $buf[5] -eq 0x46 -and $buf[6] -eq 0x53)
        } finally { $fs.Dispose() }
    } catch { return $false }
}

$foundPack = $null
$searchDirs = @($plugins, (Join-Path $plugins 'NudeMita'), $game)
foreach ($dir in $searchDirs) {
    if ($foundPack) { break }
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in (Get-ChildItem $dir -File -ErrorAction SilentlyContinue)) {
        if (Test-UnityFs $f.FullName) { $foundPack = $f.FullName; break }
    }
}

Write-Host ""
if ($foundPack) {
    Write-Host "OK  : a UnityFS pack was found: $foundPack" -ForegroundColor Green
    Write-Host "      (the plugin confirms it is the right one when the game starts)" -ForegroundColor DarkGray
} else {
    Write-Warning "the nude mod pack was NOT found."
    Write-Host "      This plugin is the loader and the fix-ups; the model itself is the pack." -ForegroundColor Yellow
    Write-Host "      Download it from the mod page:" -ForegroundColor Yellow
    Write-Host "          https://www.nexusmods.com/miside/mods/58" -ForegroundColor Yellow
    Write-Host "      and put the file -- any name -- into:" -ForegroundColor Yellow
    Write-Host "          $plugins" -ForegroundColor Yellow
    Write-Host "      A folder also works, as long as one file inside it is a UnityFS container." -ForegroundColor Yellow
    Write-Host "      The plugin looks again every few seconds, so the game does not need restarting." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Launch the game once to generate BepInEx\config\neuromita.nudemita.cfg" -ForegroundColor Cyan
Read-Host "Press Enter to close"
