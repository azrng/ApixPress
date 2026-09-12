@echo off
setlocal

rem =====================================================
rem ApixPress NativeAOT one-click package script
rem Usage: run package-aot.bat from the repo root
rem Output: dist\ApixPress.exe (NativeAOT self-contained)
rem Note: keep this file ASCII-only. cmd.exe on this machine
rem       mis-parses batch lines containing multi-byte UTF-8
rem       comments (verified: comment fragments got executed
rem       as commands), so Chinese docs live in README/report.
rem =====================================================

set "REPO_ROOT=%~dp0"
set "PROJECT=%REPO_ROOT%src\ApixPress.App\ApixPress.App.csproj"
set "OUTPUT_DIR=%REPO_ROOT%dist"

if not exist "%PROJECT%" (
    echo [ERROR] Project file not found: %PROJECT%
    exit /b 1
)

rem Clean the previous dist output to avoid stale files
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

echo [1/2] Publishing ApixPress with NativeAOT (win-x64)...
echo Project: %PROJECT%
echo Output: %OUTPUT_DIR%

rem These command-line properties override the Release config
rem in the csproj: PublishAot=false / PublishTrimmed=false /
rem PublishReadyToRun=true
dotnet publish "%PROJECT%" -c Release -r win-x64 -o "%OUTPUT_DIR%" -p:PublishAot=true -p:PublishTrimmed=true -p:PublishReadyToRun=false
if errorlevel 1 (
    echo [ERROR] dotnet publish failed, check the log above
    exit /b 1
)

if not exist "%OUTPUT_DIR%\ApixPress.exe" (
    echo [ERROR] Publish finished but ApixPress.exe was not found
    exit /b 1
)

echo [2/2] Done: %OUTPUT_DIR%\ApixPress.exe
endlocal
