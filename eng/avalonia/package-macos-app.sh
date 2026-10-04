#!/usr/bin/env bash
set -euo pipefail

publish_directory=${1:-}
output_archive=${2:-}
bundle_version=${3:-}
display_version=${4:-}

if [[ -z "$publish_directory" || -z "$output_archive" || -z "$bundle_version" || -z "$display_version" ]]; then
    echo "usage: package-macos-app.sh <publish-directory> <output-archive> <bundle-version> <display-version>" >&2
    echo "  <output-archive> ending in .dmg produces a disk image; any other name produces a zip." >&2
    echo "  CODESIGN_IDENTITY selects the signing identity (default: ad-hoc '-')." >&2
    exit 2
fi

for tool in ditto mktemp plutil sips iconutil codesign hdiutil; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "error: required command '$tool' is not installed" >&2
        exit 1
    fi
done

if [[ ! -x "$publish_directory/GitExtensions.Avalonia" ]]; then
    echo "error: publish directory does not contain the GitExtensions.Avalonia executable" >&2
    exit 1
fi

if [[ ! "$bundle_version" =~ ^[0-9]+([.][0-9]+)*$ ]]; then
    echo "error: bundle version must contain only decimal components" >&2
    exit 2
fi

work_directory=$(mktemp -d)
cleanup()
{
    rm -rf -- "$work_directory"
}
trap cleanup EXIT

app_directory="$work_directory/Git Extensions Avalonia.app"
contents_directory="$app_directory/Contents"
macos_directory="$contents_directory/MacOS"
mkdir -p "$macos_directory" "$contents_directory/Resources"
cp -a "$publish_directory/." "$macos_directory/"
chmod +x "$macos_directory/GitExtensions.Avalonia"

script_directory=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
logo_directory="$script_directory/../../setup/assets/Logo"
iconset_directory="$work_directory/GitExtensions.iconset"
mkdir -p "$iconset_directory"
# The largest shipped logo is 512px, so icon_512x512@2x is omitted rather than upscaled.
for size in 16 32 64 128 256 512; do
    cp "$logo_directory/git-extensions-logo-${size}px.png" "$iconset_directory/src-$size.png"
done
for entry in "16 icon_16x16" "32 icon_16x16@2x" "32 icon_32x32" "64 icon_32x32@2x" \
             "128 icon_128x128" "256 icon_128x128@2x" "256 icon_256x256" "512 icon_256x256@2x" "512 icon_512x512"; do
    read -r size name <<<"$entry"
    cp "$iconset_directory/src-$size.png" "$iconset_directory/$name.png"
done
rm -f "$iconset_directory"/src-*.png
iconutil -c icns "$iconset_directory" -o "$contents_directory/Resources/GitExtensions.icns"

cat > "$contents_directory/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDisplayName</key>
  <string>Git Extensions Avalonia</string>
  <key>CFBundleExecutable</key>
  <string>GitExtensions.Avalonia</string>
  <key>CFBundleIconFile</key>
  <string>GitExtensions</string>
  <key>CFBundleIdentifier</key>
  <string>com.github.gitextensions.GitExtensions.Avalonia</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>Git Extensions Avalonia</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$display_version</string>
  <key>CFBundleVersion</key>
  <string>$bundle_version</string>
  <key>LSMinimumSystemVersion</key>
  <string>10.15</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

plutil -lint "$contents_directory/Info.plist"

# Ad-hoc signing (the default) lets the bundle launch on Apple Silicon; it is not notarized.
codesign --force --deep --sign "${CODESIGN_IDENTITY:--}" "$app_directory"

mkdir -p "$(dirname "$output_archive")"
rm -f -- "$output_archive"
if [[ "$output_archive" == *.dmg ]]; then
    dmg_directory="$work_directory/dmg"
    mkdir -p "$dmg_directory"
    cp -a "$app_directory" "$dmg_directory/"
    ln -s /Applications "$dmg_directory/Applications"
    hdiutil create -volname "Git Extensions Avalonia" -srcfolder "$dmg_directory" \
        -fs HFS+ -format UDZO -ov "$output_archive"
else
    ditto -c -k --sequesterRsrc --keepParent "$app_directory" "$output_archive"
fi
