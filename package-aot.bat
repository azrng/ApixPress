@echo off
setlocal

rem =====================================================
rem ApixPress NativeAOT publish script (win-x64)
rem Usage : run package-aot.bat from the repo root
rem Output: dist\apixpress-aot\            (runnable folder)
rem         dist\ApixPress-aot-win-x64.zip (zip package)
rem Note  : keep this file ASCII-only. cmd.exe on this
rem         machine mis-parses batch lines containing
rem         multi-byte UTF-8 comments (verified: comment
rem         fragments got executed as commands).
rem =====================================================

set "REPO_ROOT=%~dp0"
set "PROJECT=%REPO_ROOT%src\ApixPress.App\ApixPress.App.csproj"
set "OUTPUT_DIR=%REPO_ROOT%dist\apixpress-aot"
set "ZIP_PATH=%REPO_ROOT%dist\ApixPress-aot-win-x64.zip"

if not exist "%PROJECT%" (
    echo [ERROR] Project file not found: %PROJECT%
    exit /b 1
)

rem Background: an intermediate NativeAOT build output (an exe
rem WITHOUT the native dlls beside it, e.g. the folder
rem bin\Release\net10.0\win-x64\native) crashes on startup with
rem BadImageFormatException when Avalonia loads SkiaSharp.
rem A valid AOT package MUST ship the native libraries below,
rem so this script publishes to a clean folder and verifies them
rem before declaring success.
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%ZIP_PATH%" del /f /q "%ZIP_PATH%"

echo [1/3] Publishing ApixPress with NativeAOT (win-x64)...
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

echo.
echo [2/3] Verifying published files...

if not exist "%OUTPUT_DIR%\ApixPress.exe" (
    echo [ERROR] ApixPress.exe was not found in publish output
    exit /b 1
)

rem Avalonia/SkiaSharp and SQLite crash at startup if these
rem native libraries are missing next to the exe.
for %%F in (libSkiaSharp.dll libHarfBuzzSharp.dll av_libglesv2.dll e_sqlite3.dll) do (
    if not exist "%OUTPUT_DIR%\%%F" (
        echo [ERROR] Missing native library: %%F
        echo [ERROR] This package would crash on startup, aborting.
        exit /b 1
    )
)

echo Verification passed: exe and native libraries are present.

rem Strip debug symbol files: not needed at runtime, keep them out
rem of both the folder and the zip package.
del /q "%OUTPUT_DIR%\*.pdb" 2>nul

echo.
echo [3/3] Creating zip package...

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUTPUT_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 (
    echo [WARN] Compress-Archive failed, keep the folder output only
) else (
    echo Zip: %ZIP_PATH%
)

echo.
echo Done. Run the app from: %OUTPUT_DIR%\ApixPress.exe
echo Do NOT run intermediate outputs such as bin\...\native\ApixPress.exe.
endlocal
