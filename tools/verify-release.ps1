<#
    verify-release.ps1 - offline checks on a built release.

    No Unity, no game. Everything here reads files that are already on disk.

    Usage:
        powershell -File tools\verify-release.ps1
        powershell -File tools\verify-release.ps1 -Zip dist\NeuroMita.NudeMita-0.1.0-pre1.zip

    Exits non-zero if any check fails.

    Deliberately ASCII-only: Windows PowerShell parses a .ps1 without a byte-order mark as ANSI, so
    a non-ASCII literal in here would be mangled into a syntax error. Script ranges are used
    instead of literal words wherever the content being checked is not ASCII.
#>
[CmdletBinding()]
param(
    [string]$Zip = '',
    [string]$Pack = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fail = 0
$pass = 0

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) {
        $script:pass++
        Write-Host ("  PASS  {0}" -f $name) -ForegroundColor Green
    } else {
        $script:fail++
        Write-Host ("  FAIL  {0}" -f $name) -ForegroundColor Red
    }
    if ($detail) { Write-Host ("        {0}" -f $detail) -ForegroundColor DarkGray }
}

$projDir = Join-Path $root 'src\NeuroMita.NudeMita'
$csprojPath = Join-Path $projDir 'NeuroMita.NudeMita.csproj'
$pluginPath = Join-Path $projDir 'Plugin.cs'
$csprojText = [System.IO.File]::ReadAllText($csprojPath)
$srcText = [System.IO.File]::ReadAllText($pluginPath)

