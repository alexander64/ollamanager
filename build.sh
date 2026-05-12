#!/bin/zsh

set -e

APP_NAME="OllamaManager"
VERSION=$(cat VERSION | tr -d '[:space:]')
DIST="dist"
BUNDLE="$DIST/${APP_NAME}.app"
DMG="$DIST/${APP_NAME}.dmg"
RID="osx-arm64"
PUBLISH="publish/$RID"

echo "Build in corso..."
dotnet publish -r $RID -c Release \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=false \
    --output "./$PUBLISH"

echo "Creazione bundle .app..."
rm -rf "$BUNDLE"
mkdir -p "$BUNDLE/Contents/MacOS"
mkdir -p "$BUNDLE/Contents/Resources"

# Eseguibile principale
cp "./$PUBLISH/$APP_NAME" "$BUNDLE/Contents/MacOS/$APP_NAME"
chmod +x "$BUNDLE/Contents/MacOS/$APP_NAME"

# Dylib native (non bundlabili nel single-file su macOS)
for dylib in ./$PUBLISH/*.dylib; do
    [[ -f "$dylib" ]] && cp "$dylib" "$BUNDLE/Contents/MacOS/"
done

# Icona
cp "Assets/AppIcon.icns" "$BUNDLE/Contents/Resources/AppIcon.icns"

cat > "$BUNDLE/Contents/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Ollama Manager</string>
    <key>CFBundleIdentifier</key>
    <string>com.local.ollamamanager</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleExecutable</key>
    <string>OllamaManager</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# ── Python embedded (mlx-lm + mlx-vlm + huggingface-hub) ─────────────────────
# open-webui NON è incluso — viene installato on-demand dall'app stessa
# tramite pip bundled. Aggiorna PBS_DATE/PBS_PY_VER se necessario.

PBS_PY_VER="3.11.12"
PBS_DATE="20250409"
PBS_TAG="cpython-${PBS_PY_VER}+${PBS_DATE}-aarch64-apple-darwin-install_only"
PBS_URL="https://github.com/astral-sh/python-build-standalone/releases/download/${PBS_DATE}/${PBS_TAG}.tar.gz"
PBS_CACHE_DIR=".build_cache"
PBS_CACHE="${PBS_CACHE_DIR}/${PBS_TAG}.tar.gz"
PYTHON_RES="$BUNDLE/Contents/Resources/python"

echo "Python embedded setup..."
mkdir -p "$PBS_CACHE_DIR"

if [ ! -f "$PBS_CACHE" ]; then
    echo "  Download Python ${PBS_PY_VER} standalone (~80MB, una tantum)..."
    curl -L --progress-bar -o "$PBS_CACHE" "$PBS_URL"
fi

echo "  Estrazione Python..."
rm -rf "$PYTHON_RES"
mkdir -p "$PYTHON_RES"
tar -xzf "$PBS_CACHE" -C "$PYTHON_RES" --strip-components=1

echo "  Installazione pacchetti (mlx-lm, mlx-vlm, huggingface-hub)..."
"$PYTHON_RES/bin/python3" -m pip install --upgrade pip --quiet
"$PYTHON_RES/bin/python3" -m pip install \
    mlx-lm \
    mlx-vlm \
    "huggingface_hub[hf_transfer]" \
    --quiet

echo "  Patching shebang entry points..."
for script in "$PYTHON_RES/bin"/*; do
    [ -f "$script" ] && [ -x "$script" ] || continue
    first=$(head -1 "$script" 2>/dev/null || true)
    [[ "$first" == *python* ]] || continue
    # Sostituisce il path assoluto con env-based così funziona su qualsiasi macchina
    content=$(tail -n +2 "$script")
    printf '#!/usr/bin/env python3\n%s' "$content" > "$script"
    chmod +x "$script"
done

echo "  Python bundled pronto."
# ─────────────────────────────────────────────────────────────────────────────

echo "Creazione DMG..."
rm -f "$DMG"
STAGING=$(mktemp -d)
cp -R "$BUNDLE" "$STAGING/"
ln -s /Applications "$STAGING/Applications"
hdiutil create \
    -volname "Ollama Manager" \
    -srcfolder "$STAGING" \
    -ov -format UDZO \
    "$DMG"
rm -rf "$STAGING"

echo ""
echo "✓ $BUNDLE"
echo "✓ $DMG"
