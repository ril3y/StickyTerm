@echo off
setlocal

echo ========================================
echo Building StickyTerm Installer
echo ========================================
echo.

:: Check for Inno Setup
set ISCC_PATH=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles%\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" (
    echo ERROR: Inno Setup 6 not found!
    echo.
    echo Please install Inno Setup 6 from:
    echo https://jrsoftware.org/isdl.php
    echo.
    pause
    exit /b 1
)

echo Found Inno Setup at: %ISCC_PATH%
echo.

:: Clean previous build
echo [1/4] Cleaning previous build...
if exist "publish" rmdir /s /q "publish"
if exist "Output" rmdir /s /q "Output"

:: Restore packages
echo [2/4] Restoring packages...
dotnet restore StickyTerm
if errorlevel 1 (
    echo ERROR: Package restore failed!
    pause
    exit /b 1
)

:: Publish the application
echo [3/4] Publishing application...
dotnet publish StickyTerm -c Release -r win-x64 --self-contained true -o publish
if errorlevel 1 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)

:: Create output directory
if not exist "Output" mkdir "Output"

:: Build the installer
echo [4/4] Building installer...
"%ISCC_PATH%" Installer\StickyTerm.iss
if errorlevel 1 (
    echo ERROR: Installer build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Installer created at: Output\StickyTerm_Setup_1.0.0.exe
echo.

pause
