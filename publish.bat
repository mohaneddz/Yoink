@echo off
setlocal

cd /d "%~dp0"

echo Publishing Yoink (self-contained, single-file, win-x64)...
dotnet publish ".\src\Yoink.Presentation\Yoink.Presentation.csproj" -p:PublishProfile=win-x64
if errorlevel 1 goto :failed

echo.
echo Publish succeeded.
echo Output: src\Yoink.Presentation\bin\Release\net8.0-windows\win-x64\publish\Yoink.exe
exit /b 0

:failed
echo.
echo Publish failed.
exit /b 1
