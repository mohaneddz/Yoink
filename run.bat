@echo off
setlocal
cd /d "%~dp0"

tasklist /FI "IMAGENAME eq Yoink.exe" /NH | findstr /I /C:"Yoink.exe" >nul
if not errorlevel 1 (
    echo [Yoink] Yoink is already running. Restore it from the tray.
    echo [Yoink] To rebuild, exit Yoink from the tray after your downloads finish.
    exit /b 0
)

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
