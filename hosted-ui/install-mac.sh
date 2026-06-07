#!/usr/bin/env bash
set -euo pipefail

ARCHIVE_URL="${1:-}"
RUNTIME="${2:-}"

if [ -z "$ARCHIVE_URL" ] || [ -z "$RUNTIME" ]; then
  echo "Usage: install-mac.sh <archive-url> <osx-arm64|osx-x64>"
  exit 1
fi

case "$RUNTIME" in
  osx-arm64|osx-x64) ;;
  *)
    echo "Unsupported runtime: $RUNTIME"
    exit 1
    ;;
esac

ARCHIVE_NAME="MultimodalUIAnalyzer-$RUNTIME.zip"
TARGET_ROOT="$HOME/Downloads/MultimodalUIAnalyzer-$RUNTIME"
ARCHIVE_PATH="$HOME/Downloads/$ARCHIVE_NAME"

echo "Multimodal UI Analyzer macOS setup"
echo "Runtime: $RUNTIME"
echo

if ! command -v ollama >/dev/null 2>&1; then
  echo "==> Installing Ollama"
  if command -v brew >/dev/null 2>&1; then
    brew install ollama
  else
    echo "Homebrew was not found."
    echo "Install Homebrew first or install Ollama manually from https://ollama.com/download"
    if command -v open >/dev/null 2>&1; then
      open "https://ollama.com/download"
    fi
    exit 1
  fi
else
  echo "==> Ollama is already installed"
fi

if ! curl -fsS "http://localhost:11434/api/tags" >/dev/null 2>&1; then
  echo "==> Starting Ollama"
  if command -v ollama >/dev/null 2>&1; then
    nohup ollama serve > "$HOME/Downloads/ollama.log" 2>&1 &
    sleep 2
  fi
fi

echo "==> Downloading package"
curl -fL --retry 3 "$ARCHIVE_URL" -o "$ARCHIVE_PATH"

echo "==> Extracting package"
rm -rf "$TARGET_ROOT"
mkdir -p "$TARGET_ROOT"
unzip -q "$ARCHIVE_PATH" -d "$TARGET_ROOT"

echo "==> Removing macOS quarantine attributes"
if command -v xattr >/dev/null 2>&1; then
  xattr -dr com.apple.quarantine "$TARGET_ROOT" >/dev/null 2>&1 || true
fi

echo "==> Preparing launcher"
chmod +x "$TARGET_ROOT/MultimodalUIAnalyzer" 2>/dev/null || true
chmod +x "$TARGET_ROOT/start-macos.sh" 2>/dev/null || true
chmod +x "$TARGET_ROOT/start-macos.command" 2>/dev/null || true

echo "==> Starting Multimodal UI Analyzer"
cd "$TARGET_ROOT"
./start-macos.command
