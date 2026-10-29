@echo off
rem Builds and runs the unit tests: bin\WeatherWidget.Tests.exe
setlocal
cd /d "%~dp0"

call "%~dp0build.bat"
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:exe /codepage:65001 /warn:4 ^
  /r:bin\WeatherWidget.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll ^
  /out:bin\WeatherWidget.Tests.exe /recurse:tests\*.cs
if errorlevel 1 exit /b 1

"%~dp0bin\WeatherWidget.Tests.exe"
