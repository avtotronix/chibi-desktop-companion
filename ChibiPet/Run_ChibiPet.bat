@echo off
if exist "%~dp0ChibiPet.exe" (
    cd /d "%~dp0"
    start "" "ChibiPet.exe"
) else if exist "%~dp0Publish\ChibiPet.exe" (
    cd /d "%~dp0Publish"
    start "" "ChibiPet.exe"
)
