#!/usr/bin/env bash
#
# Builds the InstaCropper macOS drag-and-drop app (InstaCropper.app).
#
# The app is an AppleScript droplet (compiled with osacompile) that bundles a
# self-contained .NET binary for both Apple Silicon and Intel. Dropping images
# onto it asks for the aspect ratio / background color and then runs the binary.
#
# Requires macOS (osacompile, PlistBuddy) and the .NET 8 SDK.
#
# Usage: macos/build-macos-app.sh <version> [output-dir]
#   <version>     e.g. 1.2.3 or 1.2.4-dev.abc1234 (no leading "v")
#   [output-dir]  where InstaCropper.app is written (default: artifacts)

set -euo pipefail

VERSION="${1:?usage: build-macos-app.sh <version> [output-dir]}"
OUT_DIR="${2:-artifacts}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_NAME="InstaCropper"
APP="${OUT_DIR}/${APP_NAME}.app"
# Version stripped of any prerelease suffix, for the numeric assembly version.
NUMERIC_VERSION="${VERSION%%-*}"

rm -rf "$APP"
mkdir -p "$OUT_DIR"

echo "==> Compiling AppleScript droplet"
osacompile -o "$APP" "${ROOT}/macos/InstaCropper.applescript"

for RID in osx-arm64 osx-x64; do
	echo "==> Publishing .NET binary for ${RID}"
	dotnet publish "${ROOT}/InstaCropper/InstaCropper.csproj" \
		-c Release -r "$RID" --self-contained true \
		-p:PublishSingleFile=true \
		-p:Version="$NUMERIC_VERSION" \
		-p:InformationalVersion="$VERSION" \
		-o "${APP}/Contents/Resources/bin/${RID}"
	chmod +x "${APP}/Contents/Resources/bin/${RID}/${APP_NAME}"
done

echo "==> Patching Info.plist"
PLIST="${APP}/Contents/Info.plist"
plist_set() { /usr/libexec/PlistBuddy -c "Set ${1} ${2}" "$PLIST" 2>/dev/null; }
plist_add() { /usr/libexec/PlistBuddy -c "Add ${1} ${2} ${3:-}" "$PLIST" 2>/dev/null; }

plist_set ":CFBundleName" "$APP_NAME" || true
plist_add ":CFBundleShortVersionString" string "$VERSION" || plist_set ":CFBundleShortVersionString" "$VERSION" || true
plist_add ":CFBundleVersion" string "$NUMERIC_VERSION" || plist_set ":CFBundleVersion" "$NUMERIC_VERSION" || true
plist_set ":CFBundleIdentifier" "de.synthscript.instacropper" || true

# Declare that the droplet accepts image files (so Finder enables the drop).
plist_add ":CFBundleDocumentTypes" array || true
plist_add ":CFBundleDocumentTypes:0" dict || true
plist_add ":CFBundleDocumentTypes:0:CFBundleTypeName" string "Image" || true
plist_add ":CFBundleDocumentTypes:0:CFBundleTypeRole" string "Viewer" || true
plist_add ":CFBundleDocumentTypes:0:LSItemContentTypes" array || true
plist_add ":CFBundleDocumentTypes:0:LSItemContentTypes:0" string "public.image" || true

echo "==> Built ${APP} (version ${VERSION})"
