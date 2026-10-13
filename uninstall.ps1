# NightShift for Windows - uninstaller.
# Ends the app, undoes any pending session changes, removes the recovery task and the
# startup shortcut, and deletes the program. With -Purge it also wipes settings/logs.
#
# Run from PowerShell:
#   powershell -ExecutionPolicy Bypass -File .\uninstall.ps1            # keep settings
#   powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 -Purge     # wipe everything
param([switch]$Purge)
$ErrorActionPreference = "SilentlyContinue"

$programDir = Join-Path $env:LOCALAPPDATA "Programs\NightShift"
$dataDir    = Join-Path $env:LOCALAPPDATA "NightShift"
$exe        = Join-Path $programDir "NightShift.exe"
$taskName   = "NightShiftRecovery"

Write-Host "==> Undoing any pending session changes"
if (Test-Path $exe) { Start-Process -FilePath $exe -ArgumentList "--recover" -Wait }

Write-Host "==> Stopping NightShift"
Stop-Process -Name NightShift -Force

Write-Host "==> Deleting recovery task"
schtasks /Delete /F /TN $taskName | Out-Null

Write-Host "==> Deleting startup shortcut"
Remove-Item (Join-Path ([Environment]::GetFolderPath("Startup")) "NightShift.lnk")

Write-Host "==> Deleting program"
Remove-Item $programDir -Recurse -Force

if ($Purge) {
  Write-Host "==> Purging data (config, logs, journal)"
  Remove-Item $dataDir -Recurse -Force
} else {
  Write-Host "==> Data kept at $dataDir (use -Purge to wipe it)"
}

Write-Host "Uninstalled."
