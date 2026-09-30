<#
    package.ps1 - build and produce a release zip.

    Usage:
        powershell -File package.ps1
        powershell -File package.ps1 -GameDir "D:\Games\NeuroMita"    # also deploy to a game

    Output:
        dist\NeuroMita.HideSlots-<version>.zip
        dist\NeuroMita.HideSlots-<version>\            (unpacked staging folder)

    Single managed DLL, no third-party dependencies, so the archive is small and there is no native
    library to locate.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$GameDir = ''
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\NeuroMita.HideSlots'

# ---------- version ----------
$csprojPath = Join-Path $proj 'NeuroMita.HideSlots.csproj'
$csproj = Get-Content $csprojPath -Raw
$m = [regex]::Match($csproj, '<Version>([^<]+)</Version>')
if (-not $m.Success) { throw "could not read <Version> from $csprojPath" }
$version = $m.Groups[1].Value.Trim()
Write-Host "packaging NeuroMita.HideSlots $version" -ForegroundColor Cyan

# ---------- build ----------
$buildArgs = @($proj, '-c', $Configuration, '-v', 'm')
if ($GameDir) { $buildArgs += "-p:GameDir=$GameDir" }

Write-Host "`n[1/3] building..." -ForegroundColor Cyan
& dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

$bin = Join-Path $proj "bin\$Configuration"
$pluginDll = Join-Path $bin 'NeuroMita.HideSlots.dll'
if (-not (Test-Path $pluginDll)) { throw "build output not found: $pluginDll" }

# ---------- stage ----------
Write-Host "`n[2/3] staging..." -ForegroundColor Cyan
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist "NeuroMita.HideSlots-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

$pluginDir = Join-Path $stage 'BepInEx\plugins'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item $pluginDll $pluginDir -Force

Copy-Item (Join-Path $root 'LICENSE')           $stage -Force
Copy-Item (Join-Path $root 'docs\install.bat')  $stage -Force
Copy-Item (Join-Path $root 'docs\install.ps1')  $stage -Force

# Docs go into their own folder so the root of the archive stays obvious: the installer, the
# licence, and BepInEx\plugins. Nothing else.
$docDir = Join-Path $stage 'docs'
New-Item -ItemType Directory -Path $docDir -Force | Out-Null
Copy-Item (Join-Path $root 'README.md')        $docDir -Force
Copy-Item (Join-Path $root 'CHANGELOG.md')     $docDir -Force
Copy-Item (Join-Path $root 'docs\INSTALL.md')  $docDir -Force
Copy-Item (Join-Path $root 'docs\FAQ.md')      $docDir -Force

Get-ChildItem $pluginDir -File | ForEach-Object {
    Write-Host ("    {0,-34} {1,8:N0} KB" -f $_.Name, ($_.Length / 1KB))
}

# Normalise line endings for everything textual that goes into the archive. The zip is extracted on
# Windows, and a .bat with LF-only endings can misbehave there. Doing it here means the release is
# correct regardless of how the files happened to be written.
$textPatterns = @('*.bat', '*.ps1', '*.txt', '*.md')
$normalised = 0
foreach ($pattern in $textPatterns) {
    Get-ChildItem $stage -Recurse -File -Filter $pattern | ForEach-Object {
        $t = [System.IO.File]::ReadAllText($_.FullName)
        $fixed = $t.Replace("`r`n", "`n").Replace("`n", "`r`n")
        if ($fixed -ne $t) {
            [System.IO.File]::WriteAllText($_.FullName, $fixed, (New-Object System.Text.UTF8Encoding $false))
        }
        $normalised++
    }
}
Write-Host "    (normalised line endings in $normalised text file(s))"

# ---------- zip ----------
Write-Host "`n[3/3] zipping..." -ForegroundColor Cyan
$zip = Join-Path $dist "NeuroMita.HideSlots-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("    {0}  ({1:N1} KB)" -f $zip, ((Get-Item $zip).Length / 1KB))

# ---------- optional deploy ----------
if ($GameDir) {
    $target = Join-Path $GameDir 'BepInEx\plugins'
    if (Test-Path $target) {
        Write-Host "`n[deploy] copying plugin into $target" -ForegroundColor Cyan
        Get-ChildItem $pluginDir -File | ForEach-Object {
            Copy-Item $_.FullName $target -Force
            Write-Host "    -> $($_.Name)"
        }
    } else {
        Write-Warning "not a BepInEx game folder, skipped deploy: $GameDir"
    }
}

Write-Host "`ndone." -ForegroundColor Green
Write-Host "  zip     : $zip"
Write-Host "  staging : $stage"
