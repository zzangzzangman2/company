@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_ROOT%\Tools\Stop-FamilyCompanyDeployWatch.ps1"
set "WATCH_EXIT=%ERRORLEVEL%"
if /I not "%~1"=="--no-pause" pause
exit /b %WATCH_EXIT%
