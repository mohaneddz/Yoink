@echo off
setlocal

cd /d "%~dp0"

echo Publishing StreamVault (self-contained, single-file, win-x64)...
dotnet publish ".\src\StreamVault.Presentation\StreamVault.Presentation.csproj" -p:PublishProfile=win-x64
if errorlevel 1 goto :failed

echo.
echo Publish succeeded.
echo Output: src\StreamVault.Presentation\bin\Release\net8.0-windows\win-x64\publish\StreamVault.exe
exit /b 0

:failed
echo.
echo Publish failed.
exit /b 1
