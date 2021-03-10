REM *** DOWNLOAD AND BUILD THE PROJECT ***
echo Git Branch: %APIMEVENTS-GIT-BRANCH% LogLevel: %APIMEVENTS-LOG-LEVEL%
if "%APIMEVENTS-GIT-BRANCH%" == "" set APIMEVENTS-GIT-BRANCH="master"
if "%APIMEVENTS-LOG-LEVEL%"  == "" set APIMEVENTS-LOG-LEVEL="Warn"
echo Git Branch: %APIMEVENTS-GIT-BRANCH% LogLevel: %APIMEVENTS-LOG-LEVEL%
rmdir %TEMP%\app /s /q
mkdir %TEMP%\app
cd %TEMP%\app
git clone -b %APIMEVENTS-GIT-BRANCH% https://github.com/Moesif/ApimEventProcessor
cd ApimEventProcessor\src\ApimEventProcessor
dotnet clean
nuget install packages.config
dotnet build --configuration "Release"
cd bin\Release
echo "Launching the app"
ApimEventProcessor.exe
