@echo off
setlocal

cd /d "%~dp0"

tasklist /FI "IMAGENAME eq Yoink.exe" /NH | findstr /I /C:"Yoink.exe" >nul
if not errorlevel 1 (
    echo Yoink is still running. Exit from the tray before rebuilding.
    echo Active downloads have been left running.
    exit /b 1
)

echo Restoring packages...
dotnet restore Yoink.slnx
if errorlevel 1 goto :failed

echo Building Yoink...
dotnet build Yoink.slnx --configuration Debug --no-restore
if errorlevel 1 goto :failed

echo.
echo Build succeeded.
exit /b 0

:failed
echo.
echo Build failed.
exit /b 1
