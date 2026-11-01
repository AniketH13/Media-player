@echo off
setlocal
cd /d "%~dp0"

:: If a file is dropped onto this batch file or passed as argument, pass it through to the player
if exist "publish-selfcontained\Frame.exe" (
    start "" "publish-selfcontained\Frame.exe" %*
    exit /b 0
)

if exist "publish\Frame.exe" (
    start "" "publish\Frame.exe" %*
    exit /b 0
)

if exist "bin\Release\net10.0-windows\Frame.exe" (
    start "" "bin\Release\net10.0-windows\Frame.exe" %*
    exit /b 0
)

if exist "bin\Debug\net10.0-windows\Frame.exe" (
    start "" "bin\Debug\net10.0-windows\Frame.exe" %*
    exit /b 0
)

echo Building and launching Frame...
dotnet run --project Frame.csproj -- %*

