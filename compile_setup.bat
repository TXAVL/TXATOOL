@echo off
setlocal

echo ==========================================
echo        Building TXATOOL - WIN
echo ==========================================

pushd "%~dp0"
set "PROJECT_FILE=%~dp0TXATOOL.csproj"
set "MAKENSIS=C:\Program Files (x86)\NSIS\makensis.exe"
if not exist "%MAKENSIS%" set "MAKENSIS=C:\Program Files\NSIS\makensis.exe"

if not exist "%PROJECT_FILE%" (
    echo [ERROR] Project file not found: "%PROJECT_FILE%"
    popd
    exit /b 1
)
if not exist "%MAKENSIS%" (
    echo [ERROR] NSIS compiler ^(makensis.exe^) not found. Install NSIS 3.
    popd
    exit /b 1
)

echo [1/3] Cleaning old build files...
dotnet clean "%PROJECT_FILE%" -c Release
if exist "bin\Release" rmdir /s /q "bin\Release"

echo.
echo [2/3] Publishing win-x64 (self-contained, .NET included)...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true
if %errorlevel% neq 0 (
    echo [ERROR] Dotnet publish failed!
    popd
    exit /b %errorlevel%
)

echo.
echo [3/3] Compiling NSIS installer...
if not exist Output mkdir Output
"%MAKENSIS%" /INPUTCHARSET UTF8 "%~dp0TXATOOL.nsi"
if %errorlevel% neq 0 (
    echo [ERROR] NSIS compilation failed!
    popd
    exit /b %errorlevel%
)

echo.
echo ==========================================
echo    SUCCESS: Output\TXATOOL_Setup_v1.0.0.exe
echo ==========================================
popd
