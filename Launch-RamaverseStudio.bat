@echo off
title Ramaverse Studio Launcher
echo ========================================================
echo   RAMAVERSE STUDIO - Record. Stream. Create.
echo   Launching Windows Native Creator Studio...
echo ========================================================

rem Prefer the self-contained worldwide build (no .NET install needed)
set "WORLDWIDE=%~dp0publish\worldwide\RamaverseStudio.exe"
set "LOCALBUILD=%~dp0RamaverseStudio\bin\Release\net10.0-windows\RamaverseStudio.exe"

if exist "%WORLDWIDE%" (
    start "" "%WORLDWIDE%"
    exit /b 0
)

if exist "%LOCALBUILD%" (
    start "" "%LOCALBUILD%"
    exit /b 0
)

echo.
echo No built executable found. Building now (first run takes ~1 minute)...
echo.
dotnet build "%~dp0RamaverseStudio\RamaverseStudio.csproj" -c Release
if errorlevel 1 (
    echo.
    echo Build failed. Make sure the .NET 10 SDK is installed:
    echo   https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)
start "" "%LOCALBUILD%"
exit /b 0
