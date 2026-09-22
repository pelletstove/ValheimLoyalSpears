#!/bin/bash
#
# Builds a Thunderstore/r2modman-compatible mod package from the compiled DLL.
# Creates a zip with structure: BepInEx/plugins/modname.dll + manifest.json + README.md + icon.png
#
# Usage: pack_mod.sh <output_path> <dll_path> <project_dir> <mod_name> <version> <website> <description> <dependencies>

set -euo pipefail

OUTPUT_PATH="$1"
DLL_PATH="$2"
PROJECT_DIR="$3"
MOD_NAME="$4"
MOD_VERSION="$5"
MOD_WEBSITE="$6"
MOD_DESCRIPTION="$7"
MOD_DEPENDENCIES="$8"

# Create staging directory
STAGING_DIR="${OUTPUT_PATH}staging"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR/BepInEx/plugins"

# Copy plugin DLL
cp "$DLL_PATH" "${STAGING_DIR}/BepInEx/plugins/${MOD_NAME}.dll"

# Generate dependencies array entries
DEPS_ENTRIES=""
IFS=',' read -ra DEPS <<< "$MOD_DEPENDENCIES"
FIRST=true
for dep in "${DEPS[@]}"; do
    if [ "$FIRST" = true ]; then
        DEPS_ENTRIES="    \"${dep}\""
        FIRST=false
    else
        DEPS_ENTRIES+=$',\n    \"'
        DEPS_ENTRIES+="${dep}"
        DEPS_ENTRIES+='"'
    fi
done

# Write manifest.json
printf '{\n  "name": "%s",\n  "version_number": "%s",\n  "website_url": "%s",\n  "description": "%s",\n  "dependencies": [\n%s\n  ]\n}\n' \
    "$MOD_NAME" "$MOD_VERSION" "$MOD_WEBSITE" "$MOD_DESCRIPTION" "$DEPS_ENTRIES" > "${STAGING_DIR}/manifest.json"

# Copy README.md if it exists
if [ -f "${PROJECT_DIR}/README.md" ]; then
    cp "${PROJECT_DIR}/README.md" "$STAGING_DIR/README.md"
fi

# Copy icon.png if it exists
if [ -f "${PROJECT_DIR}/icon.png" ]; then
    cp "${PROJECT_DIR}/icon.png" "$STAGING_DIR/icon.png"
fi

# Create zip
cd "$STAGING_DIR"
zip -r "../${MOD_NAME}.zip" . > /dev/null
cd ..
rm -rf staging

echo "Created ${MOD_NAME}.zip package at ${OUTPUT_PATH}${MOD_NAME}.zip"
