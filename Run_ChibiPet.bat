@echo off
title ChibiPet Launcher
cd /d "%~dp0"

if exist "Publish\ChibiPet.exe" (
    start "" "Publish\ChibiPet.exe"
    exit
)

if exist "Releases\ChibiPet-Setup.exe" (
    start "" "Releases\ChibiPet-Setup.exe"
    exit
)

echo Compiling and running ChibiPet...
dotnet run --project ChibiPet\ChibiPet.csproj -c Release
