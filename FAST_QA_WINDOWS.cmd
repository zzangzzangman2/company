@echo off
setlocal
set "PROJECT_ROOT=%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_ROOT%Tools\Invoke-FamilyCompanyFastQa.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"
endlocal & exit /b %EXIT_CODE%
