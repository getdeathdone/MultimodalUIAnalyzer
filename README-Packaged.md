# Multimodal UI Analyzer

This package contains a self-contained ASP.NET Core application.

## Windows

Run:

```bat
Start-Windows.bat
```

The launcher starts Ollama if it is installed, starts the web app, and opens:

```text
http://localhost:5088
```

## macOS

First run may require:

```bash
chmod +x ./MultimodalUIAnalyzer ./start-macos.sh ./start-macos.command
```

Then run:

```bash
./start-macos.command
```

or:

```bash
./start-macos.sh
```

## Ollama

Install Ollama from:

```text
https://ollama.com/download
```

Models are downloaded lazily. Pick a model in the web UI and click Analyze; missing models are pulled automatically with progress.
