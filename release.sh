#!/bin/zsh
# Builda localmente e pubblica direttamente su GitHub Releases.
# Uso: bash release.sh

set -e

VERSION=$(cat VERSION | tr -d '[:space:]')
TAG="v${VERSION}"

echo "→ Build $TAG..."
bash build.sh

echo "→ Creo release GitHub $TAG..."
gh release create "$TAG" \
    dist/OllamaManager.dmg \
    --title "OllamaManager $TAG" \
    --generate-notes

echo "✓ Release $TAG pubblicata."
