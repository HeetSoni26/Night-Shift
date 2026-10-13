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

if not exist icon\NightShiftActiveDarkBlink.ico (
  if not exist tools\genicon.exe "%CSC%" /nologo /out:tools\genicon.exe tools\GenIcon.cs
  if errorlevel 1 exit /b 1
  rem Adaptive owl: black on a light taskbar, white on a dark one; inverted (with a
  rem thin contrast ring) while a session runs, plus blink frames for the animation.
  tools\genicon.exe icon\owl-source.png icon\NightShiftIdleLight.ico 13,15,19 244,245,248 none icon\owl-black-256.png
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftIdleDark.ico 244,245,248 13,15,19 none
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftActiveLight.ico 244,245,248 13,15,19 13,15,19
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftActiveDark.ico 13,15,19 244,245,248 244,245,248 icon\owl-white-256.png
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftActiveLightBlink.ico 244,245,248 13,15,19 13,15,19 --blink
  if errorlevel 1 exit /b 1
  tools\genicon.exe icon\owl-source.png icon\NightShiftActiveDarkBlink.ico 13,15,19 244,245,248 244,245,248 --blink
  if errorlevel 1 exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:NightShift.exe /win32icon:icon\NightShiftIdleLight.ico /res:icon\NightShiftIdleLight.ico,NightShiftIdleLight.ico /res:icon\NightShiftIdleDark.ico,NightShiftIdleDark.ico /res:icon\NightShiftActiveLight.ico,NightShiftActiveLight.ico /res:icon\NightShiftActiveDark.ico,NightShiftActiveDark.ico /res:icon\NightShiftActiveLightBlink.ico,NightShiftActiveLightBlink.ico /res:icon\NightShiftActiveDarkBlink.ico,NightShiftActiveDarkBlink.ico /r:System.Web.Extensions.dll /r:System.Management.dll src\*.cs
if errorlevel 1 exit /b 1
echo Build OK: NightShift.exe
endlocal
