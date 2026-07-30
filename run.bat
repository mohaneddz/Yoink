@echo off
setlocal

cd /d "%~dp0"

if not exist ".\src\StreamVault.Presentation\bin\Debug\net8.0-windows\StreamVault.exe" (
    echo Build output not found. Run build.bat first.
    exit /b 1
)

dotnet run --project ".\src\StreamVault.Presentation\StreamVault.Presentation.csproj" --no-build
exit /b %errorlevel%
