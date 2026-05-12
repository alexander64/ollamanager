#!/bin/zsh
# Builda localmente e pubblica direttamente su GitHub Releases.
# Uso: bash release.sh

set -e

VERSION=$(cat VERSION | tr -d '[:space:]')
TAG="v${VERSION}"

echo "→ Build $TAG..."
bash build.sh

echo "→ Zip .app..."
cd dist
zip -r OllamaManager.app.zip OllamaManager.app
cd ..

echo "→ Creo release GitHub $TAG..."
gh release create "$TAG" \
    dist/OllamaManager.dmg \
    dist/OllamaManager.app.zip \
    --title "OllamaManager $TAG" \
    --generate-notes

echo "✓ Release $TAG pubblicata."
