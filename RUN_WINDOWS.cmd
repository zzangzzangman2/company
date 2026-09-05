@echo off
setlocal

for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "GAME_EXE=%PROJECT_ROOT%\Builds\Windows\FamilyCompany_Playtest\FamilyCompany.exe"
if not exist "%GAME_EXE%" (
    echo [Family Company] No verified local game build was found.
    echo [Family Company] Use the current published Windows game package.
    pause
    exit /b 2
)

start "Family Company" "%GAME_EXE%"
exit /b 0
