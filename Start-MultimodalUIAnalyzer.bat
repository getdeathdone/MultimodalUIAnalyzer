@echo off
setlocal enabledelayedexpansion

set "ROOT_DIR=%~dp0"
set "APP_DIR=%ROOT_DIR%"
set "APP_URL=http://localhost:5088"
set "OLLAMA_URL=http://localhost:11434"

echo.
echo === Multimodal UI Analyzer ===
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet SDK was not found in PATH.
    echo Install .NET 8 SDK and run this file again.
    pause
    exit /b 1
)

if not exist "%APP_DIR%\MultimodalUIAnalyzer.csproj" (
    echo [ERROR] Project file was not found:
    echo %APP_DIR%\MultimodalUIAnalyzer.csproj
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing '%APP_URL%' -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
if errorlevel 1 (
    set "WEB_ALREADY_RUNNING=0"
) else (
    set "WEB_ALREADY_RUNNING=1"
)

echo [1/5] Checking Ollama...
where ollama >nul 2>nul
if errorlevel 1 (
    echo [WARN] Ollama was not found in PATH.
    echo        The web app will still start, but local vision analysis will fail
    echo        while appsettings.Development.json uses Ai:Provider = Ollama.
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing '%OLLAMA_URL%/api/tags' -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
    if errorlevel 1 (
        echo       Starting Ollama server...
        start "Ollama Server" /min cmd /k "ollama serve"
        powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(20); do { try { Invoke-WebRequest -UseBasicParsing '%OLLAMA_URL%/api/tags' -TimeoutSec 2 | Out-Null; exit 0 } catch { Start-Sleep -Seconds 1 } } while ((Get-Date) -lt $deadline); exit 1" >nul 2>nul
        if errorlevel 1 (
            echo [WARN] Ollama did not respond on %OLLAMA_URL%.
        )
    ) else (
        echo       Ollama is already running.
    )

    echo [2/5] Model downloads are lazy.
    echo       Pick a model on the web page; missing models will be pulled on first use.
)

echo [3/5] Building ASP.NET Core app...
if "%WEB_ALREADY_RUNNING%"=="1" (
    echo       Web server is already running, skipping build to avoid locked files.
) else (
    pushd "%APP_DIR%"
    dotnet build
    if errorlevel 1 (
        popd
        echo [ERROR] Build failed.
        pause
        exit /b 1
    )
    popd
)

echo [4/5] Starting web server...
if "%WEB_ALREADY_RUNNING%"=="0" (
    start "Multimodal UI Analyzer Server" cmd /k "cd /d ""%APP_DIR%"" && dotnet bin\Debug\net8.0\MultimodalUIAnalyzer.dll --no-open"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(20); do { try { Invoke-WebRequest -UseBasicParsing '%APP_URL%' -TimeoutSec 2 | Out-Null; exit 0 } catch { Start-Sleep -Seconds 1 } } while ((Get-Date) -lt $deadline); exit 1" >nul 2>nul
    if errorlevel 1 (
        echo [ERROR] Web server did not respond on %APP_URL%.
        pause
        exit /b 1
    )
) else (
    echo       Web server is already running on %APP_URL%.
)

echo [5/5] Opening browser...
start "" "%APP_URL%"

echo.
echo Ready: %APP_URL%
echo Keep the server window open while using the app.
echo.
exit /b 0
