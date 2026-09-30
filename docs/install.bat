@echo off
rem ---------------------------------------------------------------------------
rem  NeuroMita.HideSlots - one-click installer
rem
rem  Double-click this file. It copies the plugin into BepInEx\plugins and then
rem  checks that NeuroMita.CustomModels is present, which this plugin needs in
rem  order to do anything visible.
rem
rem  You can also drag the game folder (or NeuroMita.exe) onto this file.
rem
rem  BepInEx is not installed by this script: this is a companion plugin, so it
rem  assumes the model installer is already set up.
rem ---------------------------------------------------------------------------

setlocal
if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -GameDir "%~1"
)
endlocal
