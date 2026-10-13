# NightShift for Windows - installer.
# Installs NightShift, registers the 1-minute recovery task and the startup shortcut,
# then launches the tray app. Uses the NightShift.exe bundled next to this script
# (release zip); if there is none, builds it first with the .NET Framework 4 compiler
# that ships with Windows (no SDK needed).
#
# Double-click install.cmd, or run from PowerShell:
#   powershell -ExecutionPolicy Bypass -File .\install.ps1
param([switch]$SkipLaunch)
$ErrorActionPreference = "Stop"

$root       = Split-Path -Parent $MyInvocation.MyCommand.Path
$programDir = Join-Path $env:LOCALAPPDATA "Programs\NightShift"
$exe        = Join-Path $programDir "NightShift.exe"
$taskName   = "NightShiftRecovery"

if (Test-Path (Join-Path $root "NightShift.exe")) {
  Write-Host "==> Using the bundled NightShift.exe"
} else {
  Write-Host "==> Building NightShift.exe"
  cmd /c "`"$root\build.cmd`"" | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "build failed" }
  if (-not (Test-Path (Join-Path $root "NightShift.exe"))) { throw "NightShift.exe not found after build" }
}

Write-Host "==> Installing to $programDir"
New-Item -ItemType Directory -Force $programDir | Out-Null
Stop-Process -Name NightShift -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300
Copy-Item (Join-Path $root "NightShift.exe") $exe -Force

Write-Host "==> Registering recovery task '$taskName' (runs every minute)"
schtasks /Create /F /TN $taskName /SC MINUTE /MO 1 /TR "`"$exe`" --recover" | Out-Null

Write-Host "==> Creating startup shortcut"
$ws = New-Object -ComObject WScript.Shell
$startup = [Environment]::GetFolderPath("Startup")
$lnk = $ws.CreateShortcut((Join-Path $startup "NightShift.lnk"))
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $programDir
$lnk.IconLocation = $exe
$lnk.Save()

if (-not $SkipLaunch) {
  Write-Host "==> Starting NightShift"
  Start-Process $exe
}

Write-Host ""
Write-Host "Installed. Look for the owl in the system tray (near the clock)."
Write-Host "Data and logs:  $env:LOCALAPPDATA\NightShift"
Write-Host "Uninstall:      powershell -ExecutionPolicy Bypass -File `"$root\uninstall.ps1`""
