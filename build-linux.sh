#!/bin/bash

# Build script for Linux binaries
set -e

PROJECT_PATH="src/ffpbdotnet/ffpbdotnet.csproj"
OUTPUT_DIR="dist-linux"

echo "Building Linux binaries for ffpbdotnet..."

# Clean up previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build for Linux x64
echo "Building Linux x64 binary..."
dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o "$OUTPUT_DIR/x64"

# Build for Linux ARM64
echo "Building Linux ARM64 binary..."
dotnet publish "$PROJECT_PATH" \
    -c Release \
    -r linux-arm64 \
    --self-contained \
    -o "$OUTPUT_DIR/arm64"

# Make binaries executable
chmod +x "$OUTPUT_DIR/x64/ffpb"
chmod +x "$OUTPUT_DIR/arm64/ffpb"

echo "Linux binaries created:"
echo "  x64:   $OUTPUT_DIR/x64/ffpb"
echo "  ARM64: $OUTPUT_DIR/arm64/ffpb"

# Show file info
echo ""
echo "x64 binary info:"
file "$OUTPUT_DIR/x64/ffpb"
echo ""
echo "ARM64 binary info:"
file "$OUTPUT_DIR/arm64/ffpb"