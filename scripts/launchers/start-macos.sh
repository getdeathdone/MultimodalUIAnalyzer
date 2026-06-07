#!/usr/bin/env bash
set -euo pipefail

APP_URL="http://localhost:5088"
OLLAMA_URL="http://localhost:11434"
DIR="$(cd "$(dirname "$0")" && pwd)"
APP="$DIR/MultimodalUIAnalyzer"

echo
echo "=== Multimodal UI Analyzer ==="
echo

if [[ ! -f "$APP" ]]; then
  echo "[ERROR] Application executable was not found: $APP"
  exit 1
fi

echo "[1/4] Checking Ollama..."
if ! command -v ollama >/dev/null 2>&1; then
  echo "      Ollama is not installed."
  if command -v brew >/dev/null 2>&1; then
    echo "      Installing Ollama with Homebrew..."
    brew install ollama
  else
    echo "[WARN] Homebrew is not installed."
    echo "       Install Homebrew or install Ollama manually from https://ollama.com/download"
    if command -v open >/dev/null 2>&1; then
      open "https://ollama.com/download"
    fi
  fi
fi

if command -v ollama >/dev/null 2>&1; then
  if ! curl -fsS "$OLLAMA_URL/api/tags" >/dev/null 2>&1; then
    echo "      Starting Ollama..."
    nohup ollama serve > "$DIR/ollama.log" 2>&1 &
  else
    echo "      Ollama is already running."
  fi
fi

echo "[2/4] Starting web server..."
chmod +x "$APP" || true
if ! curl -fsS "$APP_URL" >/dev/null 2>&1; then
  nohup "$APP" --no-open > "$DIR/app.log" 2>&1 &
else
  echo "      Web server is already running."
fi

echo "[3/4] Waiting for the app..."
for _ in {1..25}; do
  if curl -fsS "$APP_URL" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl -fsS "$APP_URL" >/dev/null 2>&1; then
  echo "[ERROR] App did not respond on $APP_URL."
  exit 1
fi

echo "[4/4] Opening browser..."
open "$APP_URL"

echo
echo "Ready: $APP_URL"
echo "Missing Ollama models are downloaded lazily from the web UI."
