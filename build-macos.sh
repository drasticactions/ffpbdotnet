#!/bin/bash

# Build script for universal macOS binary
set -e

PROJECT_PATH="src/ffpbdotnet/ffpbdotnet.csproj"
OUTPUT_DIR="dist"
TEMP_DIR="temp"

echo "Building universal macOS binary for ffpbdotnet..."

# Clean up previous builds
rm -rf "$OUTPUT_DIR" "$TEMP_DIR"
mkdir -p "$OUTPUT_DIR" "$TEMP_DIR"

# Build for ARM64 (Apple Silicon)
echo "Building ARM64 binary..."
dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r osx-arm64 \
    --self-contained \
    -o "$TEMP_DIR/arm64"

# Build for x64 (Intel)
echo "Building x64 binary..."
dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r osx-x64 \
    --self-contained \
    -o "$TEMP_DIR/x64"

# Create universal binary using lipo
echo "Creating universal binary..."
lipo -create \
    "$TEMP_DIR/arm64/ffpb" \
    "$TEMP_DIR/x64/ffpb" \
    -output "$OUTPUT_DIR/ffpb"

# Make it executable
chmod +x "$OUTPUT_DIR/ffpb"

# Clean up temp files
rm -rf "$TEMP_DIR"

echo "Universal macOS binary created at: $OUTPUT_DIR/ffpb"
echo "File info:"
file "$OUTPUT_DIR/ffpb"