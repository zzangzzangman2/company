@echo off
setlocal

for %%I in ("%~dp0.") do set "GAME_ROOT=%%~fI"
set "GAME_EXE=%GAME_ROOT%\FamilyCompany.exe"
if not exist "%GAME_EXE%" (
    echo [Family Company] FamilyCompany.exe is missing beside this runner.
    pause
    exit /b 2
)

start "Family Company" "%GAME_EXE%"
exit /b 0
