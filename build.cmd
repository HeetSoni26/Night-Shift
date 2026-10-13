@echo off
rem Builds NightShift.exe with the .NET Framework 4 compiler that ships with Windows (no SDK needed).
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo ERROR: .NET Framework 4 csc.exe not found on this machine.
  exit /b 1
)

if not exist icon\owl-source.png (
  echo ERROR: icon\owl-source.png is missing - it is the logo the icons are built from.
  exit /b 1
)

if not exist icon\NightShiftIdle.ico (
  if not exist tools\genicon.exe "%CSC%" /nologo /out:tools\genicon.exe tools\GenIcon.cs
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftIdle.ico 26,30,42
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftActive.ico 255,173,39 icon\NightShift-256.png
  if errorlevel 1 exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:NightShift.exe /win32icon:icon\NightShiftIdle.ico /res:icon\NightShiftIdle.ico,NightShiftIdle.ico /res:icon\NightShiftActive.ico,NightShiftActive.ico /r:System.Web.Extensions.dll /r:System.Management.dll src\*.cs
if errorlevel 1 exit /b 1
echo Build OK: NightShift.exe
endlocal
