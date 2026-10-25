@echo off
rem Regenerates assets\app.ico and docs\*.png with the widget's own rendering code (tools\Tools.cs).
setlocal
cd /d "%~dp0.."

call "%~dp0..\build.bat"
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:exe /codepage:65001 /warn:4 ^
  /r:bin\WeatherWidget.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:bin\WeatherWidget.Tools.exe tools\Tools.cs
if errorlevel 1 exit /b 1

"%~dp0..\bin\WeatherWidget.Tools.exe" icon assets\app.ico || exit /b 1
"%~dp0..\bin\WeatherWidget.Tools.exe" screenshots docs || exit /b 1

rem rebuild the widget with the new icon
call "%~dp0..\build.bat"
