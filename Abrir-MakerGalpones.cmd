@echo off
setlocal
cd /d "%~dp0"
if exist "artifacts\desktop\MakerGalpones.exe" (
    start "" "artifacts\desktop\MakerGalpones.exe"
) else (
    dotnet run --project "src\Galpones.Desktop\Galpones.Desktop.csproj"
    if errorlevel 1 pause
)
