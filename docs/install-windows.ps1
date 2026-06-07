param(
    [string]$AppUrl,
    [string]$OllamaInstallScriptUrl = "https://ollama.com/install.ps1"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AppUrl)) {
    Write-Host "Usage: install-windows.ps1 -AppUrl <MultimodalUIAnalyzer-win-x64.exe URL>"
    exit 1
}

$installRoot = Join-Path $env:LOCALAPPDATA "Programs\MultimodalUIAnalyzer"
$appPath = Join-Path $installRoot "MultimodalUIAnalyzer.exe"
$ollamaPath = Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe"
$localAppUrl = "http://localhost:5088"

Write-Host "Multimodal UI Analyzer Windows setup"
Write-Host ""

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null

Write-Host "==> Downloading Multimodal UI Analyzer"
Invoke-WebRequest -UseBasicParsing -Uri $AppUrl -OutFile $appPath

if (-not (Test-Path $ollamaPath) -and -not (Get-Command ollama -ErrorAction SilentlyContinue)) {
    Write-Host "==> Ollama was not found. Installing with PowerShell install script"
    Invoke-RestMethod -Uri $OllamaInstallScriptUrl | Invoke-Expression
}
else {
    Write-Host "==> Ollama is already installed"
}

Write-Host "==> Starting Multimodal UI Analyzer"
Start-Process -FilePath $appPath -ArgumentList @("--no-open")

Write-Host "==> Waiting for local web app"
$deadline = (Get-Date).AddSeconds(45)
do {
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $localAppUrl -TimeoutSec 2 | Out-Null
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
} while ((Get-Date) -lt $deadline)

try {
    Invoke-WebRequest -UseBasicParsing -Uri $localAppUrl -TimeoutSec 2 | Out-Null
}
catch {
    throw "The app did not respond on $localAppUrl"
}

Write-Host "==> Opening browser"
Start-Process $localAppUrl

Write-Host ""
Write-Host "Done. Opened $localAppUrl"
