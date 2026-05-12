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
