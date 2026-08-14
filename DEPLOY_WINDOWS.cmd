@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "DEPLOY_SCRIPT=%PROJECT_ROOT%\Tools\Deploy-FamilyCompanyWindows.ps1"
set "DEPLOY_ARGS="
set "NO_PAUSE=0"
if /I "%~1"=="--dry-run" set "DEPLOY_ARGS=-DryRun"
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"
if /I "%~2"=="--no-pause" set "NO_PAUSE=1"

echo [Family Company] Windows Release deployment pipeline...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_SCRIPT%" %DEPLOY_ARGS%
set "DEPLOY_EXIT=%ERRORLEVEL%"

if "%DEPLOY_EXIT%"=="34" (
    echo [Family Company] DEPLOYMENT PENDING: close FamilyCompany.exe; a running watcher will retry automatically.
) else if not "%DEPLOY_EXIT%"=="0" (
    echo [Family Company] DEPLOYMENT FAILED OR HELD. Check the printed status and log paths.
) else (
    echo [Family Company] DEPLOYMENT COMMAND SUCCEEDED.
)

if "%NO_PAUSE%"=="0" pause
exit /b %DEPLOY_EXIT%
