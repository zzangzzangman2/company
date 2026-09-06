@echo off
setlocal

set "GAME_ROOT=%USERPROFILE%\Downloads\FamilyCompany_Playtest"
set "GAME_EXE=%GAME_ROOT%\FamilyCompany.exe"
if not exist "%GAME_EXE%" (
    echo [Family Company] Install the published FamilyCompany-Windows.zip here once:
    echo %GAME_ROOT%
    echo See Docs\MAIN_GAME_ENTRY.md. Playing does not require a local build.
    pause
    exit /b 2
)

if not exist "%GAME_ROOT%\FamilyCompanyPatch\FamilyCompany.InGame.ps1" (
    echo [Family Company] This is an old installation without in-game patch support.
    echo Install the first verified full Windows package. Do not launch this old copy.
    echo See Docs\MAIN_GAME_ENTRY.md for current release and installation status.
    pause
    exit /b 3
)

start "Family Company" "%GAME_EXE%"
exit /b 0
