@echo off
setlocal
set "player=%~dp0Builds\Latest\EarthRecovery.exe"
if not exist "%player%" (
    echo Latest build not found. Build with Unity: Earth Recovery ^> Build Windows Prototype.
    pause
    exit /b 1
)
start "" /D "%~dp0Builds\Latest" "%player%" --dev-solo
