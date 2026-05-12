# OllamaManager

macOS menu-bar app for managing **Ollama**, **MLX LM / VLM** servers and **Open WebUI** from a single interface.

Built with Avalonia 11 + ReactiveUI on .NET 10, native Apple Silicon (arm64).

## Features

- Start / stop Ollama with custom environment variables
- Start / stop MLX LM or VLM server (auto-detected from `config.json`)
- Speculative decoding support via draft model field
- Configurable host and port for each service
- Open WebUI install, start and browser shortcut
- HuggingFace model browser — download, delete, disk size, one-click "Use"
- Ollama model list with pull / delete
- All data stored in `~/Library/Application Support/OllamaManager/`

## Requirements

- macOS 14+ Apple Silicon
- [Ollama](https://ollama.com) (optional)
- Python 3 with `mlx-lm` and/or `mlx-vlm` installed (optional)
- `open-webui` pip package (optional)

## Installation

Download the latest `.dmg` from [Releases](../../releases), open it and drag **OllamaManager** to Applications.

## Build from source

```bash
# requires .NET 10 SDK
bash build.sh
# outputs dist/OllamaManager.app and dist/OllamaManager.dmg
```

## Release

Bump `VERSION`, commit and push — the release workflow builds and publishes automatically.
