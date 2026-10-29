@echo off
rem Builds the widget: bin\WeatherWidget.exe
rem Requires only .NET Framework 4.x that ships with Windows (csc.exe).
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: csc.exe not found. .NET Framework 4.x is required.
    exit /b 1
)

if not exist bin mkdir bin
set "ICON="
if exist assets\app.ico set "ICON=/win32icon:assets\app.ico"

rem the manifest makes the widget DPI-aware: crisp at 125-150 % display scale
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /warn:4 %ICON% /win32manifest:assets\app.manifest ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll ^
  /resource:assets\cities.txt,WeatherWidget.cities.txt /resource:assets\app.ico,WeatherWidget.app.ico ^
  /out:bin\WeatherWidget.exe /recurse:src\*.cs
if errorlevel 1 exit /b 1

echo OK: bin\WeatherWidget.exe
rem pass the compiler path to test.bat and tools\make-assets.bat
endlocal & set "CSC=%CSC%"
