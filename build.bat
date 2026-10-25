@echo off
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /codepage:65001 /optimize+ ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:WeatherWidget.exe WeatherWidget.cs
if errorlevel 1 (pause & exit /b 1)
echo OK: WeatherWidget.exe
