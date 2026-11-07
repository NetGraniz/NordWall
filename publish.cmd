@echo off
setlocal
cd /d "%~dp0"
if errorlevel 1 goto FolderError

where dotnet >nul 2>&1
if errorlevel 1 goto MissingSdk
dotnet --list-sdks | findstr /R "^[0-9]" >nul
if errorlevel 1 goto MissingSdk

echo Publishing Nordwall for Windows x64...
dotnet publish "Nordwall\Nordwall.csproj" --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None --output "%~dp0release\Nordwall-standalone-win-x64"
if errorlevel 1 goto PublishError

echo Creating standalone ZIP archive...
powershell -NoProfile -Command "Compress-Archive -Path '%~dp0release\Nordwall-standalone-win-x64\*' -DestinationPath '%~dp0release\Nordwall-standalone-win-x64.zip' -Force"
if errorlevel 1 goto PublishError

echo.
echo Standalone build: release\Nordwall-standalone-win-x64\Nordwall.exe
echo Archive: release\Nordwall-standalone-win-x64.zip
echo Nordwall creates Documents\Wallpapers, initially empty.
echo Move Nordwall.exe to its final location before enabling autostart.
pause
exit /b 0

:MissingSdk
echo .NET SDK was not found. Install .NET 10 SDK to build Nordwall.
pause
exit /b 1

:FolderError
echo Could not access the source folder.
pause
exit /b 1

:PublishError
echo The release build failed. See the error messages above.
pause
exit /b 1
