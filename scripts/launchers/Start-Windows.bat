@echo off
setlocal

set "APP_URL=http://localhost:5088"
set "APP_EXE=%~dp0MultimodalUIAnalyzer.exe"
set "OLLAMA_URL=http://localhost:11434"

echo.
echo === Multimodal UI Analyzer ===
echo.

if not exist "%APP_EXE%" (
    echo [ERROR] Application executable was not found:
    echo %APP_EXE%
    pause
    exit /b 1
)

echo [1/4] Checking Ollama...
where ollama >nul 2>nul
if errorlevel 1 (
    if exist "%LOCALAPPDATA%\Programs\Ollama\ollama.exe" (
        set "OLLAMA_EXE=%LOCALAPPDATA%\Programs\Ollama\ollama.exe"
    ) else (
        echo       Ollama is not installed. Installing with PowerShell install script...
        powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://ollama.com/install.ps1 | iex"
        if errorlevel 1 (
            echo [WARN] Could not install Ollama automatically.
            echo        Install it manually from https://ollama.com/download
            goto start_app
        )

        if exist "%LOCALAPPDATA%\Programs\Ollama\ollama.exe" (
            set "OLLAMA_EXE=%LOCALAPPDATA%\Programs\Ollama\ollama.exe"
        ) else (
            where ollama >nul 2>nul
            if errorlevel 1 (
                echo [WARN] Ollama was not found after installation.
                echo        The app will open, but local analysis needs Ollama.
                goto start_app
            ) else (
                set "OLLAMA_EXE=ollama"
            )
        )
    )
) else (
    set "OLLAMA_EXE=ollama"
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing '%OLLAMA_URL%/api/tags' -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
if errorlevel 1 (
    echo       Starting Ollama...
    start "Ollama Server" /min cmd /k ""%OLLAMA_EXE%" serve"
) else (
    echo       Ollama is already running.
)

:start_app
echo [2/4] Starting web server...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-WebRequest -UseBasicParsing '%APP_URL%' -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>nul
if errorlevel 1 (
    start "Multimodal UI Analyzer" /min "%APP_EXE%" --no-open
) else (
    echo       Web server is already running.
)

echo [3/4] Waiting for the app...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(25); do { try { Invoke-WebRequest -UseBasicParsing '%APP_URL%' -TimeoutSec 2 | Out-Null; exit 0 } catch { Start-Sleep -Seconds 1 } } while ((Get-Date) -lt $deadline); exit 1" >nul 2>nul
if errorlevel 1 (
    echo [ERROR] App did not respond on %APP_URL%.
    pause
    exit /b 1
)

echo [4/4] Opening browser...
start "" "%APP_URL%"

echo.
echo Ready: %APP_URL%
echo Missing Ollama models are downloaded lazily from the web UI.
echo.
exit /b 0