$csprojVer = [regex]::Match($csprojText, '<Version>([^<]+)</Version>').Groups[1].Value
$srcVer = [regex]::Match($srcText, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value
$srcGuid = [regex]::Match($srcText, 'Guid\s*=\s*"([^"]+)"').Groups[1].Value
$attrLine = [regex]::Match($srcText, '\[BepInPlugin\(([^\)]*)\)\]').Groups[1].Value

$dll = Join-Path $projDir 'bin\Release\NeuroMita.NudeMita.dll'
$infoVer = ''
if (Test-Path $dll) {
    # FileVersionInfo reads the version resources without loading the assembly, which matters:
    # this is a net6.0 assembly and Windows PowerShell cannot resolve its dependencies.
    $infoVer = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
}

Write-Host "version agreement" -ForegroundColor Cyan
Check 'csproj <Version> equals the PluginVersion const' ($csprojVer -eq $srcVer) "csproj=$csprojVer  source=$srcVer"
Check 'the built assembly carries the same version' ($infoVer -eq $csprojVer) "assembly=$infoVer"
Check 'version is a valid semver pre-release tag' ([bool]([regex]::IsMatch($csprojVer, '^\d+\.\d+\.\d+(-[0-9A-Za-z\.\-]+)?$'))) $csprojVer

Write-Host "`nplugin identity" -ForegroundColor Cyan
Check 'declares a BepInPlugin attribute from the constants' ($attrLine -eq 'Guid, PluginName, PluginVersion') $attrLine
Check 'the GUID is the expected one' ($srcGuid -eq 'neuromita.nudemita') $srcGuid

if (Test-Path $dll) {
    # ------------------------------------------------------------ what it links against
    # GetReferencedAssemblies only reads metadata, so it works here even though the assembly cannot
    # be fully loaded.
    $asm = [System.Reflection.Assembly]::LoadFrom($dll)
    $refs = $asm.GetReferencedAssemblies() | ForEach-Object { $_.Name }
    Write-Host "`ndependencies" -ForegroundColor Cyan
    Check 'does NOT reference Assembly-CSharp (the game''s own types)' ($refs -notcontains 'Assembly-CSharp')
    Check 'does NOT reference any other NeuroMita plugin' (-not ($refs | Where-Object { $_ -like 'NeuroMita.*' }))
    foreach ($need in 'BepInEx.Core', 'BepInEx.Unity.IL2CPP', 'Il2CppInterop.Runtime', 'UnityEngine.CoreModule', 'AssetsTools.NET') {
        Check ("references {0}" -f $need) ($refs -contains $need)
    }
    Check 'drops the FBX native library (assimp is gone)' (-not ($refs -contains 'AssimpNetter'))
    Write-Host ("        all references: {0}" -f ($refs -join ', ')) -ForegroundColor DarkGray
}

Write-Host "`nsource" -ForegroundColor Cyan
$stale = @()
foreach ($f in (Get-ChildItem $projDir -File -Filter *.cs)) {
    $t = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($bad in 'TODO', 'FIXME', 'HACK', 'NeuroMita.HideSlots', 'CpuMorphRuntime', 'FbxPackage', 'AddonConfig', 'RendererSlotMatcher') {
        if ($t -match [regex]::Escape($bad)) { $stale += "$($f.Name):$bad" }
    }
}
Check 'no TODOs and no references to the left-behind halves' ($stale.Count -eq 0) ($stale -join ', ')
Check 'csproj has no Assembly-CSharp reference' (-not ($csprojText -match 'Assembly-CSharp'))
Check 'csproj does not pull in assimp' (-not ($csprojText -match 'Assimp'))

Write-Host "`nscripts parse" -ForegroundColor Cyan
foreach ($ps in 'package.ps1', 'tools\verify-bindpose-math.ps1', 'tools\verify-release.ps1', 'docs\install.ps1') {
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile((Join-Path $root $ps), [ref]$null, [ref]$errors)
    Check ("{0}" -f $ps) ($errors.Count -eq 0) (($errors | ForEach-Object { $_.Message }) -join '; ')
}

Write-Host "`npack profile" -ForegroundColor Cyan
$bakedTris = ([regex]::Match($srcText, 'SplitTris\s*=\s*\{([^}]*)\}').Groups[1].Value -split ',') |
             ForEach-Object { [int]$_.Trim() }
$bakedTex = ([regex]::Match($srcText, 'SplitTextures\s*=\s*\{([^}]*)\}').Groups[1].Value -split ',') |
            ForEach-Object { $_.Trim().Trim('"') }

Check 'SplitTris and SplitTextures describe the same number of parts' ($bakedTris.Count -eq $bakedTex.Count) "tris=$($bakedTris -join ',')  textures=$($bakedTex -join ',')"

if ($Pack -and (Test-Path $Pack)) {
    # The baked profile is only correct for one pack, so it is checked against the real container
    # with tools\bundledump -- which needs no game and no Unity.
    Write-Host "        checking against $([System.IO.Path]::GetFileName($Pack))" -ForegroundColor DarkGray
    $dumpProj = Join-Path $root 'tools\bundledump'
    $out = (& dotnet run --project $dumpProj -c Release -- $Pack 2>&1) -join "`n"

    $packTris = @()
    $inBody = $false
    foreach ($line in ($out -split "`n")) {
        if ($line -match '^\s+Body\s+subMeshes=') { $inBody = $true; continue }
        if (-not $inBody) { continue }
        if ($line -match 'sub\d+:.*tris=(\d+)') { $packTris += [int]$Matches[1]; continue }
        if ($line.Trim().Length -gt 0) { break }
    }

    Check 'the pack has a Body mesh with submeshes' ($packTris.Count -gt 0)
    Check 'the baked SplitTris matches the pack, part for part' (($packTris -join ',') -eq ($bakedTris -join ',')) "pack=$($packTris -join ',')  baked=$($bakedTris -join ',')"

    foreach ($t in $bakedTex) {
        if ($t -eq '' -or $t -eq 'null') { continue }   # a part that keeps the installer's material
        Check ("the pack carries the texture '{0}'" -f $t) ([bool]([regex]::IsMatch($out, "(?m)^\s+$([regex]::Escape($t))\s+\d+x\d+\s+fmt=")))
    }
} else {
    Write-Host "        no -Pack given, so the baked profile was not checked against the container" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- the archive
if (-not $Zip) {
    $zips = Get-ChildItem (Join-Path $root 'dist') -Filter 'NeuroMita.NudeMita-*.zip' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending
    if ($zips) { $Zip = $zips[0].FullName }
}

if ($Zip -and (Test-Path $Zip)) {
    Write-Host "`narchive: $([System.IO.Path]::GetFileName($Zip))" -ForegroundColor Cyan
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $a = [System.IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        # Entry names are normalised to forward slashes: that is what the zip specification asks
        # for, and any backslash in here is the packaging bug this check exists to catch.
        $names = $a.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }

        foreach ($need in 'READ-FIRST.txt', 'install.bat', 'install.ps1', 'LICENSE',
                          'BepInEx/plugins/NeuroMita.NudeMita.dll',
                          'BepInEx/plugins/AssetsTools.NET.dll',
                          'BepInEx/plugins/AssetsTools.NET.Texture.dll',
                          'BepInEx/plugins/AssetRipper.TextureDecoder.dll',
                          'docs/README.md', 'docs/INSTALL.md', 'docs/FAQ.md', 'docs/CHANGELOG.md',
                          'docs/RELEASE-NOTES.md') {
            Check ("contains {0}" -f $need) ($names -contains $need)
        }

        $strays = $names | Where-Object { $_ -match '\.pdb$' -or $_ -match '^src/' -or $_ -match '\.cs$' -or $_ -match 'assimp' }
        Check 'carries no build leftovers (pdb, sources, assimp)' (-not $strays) ($strays -join ', ')

        # The plugin's own scripts must stay ASCII, even though the docs must not: a .ps1 or .bat
        # read as ANSI would have its non-ASCII bytes mangled.
        $nonAsciiScripts = @()
        foreach ($e in $a.Entries) {
            if ($e.FullName -notmatch '\.(ps1|bat)$') { continue }
            $sr = New-Object System.IO.StreamReader($e.Open())
            $tx = $sr.ReadToEnd(); $sr.Dispose()
            if ($tx -match '[^\x00-\x7F]') { $nonAsciiScripts += $e.FullName }
        }
        Check 'shipped scripts are ASCII-only' ($nonAsciiScripts.Count -eq 0) ($nonAsciiScripts -join ', ')

        # Every text entry must decode cleanly -- a mis-encoded Russian or Chinese doc shows up here.
        $broken = @()
        foreach ($e in $a.Entries) {
            if ($e.FullName -notmatch '\.(md|txt|ps1|bat)$') { continue }
            $sr = New-Object System.IO.StreamReader($e.Open(), (New-Object System.Text.UTF8Encoding($false, $false)))
            $text = $sr.ReadToEnd(); $sr.Dispose()
            if ($text -match [char]0xFFFD) { $broken += $e.FullName }
        }
        Check 'all text decodes as UTF-8 with no replacement characters' ($broken.Count -eq 0) ($broken -join ', ')

        $bat = $a.Entries | Where-Object { $_.FullName -eq 'install.bat' }
        $sr = New-Object System.IO.StreamReader($bat.Open())
        $batText = $sr.ReadToEnd(); $sr.Dispose()
        Check 'install.bat uses CRLF line endings' ($batText -match "`r`n")

        $backslashed = $a.Entries | Where-Object { $_.FullName -match '\\' } | ForEach-Object { $_.FullName }
        Check 'entry names use forward slashes' ($backslashed.Count -eq 0) ($backslashed -join ', ')

        function Read-Entry([string]$name) {
            $e = $a.Entries | Where-Object { ($_.FullName -replace '\\', '/') -eq $name }
            if (-not $e) { return '' }
            $r = New-Object System.IO.StreamReader($e.Open())
            $t = $r.ReadToEnd(); $r.Dispose()
            return $t
        }

        $rm = Read-Entry 'docs/README.md'
        $im = Read-Entry 'docs/INSTALL.md'
        $fa = Read-Entry 'docs/FAQ.md'
        $ff = Read-Entry 'READ-FIRST.txt'
        $rn = Read-Entry 'docs/RELEASE-NOTES.md'

        # Language sections are located by their markers, not by "the first Cyrillic character" or
        # "the first CJK character": the document titles are bilingual by design ("Установка / 安装"),
        # so a character test would land on a title instead of on a section. All three come back as
        # LINE numbers -- mixing a character offset with a line number would compare two different
        # units and pass or fail by luck.
        function Get-LanguageLayout([string]$text) {
            $lines = [regex]::Split($text, "\r?\n")
            $ru = -1; $en = -1; $zh = -1
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($ru -lt 0 -and $lines[$i] -match '[\u0400-\u04FF]') { $ru = $i }
                if ($en -lt 0 -and $lines[$i] -match 'ENGLISH|English') { $en = $i }
                if ($zh -lt 0 -and (($lines[$i] -match '^<!--.*[\u4e00-\u9fff].*-->') -or
                                    ($lines[$i] -match '^\s*[\u4e00-\u9fff]+\s*$'))) { $zh = $i }
            }
            return [pscustomobject]@{ Ru = $ru; En = $en; Zh = $zh }
        }

        foreach ($pair in @(@('README.md', $rm), @('INSTALL.md', $im), @('FAQ.md', $fa),
                            @('READ-FIRST.txt', $ff), @('RELEASE-NOTES.md', $rn))) {
            $lay = Get-LanguageLayout $pair[1]
            Check ("{0}: Russian, then English, then Chinese" -f $pair[0]) `
                  (($lay.En -ge 0) -and ($lay.Ru -ge 0) -and ($lay.En -gt $lay.Ru) -and ($lay.Zh -gt $lay.En)) `
                  "ru@$($lay.Ru) en@$($lay.En) zh@$($lay.Zh)"
        }

        $allDocs = $rm + $im + $fa + $ff + $rn
        Check 'README carries the mod page URL' ($rm -match 'nexusmods\.com/miside/mods/58')
        Check 'INSTALL.md carries the mod page URL' ($im -match 'nexusmods\.com/miside/mods/58')
        Check 'READ-FIRST carries the mod page URL' ($ff -match 'nexusmods\.com/miside/mods/58')
        Check 'RELEASE-NOTES carries the mod page URL' ($rn -match 'nexusmods\.com/miside/mods/58')
        Check 'every doc decodes without replacement characters' (-not ($allDocs -match [char]0xFFFD))
        Check 'no doc still points at the old plugin name' (-not ($allDocs -match 'HideSlots'))

        Write-Host ("        {0} entries, {1:N1} KB" -f $a.Entries.Count, ((Get-Item $Zip).Length / 1KB)) -ForegroundColor DarkGray
    } finally { $a.Dispose() }
} else {
    Write-Host "`narchive" -ForegroundColor Cyan
    Check 'a release zip exists' $false "nothing found in dist"
}

Write-Host ""
if ($fail -eq 0) {
    Write-Host ("ALL {0} CHECKS PASSED" -f $pass) -ForegroundColor Green
} else {
    Write-Host ("{0} passed, {1} FAILED" -f $pass, $fail) -ForegroundColor Red
    exit 1
}
