@echo off
setlocal

cd /d "%~dp0"

if not exist ".\src\Yoink.Presentation\bin\Debug\net8.0-windows\Yoink.exe" (
    echo Build output not found. Run build.bat first.
    exit /b 1
)

dotnet run --project ".\src\Yoink.Presentation\Yoink.Presentation.csproj" --no-build
exit /b %errorlevel%
