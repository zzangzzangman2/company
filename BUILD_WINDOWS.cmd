@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "BUILD_SCRIPT=%PROJECT_ROOT%\Tools\Build-FamilyCompanyWindows.ps1"

echo [Family Company] Building Windows x64 playtest...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%" -CanonicalProjectPath "%PROJECT_ROOT%"
set "BUILD_EXIT=%ERRORLEVEL%"

if not "%BUILD_EXIT%"=="0" (
    echo.
    echo [Family Company] BUILD FAILED. Check Builds\Windows\Automation\logs.
) else (
    echo.
    echo [Family Company] BUILD SUCCEEDED.
    echo Output: %PROJECT_ROOT%\Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe
)

if /I not "%~1"=="--no-pause" pause
exit /b %BUILD_EXIT%
