@echo off
setlocal
cd /d "%~dp0"

echo [Yoink] Closing any running instances...
taskkill /F /IM Yoink.exe >nul 2>&1

echo [Yoink] Building latest code...
dotnet build src\Yoink.Presentation\Yoink.Presentation.csproj --configuration Debug
if errorlevel 1 (
    echo [Yoink] Build failed.
    pause
    exit /b 1
)

echo [Yoink] Launching Yoink...
start "" "src\Yoink.Presentation\bin\Debug\net8.0-windows\Yoink.exe"
exit /b 0
