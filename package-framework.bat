@echo off
setlocal

rem =====================================================
rem ApixPress framework-dependent publish script (win-x64)
rem Usage : run package-framework.bat from the repo root
rem Output: dist\apixpress-framework\           (runnable)
rem         dist\ApixPress-framework-win-x64.zip
rem About: framework-dependent = small package, requires
rem        the .NET 10 Desktop Runtime on target machines
rem        (https://dotnet.microsoft.com/download/dotnet/10.0).
rem Paired: package-selfcontained.bat bundles the runtime;
rem         package-aot.bat is reserved for a future NativeAOT
rem         build once Avalonia supports it better.
rem Note  : keep this file ASCII-only. cmd.exe on this
rem         machine mis-parses batch lines containing
rem         multi-byte UTF-8 comments (verified).
rem =====================================================

set "REPO_ROOT=%~dp0"
set "PROJECT=%REPO_ROOT%src\ApixPress.App\ApixPress.App.csproj"
set "OUTPUT_DIR=%REPO_ROOT%dist\apixpress-framework"
set "ZIP_PATH=%REPO_ROOT%dist\ApixPress-framework-win-x64.zip"

if not exist "%PROJECT%" (
    echo [ERROR] Project file not found: %PROJECT%
    exit /b 1
)

if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
if exist "%ZIP_PATH%" del /f /q "%ZIP_PATH%"

echo [1/3] Publishing ApixPress framework-dependent single-file (win-x64)...
echo Project: %PROJECT%
echo Output: %OUTPUT_DIR%

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false -o "%OUTPUT_DIR%"
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

echo Verification passed: single-file exe is present.

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
endlocal
