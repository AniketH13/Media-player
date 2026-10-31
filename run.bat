@echo off
setlocal
cd /d "%~dp0"

:: If a file is dropped onto this batch file or passed as argument, pass it through to the player
if exist "publish-selfcontained\MediaPlayer.exe" (
    start "" "publish-selfcontained\MediaPlayer.exe" %*
    exit /b 0
)

if exist "publish\MediaPlayer.exe" (
    start "" "publish\MediaPlayer.exe" %*
    exit /b 0
)

if exist "bin\Release\net10.0-windows\MediaPlayer.exe" (
    start "" "bin\Release\net10.0-windows\MediaPlayer.exe" %*
    exit /b 0
)

if exist "bin\Debug\net10.0-windows\MediaPlayer.exe" (
    start "" "bin\Debug\net10.0-windows\MediaPlayer.exe" %*
    exit /b 0
)

echo Building and launching MediaPlayer...
dotnet run -- %*

