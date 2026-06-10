# Multimodal UI Analyzer

Local-first .NET 8 web app for analyzing UI screenshots with vision models and returning structured JSON.

The app lets a user upload an image, enter a prompt, choose a vision model, and receive a JSON response with detected UI elements, approximate bounds, hierarchy, and accessibility notes.

## Highlights

- ASP.NET Core Minimal API backend
- Vanilla HTML/CSS/JavaScript frontend
- Microsoft Semantic Kernel integration
- Vendor-agnostic AI configuration
- OpenAI `gpt-4o` support
- Ollama local vision model support
- Lazy Ollama model download with progress in the UI
- Self-contained Windows and macOS packaging
- GitHub Pages setup page with one-command installers

## Tech Stack

- .NET 8
- ASP.NET Core Minimal API
- Microsoft Semantic Kernel
- OpenAI Chat Completion connector
- Ollama connector
- JavaScript, HTML/CSS
- PowerShell and Bash installer scripts

## Supported Providers

### Ollama

Default local provider. Recommended for local-first usage.

Configured models:

- `qwen2.5vl:7b` - recommended starting point for UI screenshots and OCR
- `llama3.2-vision:11b` - stronger general vision reasoning, heavier
- `llava-llama3:8b` - speed/quality compromise
- `llava` - fallback for older hardware

The app downloads only the selected model when it is needed.

### OpenAI

Cloud provider option using `gpt-4o`.

Set `Ai:Provider` to `OpenAI` and provide `Ai:OpenAI:ApiKey` in `appsettings.json` or user secrets/environment configuration.

## Configuration

Main configuration lives in `appsettings.json`.

```json
{
  "Ai": {
    "Provider": "Ollama",
    "ServiceId": "vision-chat",
    "OpenAI": {
      "ModelId": "gpt-4o",
      "ApiKey": "",
      "OrganizationId": ""
    },
    "Ollama": {
      "ModelId": "qwen2.5vl:7b",
      "Models": [
        "qwen2.5vl:7b",
        "llama3.2-vision:11b",
        "llava-llama3:8b",
        "llava"
      ],
      "Endpoint": "http://localhost:11434"
    }
  }
}
```

## Run Locally

Requirements:

- .NET 8 SDK
- Ollama, if using local models

Run:

```powershell
dotnet restore
dotnet run --urls http://localhost:5088
```

Open:

```text
http://localhost:5088
```

Setup page:

```text
http://localhost:5088/setup.html
```

## API

### Status

```http
GET /api/status
```

Returns the current provider and default model.

### Analyze Screenshot

```http
POST /api/vision/analyze
Content-Type: multipart/form-data
```

Fields:

- `image` - PNG, JPEG, or WebP image
- `prompt` - user prompt for the analysis
- `model` - optional model override

### Ensure Ollama Model

```http
GET /api/vision/models/ensure?model=qwen2.5vl:7b
```

Starts a lazy model download when the selected Ollama model is missing.

## Packaging

Create self-contained packages for Windows and macOS:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Version 0.1.0
```

Generated packages:

```text
artifacts/packages/
```

The script also copies packages to:

```text
wwwroot/downloads/
docs/downloads/
hosted-ui/downloads/
```

## Installers

The setup page provides one-command installers.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -Command "iwr -UseBasicParsing 'https://getdeathdone.github.io/MultimodalUIAnalyzer/install-windows.ps1' -OutFile $env:TEMP\mua-install.ps1; & $env:TEMP\mua-install.ps1 -AppUrl 'https://getdeathdone.github.io/MultimodalUIAnalyzer/downloads/MultimodalUIAnalyzer-0.1.0-win-x64.zip'"
```

macOS Apple Silicon:

```bash
curl -L 'https://getdeathdone.github.io/MultimodalUIAnalyzer/install-mac.sh' | bash -s -- 'https://getdeathdone.github.io/MultimodalUIAnalyzer/downloads/MultimodalUIAnalyzer-0.1.0-osx-arm64.zip'
```

macOS Intel:

```bash
curl -L 'https://getdeathdone.github.io/MultimodalUIAnalyzer/install-mac.sh' | bash -s -- 'https://getdeathdone.github.io/MultimodalUIAnalyzer/downloads/MultimodalUIAnalyzer-0.1.0-osx-x64.zip'
```

## Project Structure

```text
Api/                    Minimal API endpoint mappings
Configuration/          Options and provider configuration
DependencyInjection/    Semantic Kernel and service registration
Services/               AI analysis and Ollama model services
wwwroot/                Local web UI and setup page
docs/                   GitHub Pages setup site
hosted-ui/              Standalone hosted setup copy
scripts/                Packaging and launcher scripts
```

## Notes

- Ollama runs on `http://localhost:11434` by default.
- The web app runs on `http://localhost:5088`.
- Image uploads are limited by `VisionAnalysis:MaxImageBytes`.
- Allowed image types are configured in `VisionAnalysis:AllowedMimeTypes`.

