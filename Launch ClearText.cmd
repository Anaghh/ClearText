@echo off
cd /d "%~dp0"
if exist "dist\ClearText.exe" (
  start "" "dist\ClearText.exe"
) else (
  echo Build the native Windows app first by running build_windows.ps1 in PowerShell.
  pause
)
