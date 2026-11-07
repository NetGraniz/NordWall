@echo off
setlocal
cd /d "%~dp0Nordwall"
if errorlevel 1 goto FolderError

where dotnet >nul 2>&1
if errorlevel 1 goto MissingSdk
dotnet --list-sdks | findstr /R "^[0-9]" >nul
if errorlevel 1 goto MissingSdk

echo Building and starting Nordwall...
dotnet run --project "Nordwall.csproj" --configuration Release
if errorlevel 1 goto StartError
exit /b 0

:MissingSdk
echo.
echo .NET SDK was not found. Sources cannot be built without the SDK.
echo Install .NET 10 SDK, then start run.cmd again.
echo https://dotnet.microsoft.com/download/dotnet/10.0
pause
exit /b 1

:FolderError
echo The Nordwall project folder could not be found next to run.cmd.
pause
exit /b 1

:StartError
echo.
echo Build or startup failed. The error is shown above.
echo Copy that error when asking for help.
pause
exit /b 1
