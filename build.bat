@echo off
setlocal enabledelayedexpansion
title Media Player - Manual Build Tool

cd /d "%~dp0"

:: 1. Verify .NET SDK installation
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK was not found in your system PATH!
    echo Please install the .NET 10 SDK: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

:: 2. Handle command line arguments if passed directly (e.g., build.bat selfcontained)
if /i "%~1"=="selfcontained" goto :build_selfcontained
if /i "%~1"=="standalone" goto :build_selfcontained
if /i "%~1"=="release" goto :build_release
if /i "%~1"=="debug" goto :build_debug
if /i "%~1"=="all" goto :build_all
if /i "%~1"=="clean" goto :clean_all
if /i "%~1"=="installer" goto :build_installer

:menu
cls
echo ===============================================================================
echo                         MEDIA PLAYER - BUILD TOOL
echo ===============================================================================
echo.
echo   [1] Build Self-Contained Release (Recommended)
echo       - Generates portable win-x64 app (runs on any Windows PC without .NET)
echo       - Output: .\publish-selfcontained\MediaPlayer.exe
echo.
echo   [2] Build Framework-Dependent Release
echo       - Generates compact build (requires .NET Desktop Runtime on PC)
echo       - Output: .\publish\MediaPlayer.exe
echo.
echo   [3] Build Debug Version
echo       - Fast compilation for testing and debugging
echo       - Output: .\bin\Debug\net10.0-windows\MediaPlayer.exe
echo.
echo   [4] Build All Targets (Self-Contained + Framework-Dependent + Debug)
echo.
echo   [5] Clean All Build Folders (bin, obj, publish)
echo.
echo   [6] Compile Inno Setup Installer (MediaPlayerSetup.iss)
echo.
echo   [7] Exit
echo.
echo ===============================================================================
set /p choice="Select an option (1-7): "

if "%choice%"=="1" goto :build_selfcontained
if "%choice%"=="2" goto :build_release
if "%choice%"=="3" goto :build_debug
if "%choice%"=="4" goto :build_all
if "%choice%"=="5" goto :clean_all
if "%choice%"=="6" goto :build_installer
if "%choice%"=="7" exit /b 0

echo.
echo Invalid selection. Please choose an option from 1 to 7.
timeout /t 2 >nul
goto :menu

:check_running
tasklist /fi "imagename eq MediaPlayer.exe" 2>nul | find /i "MediaPlayer.exe" >nul
if %ERRORLEVEL% equ 0 (
    echo [NOTICE] MediaPlayer.exe is currently running.
    echo Closing running instances to prevent file-locking issues during build...
    taskkill /f /im MediaPlayer.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
)
exit /b 0

:build_selfcontained
call :check_running
echo.
echo ===============================================================
echo  Building Self-Contained Win-x64 Release...
echo ===============================================================
dotnet publish MediaPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish-selfcontained
if %ERRORLEVEL% neq 0 (
    echo.
    echo [BUILD ERROR] Self-contained publish failed! Check messages above.
    echo.
    pause
    goto :menu
)
echo.
echo [SUCCESS] Self-Contained build completed successfully!
echo Executable location:
echo   %~dp0publish-selfcontained\MediaPlayer.exe
echo.
pause
goto :menu

:build_release
call :check_running
echo.
echo ===============================================================
echo  Building Framework-Dependent Release...
echo ===============================================================
dotnet publish MediaPlayer.csproj -c Release --no-self-contained -o .\publish
if %ERRORLEVEL% neq 0 (
    echo.
    echo [BUILD ERROR] Framework-dependent publish failed! Check messages above.
    echo.
    pause
    goto :menu
)
echo.
echo [SUCCESS] Release build completed successfully!
echo Executable location:
echo   %~dp0publish\MediaPlayer.exe
echo.
pause
goto :menu

:build_debug
call :check_running
echo.
echo ===============================================================
echo  Building Debug Version...
echo ===============================================================
dotnet build MediaPlayer.csproj -c Debug
if %ERRORLEVEL% neq 0 (
    echo.
    echo [BUILD ERROR] Debug build failed! Check messages above.
    echo.
    pause
    goto :menu
)
echo.
echo [SUCCESS] Debug build completed successfully!
echo Executable location:
echo   %~dp0bin\Debug\net10.0-windows\MediaPlayer.exe
echo.
pause
goto :menu

:build_all
call :check_running
echo.
echo ===============================================================
echo  Building All Targets (Self-Contained + Release + Debug)...
echo ===============================================================
echo.
echo Step 1/3: Building Debug...
dotnet build MediaPlayer.csproj -c Debug
if %ERRORLEVEL% neq 0 goto :all_failed

echo.
echo Step 2/3: Building Framework-Dependent Release...
dotnet publish MediaPlayer.csproj -c Release --no-self-contained -o .\publish
if %ERRORLEVEL% neq 0 goto :all_failed

echo.
echo Step 3/3: Building Self-Contained Win-x64 Release...
dotnet publish MediaPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish-selfcontained
if %ERRORLEVEL% neq 0 goto :all_failed

echo.
echo ===============================================================
echo [SUCCESS] All targets built successfully!
echo   1. %~dp0publish-selfcontained\MediaPlayer.exe
echo   2. %~dp0publish\MediaPlayer.exe
echo   3. %~dp0bin\Debug\net10.0-windows\MediaPlayer.exe
echo ===============================================================
echo.
pause
goto :menu

:all_failed
echo.
echo [BUILD ERROR] One or more build steps failed!
echo.
pause
goto :menu

:clean_all
call :check_running
echo.
echo ===============================================================
echo  Cleaning build and publish directories...
echo ===============================================================
dotnet clean MediaPlayer.csproj >nul 2>&1
if exist "bin" rd /s /q "bin" >nul 2>&1
if exist "obj" rd /s /q "obj" >nul 2>&1
if exist "publish" rd /s /q "publish" >nul 2>&1
if exist "publish-selfcontained" rd /s /q "publish-selfcontained" >nul 2>&1
echo [SUCCESS] Workspace cleaned successfully.
echo.
pause
goto :menu

:build_installer
echo.
echo ===============================================================
echo  Building Inno Setup Installer...
echo ===============================================================
set "ISCC_PATH="
where iscc.exe >nul 2>&1 && for /f "delims=" %%i in ('where iscc.exe') do set "ISCC_PATH=%%i"
if not defined ISCC_PATH if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not defined ISCC_PATH if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"

if not defined ISCC_PATH (
    echo [NOTICE] Inno Setup compiler (ISCC.exe) was not found.
    echo To build the installer EXE, download Inno Setup 6 from:
    echo   https://jrsoftware.org/isdl.php
    echo.
    pause
    goto :menu
)

if not exist "publish-selfcontained\MediaPlayer.exe" (
    echo [INFO] Self-contained build not found. Compiling first...
    dotnet publish MediaPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish-selfcontained
)

"%ISCC_PATH%" MediaPlayerSetup.iss
if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Installer successfully compiled to Output\MediaPlayerSetup.exe
) else (
    echo.
    echo [ERROR] Inno Setup compilation failed.
)
echo.
pause
goto :menu
