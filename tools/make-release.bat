@echo off
rem Builds, tests and packs a release: dist\WeatherWidget-<version>.zip (exe + README.txt)
rem and its checksum dist\WeatherWidget-<version>.zip.sha256 - the widget checks it before updating itself
rem Usage: tools\make-release.bat 1.3.1   (the version must match Program.Version)
setlocal
cd /d "%~dp0.."
if "%~1"=="" (
    echo Usage: tools\make-release.bat ^<version^>
    exit /b 1
)
findstr /c:"Version = \"%~1\"" src\Program.cs >nul || (
    echo ERROR: src\Program.cs has a different version than %~1
    exit /b 1
)

call "%~dp0..\test.bat"
if errorlevel 1 exit /b 1

if not exist dist mkdir dist
set "OUT=dist\WeatherWidget-%~1.zip"
if exist "%OUT%" del "%OUT%"
set "STAGE=%TEMP%\WeatherWidget-release-%RANDOM%"
mkdir "%STAGE%"
copy /y bin\WeatherWidget.exe "%STAGE%\" >nul
copy /y tools\release-readme.txt "%STAGE%\README.txt" >nul
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%OUT%'"
set "ERR=%ERRORLEVEL%"
rmdir /s /q "%STAGE%"
if not "%ERR%"=="0" exit /b 1
powershell -NoProfile -Command "$h = (Get-FileHash '%OUT%' -Algorithm SHA256).Hash.ToLower(); [IO.File]::WriteAllText('%OUT%.sha256', $h + '  WeatherWidget-%~1.zip' + [char]10)"
if errorlevel 1 exit /b 1
echo OK: %OUT%
