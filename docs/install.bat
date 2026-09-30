@echo off
rem ---------------------------------------------------------------------------
rem  NeuroMita.NudeMita - one-click installer
rem
rem  Double-click this file. It copies the plugin into BepInEx\plugins and then
rem  checks that the nude mod pack is in place, which is the model itself.
rem
rem  You can also drag the game folder (or NeuroMita.exe) onto this file.
rem
rem  BepInEx is not installed by this script: install that first.
rem ---------------------------------------------------------------------------

setlocal
if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -GameDir "%~1"
)
endlocal
