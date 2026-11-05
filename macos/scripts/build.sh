#!/bin/bash
set -euo pipefail
if [[ "$(uname -s)" != Darwin || "$(uname -m)" != arm64 ]]; then
  echo "Build this preview on an Apple silicon Mac with Xcode 26 or later." >&2
  exit 1
fi
cd "$(dirname "$0")/.."
sdk_version="$(xcrun --sdk macosx --show-sdk-version)"
if [[ "${sdk_version%%.*}" -lt 26 ]]; then
  echo "The macOS 26 SDK or later is required. Select Xcode 26 in xcode-select." >&2
  exit 1
fi
swift test
swift build -c release --product Murmur
binary_dir="$(swift build -c release --show-bin-path)"
bundle="$PWD/.build/bundle/Murmur.app"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources/Licenses"
cp "$binary_dir/Murmur" "$bundle/Contents/MacOS/Murmur"
cp Info.plist "$bundle/Contents/Info.plist"
cp ../LICENSE "$bundle/Contents/Resources/Licenses/Murmur-LICENSE"
cp ../licenses/thinking-orbs-LICENSE "$bundle/Contents/Resources/Licenses/thinking-orbs-LICENSE"
plutil -lint "$bundle/Contents/Info.plist"
if [[ -n "${DEVELOPER_ID:-}" ]]; then
  codesign --force --options runtime --timestamp --entitlements Murmur.entitlements --sign "$DEVELOPER_ID" "$bundle"
else
  codesign --force --entitlements Murmur.entitlements --sign - "$bundle"
fi
codesign --verify --strict --verbose=2 "$bundle"
echo "Built $bundle"
echo "Local build only. Public distribution still requires Developer ID signing and notarization."
