$ErrorActionPreference = "Stop"

param(
    [string]$AppUrl,
    [string]$OllamaInstallerUrl = "https://ollama.com/download/OllamaSetup.exe"
)

if ([string]::IsNullOrWhiteSpace($AppUrl)) {
    Write-Host "Usage: install-windows.ps1 -AppUrl <MultimodalUIAnalyzer-win-x64.exe URL>"
    exit 1
}

$installRoot = Join-Path $env:LOCALAPPDATA "Programs\MultimodalUIAnalyzer"
$appPath = Join-Path $installRoot "MultimodalUIAnalyzer.exe"
$ollamaPath = Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe"
$ollamaSetup = Join-Path $env:TEMP "OllamaSetup.exe"

Write-Host "Multimodal UI Analyzer Windows setup"
Write-Host ""

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null

Write-Host "==> Downloading Multimodal UI Analyzer"
Invoke-WebRequest -UseBasicParsing -Uri $AppUrl -OutFile $appPath

if (-not (Test-Path $ollamaPath) -and -not (Get-Command ollama -ErrorAction SilentlyContinue)) {
    Write-Host "==> Ollama was not found. Downloading Ollama installer"
    Invoke-WebRequest -UseBasicParsing -Uri $OllamaInstallerUrl -OutFile $ollamaSetup
    Write-Host "==> Starting Ollama installer"
    Write-Host "    Finish the Ollama installer window if it appears."
    Start-Process -FilePath $ollamaSetup -Wait
}
else {
    Write-Host "==> Ollama is already installed"
}

Write-Host "==> Starting Multimodal UI Analyzer"
Start-Process -FilePath $appPath

Write-Host ""
Write-Host "Done. The browser should open http://localhost:5088"
