@echo off
setlocal

cd /d "%~dp0"

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
