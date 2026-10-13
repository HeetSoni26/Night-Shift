@echo off
rem NightShift double-click uninstaller.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
echo.
pause
