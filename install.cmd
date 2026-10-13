@echo off
rem NightShift double-click installer. Runs install.ps1 without touching the
rem PowerShell execution policy of the machine.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 (
  echo.
  echo Install failed - see the messages above.
  pause
) else (
  echo.
  echo Done. Look for the owl near the clock.
  pause
)
