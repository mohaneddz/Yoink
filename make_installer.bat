@echo off
setlocal

cd /d "%~dp0"

echo [1/2] Publishing Yoink release build...
call publish.bat
if errorlevel 1 goto :failed

if not exist "dist" mkdir dist

echo.
echo [2/2] Building Windows Installer (YoinkSetup.msi)...
wix build .\installer\Package.wxs -d SourceDir=".\src\Yoink.Presentation\bin\Release\net10.0-windows\win-x64\publish" -d AppIconPath=".\src\Yoink.Presentation\app.ico" -o ".\dist\YoinkSetup.msi"
if errorlevel 1 goto :failed

echo.
echo ===================================================
echo Installer created successfully!
echo Target: dist\YoinkSetup.msi
echo ===================================================
exit /b 0

:failed
echo.
echo Installer build failed.
exit /b 1
