@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "GAME_EXE=%PROJECT_ROOT%\Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe"
if not exist "%GAME_EXE%" (
    echo [Family Company] No local player build was found.
    echo Run BUILD_WINDOWS.cmd first.
    pause
    exit /b 2
)

start "Family Company" "%GAME_EXE%"
exit /b 0
