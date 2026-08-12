@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD_AND_INSTALL.ps1" %*
if errorlevel 1 (
    echo.
    echo Build/install failed.
    pause
    exit /b 1
)
echo.
echo Erenshor Campmaster build/install complete.
pause
