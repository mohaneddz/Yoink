@echo off
setlocal

cd /d "%~dp0"

echo Restoring packages...
dotnet restore StreamVault.slnx
if errorlevel 1 goto :failed

echo Building StreamVault...
dotnet build StreamVault.slnx --configuration Debug --no-restore
if errorlevel 1 goto :failed

echo.
echo Build succeeded.
exit /b 0

:failed
echo.
echo Build failed.
exit /b 1
