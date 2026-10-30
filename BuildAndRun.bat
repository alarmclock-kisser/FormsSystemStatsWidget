@echo off
setlocal
chcp 65001 >nul

rem Get the directory where this .BAT resides (strip trailing backslash)
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

rem The build output lives under the Forms project
set "RELEASE_DIR=%ROOT%\FormsSystemStatsWidget.Forms\bin\Release"
set "PUBLISH_DIR=%RELEASE_DIR%\net10.0-windows\win-x64"
set "EXE_PATH=%PUBLISH_DIR%\FormsSystemStatsWidget.Forms.exe"
set "LNK_PATH=%ROOT%\FSSWidget (Publish now + Run).lnk"

rem === Step 0: Stop stale .NET host and app processes ===
echo.
echo ==========================================
echo Stopping stale .NET host and FSSWidget processes...
echo ==========================================

rem Ignore the error when no matching process is running.
taskkill /F /T /IM dotnet.exe >nul 2>&1
taskkill /F /T /IM FormsSystemStatsWidget.Forms.exe >nul 2>&1

echo OK: Stale processes have been stopped.

rem === Step 1: Wipe the entire bin\Release directory recursively ===
echo.
echo ==========================================
echo Wiping %RELEASE_DIR% (recursive)...
echo ==========================================

if exist "%RELEASE_DIR%" (
    rem First try: standard rmdir
    rmdir /s /q "%RELEASE_DIR%" 2>nul

    rem If it still exists, try PowerShell Remove-Item (handles locked files better)
    if exist "%RELEASE_DIR%" (
        echo First attempt failed, trying PowerShell Remove-Item...
        powershell -NoProfile -Command "Remove-Item -Path '%RELEASE_DIR%' -Recurse -Force -ErrorAction SilentlyContinue" 2>nul
    )

    rem Second retry after a short pause (gives OS time to release handles)
    if exist "%RELEASE_DIR%" (
        timeout /t 2 /nobreak >nul
        rmdir /s /q "%RELEASE_DIR%" 2>nul
    )
)

rem Verify everything is gone - abort if not (e.g. locked by a running process)
if exist "%RELEASE_DIR%" (
    echo.
    echo WARNING: %RELEASE_DIR% could not be fully removed.
    echo Some files may be locked. Attempting to continue anyway...
    echo (The publish step will overwrite files in place.)
) else (
    echo.
    echo OK: bin\Release directory fully wiped.
)
echo ==========================================

rem === Step 2: Publish (Release, self-contained, single-file) ===
echo.
echo ==========================================
echo Publishing FormsSystemStatsWidget.Forms (Release, self-contained, single-file)...
echo ==========================================

rem -o points the output directly at the win-x64 folder, so NO "publish"
rem    subfolder is created and there are no duplicate binaries.
dotnet publish "%ROOT%\FormsSystemStatsWidget.Forms\FormsSystemStatsWidget.Forms.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -o "%PUBLISH_DIR%"

if errorlevel 1 (
    echo.
    echo ERROR: Publish failed!
    pause
    exit /b 1
)

echo.
echo ==========================================
echo Publish completed successfully!
echo ==========================================

rem === Step 3: Create a shortcut to the EXE in the repo root ===
echo.
echo ==========================================
echo Creating shortcut in repo root...
echo ==========================================

if not exist "%EXE_PATH%" (
    echo.
    echo WARNING: Could not find %EXE_PATH%
    echo Please check the output directory manually.
    pause
    exit /b 1
)

rem Remove any stale shortcut first
if exist "%LNK_PATH%" del /f /q "%LNK_PATH%" 2>nul

powershell -NoProfile -Command ^
    "$ws = New-Object -ComObject WScript.Shell; " ^
    "$s = $ws.CreateShortcut('%LNK_PATH%'); " ^
    "$s.TargetPath = '%EXE_PATH%'; " ^
    "$s.WorkingDirectory = '%PUBLISH_DIR%'; " ^
    "$s.Description = 'FormsSystemStatsWidget.Forms (Release, self-contained, single-file)'; " ^
    "$s.Save()"

if errorlevel 1 (
    echo.
    echo WARNING: Could not create the shortcut.
    pause
    exit /b 1
)

echo.
echo OK: Shortcut created at %LNK_PATH%
echo ==========================================

rem === Step 4: Run the app ===
echo.
echo ==========================================
echo Starting FormsSystemStatsWidget.Forms.exe...
echo ==========================================
start "" "%EXE_PATH%"

echo.
echo Done.
endlocal
