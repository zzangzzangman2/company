@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "BUILD_SCRIPT=%PROJECT_ROOT%\Tools\Build-FamilyCompanyWindows.ps1"
set "DEPLOYMENT_ROOT="
set "OUTPUT_PATH="
set "UNITY_EDITOR="
set "DRY_RUN_ARG="
set "NO_PAUSE="

:parse_args
if "%~1"=="" goto run_build
if /I "%~1"=="--deploy-root" (
    if "%~2"=="" goto usage_error
    set "DEPLOYMENT_ROOT=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--output" (
    if "%~2"=="" goto usage_error
    set "OUTPUT_PATH=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--unity" (
    if "%~2"=="" goto usage_error
    set "UNITY_EDITOR=%~2"
    shift
    shift
    goto parse_args
)
if /I "%~1"=="--dry-run" (
    set "DRY_RUN_ARG=-DryRun"
    shift
    goto parse_args
)
if /I "%~1"=="--no-pause" (
    set "NO_PAUSE=1"
    shift
    goto parse_args
)
goto usage_error

:run_build
if defined DEPLOYMENT_ROOT if defined OUTPUT_PATH goto usage_error

set "DEPLOYMENT_ARG="
set "OUTPUT_ARG="
set "UNITY_ARG="
if defined DEPLOYMENT_ROOT set "DEPLOYMENT_ARG=-DeploymentRoot "%DEPLOYMENT_ROOT%""
if defined OUTPUT_PATH set "OUTPUT_ARG=-FinalOutputPath "%OUTPUT_PATH%""
if defined UNITY_EDITOR set "UNITY_ARG=-UnityEditorPath "%UNITY_EDITOR%""

echo [Family Company] Building Windows x64 playtest...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%" -CanonicalProjectPath "%PROJECT_ROOT%" %DEPLOYMENT_ARG% %OUTPUT_ARG% %UNITY_ARG% %DRY_RUN_ARG%
set "BUILD_EXIT=%ERRORLEVEL%"

if not "%BUILD_EXIT%"=="0" (
    echo.
    echo [Family Company] BUILD FAILED. Check Builds\Windows\Automation\logs.
) else (
    echo.
    if defined DRY_RUN_ARG (
        echo [Family Company] DRY RUN SUCCEEDED.
    ) else (
        echo [Family Company] BUILD SUCCEEDED.
        if defined DEPLOYMENT_ROOT (
            echo Output: %DEPLOYMENT_ROOT%\FamilyCompany_Playtest\FamilyCompany.exe
        ) else if defined OUTPUT_PATH (
            echo Output: %OUTPUT_PATH%\FamilyCompany.exe
        ) else (
            echo Output: %PROJECT_ROOT%\Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe
        )
    )
)

if not defined NO_PAUSE pause
exit /b %BUILD_EXIT%

:usage_error
echo Usage: BUILD_WINDOWS.cmd [--deploy-root PATH ^| --output PATH] [--unity UNITY_EXE] [--dry-run] [--no-pause]
exit /b 64
