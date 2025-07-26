#!/bin/bash

# Build script for universal macOS binary with optional code signing and notarization
set -e

PROJECT_PATH="src/ffpbdotnet/ffpbdotnet.csproj"
OUTPUT_DIR="dist"
TEMP_DIR="temp"
NOTARIZE=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --notarize)
            NOTARIZE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--notarize]"
            exit 1
            ;;
    esac
done

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

# Code signing (if APPLE_CODESIGN_IDENTITY is set)
SIGNED=false
if [ -n "$APPLE_CODESIGN_IDENTITY" ]; then
    echo "Code signing with identity: $APPLE_CODESIGN_IDENTITY"
    codesign --sign "$APPLE_CODESIGN_IDENTITY" \
             --options runtime \
             --timestamp \
             --verbose \
             "$OUTPUT_DIR/ffpb"
    
    # Verify signature
    echo "Verifying signature..."
    codesign --verify --verbose=2 "$OUTPUT_DIR/ffpb"
    echo "✅ Binary signed successfully"
    SIGNED=true
else
    echo "⚠️  No signing identity found (set APPLE_CODESIGN_IDENTITY to sign)"
fi

# Notarization (if requested and signed)
if [ "$NOTARIZE" = true ]; then
    if [ "$SIGNED" = true ]; then
        if [ -n "$APPLE_ID" ] && [ -n "$APPLE_APP_PASSWORD" ] && [ -n "$APPLE_TEAM_ID" ]; then
            echo "Starting notarization process..."
            
            # Create a zip for notarization
            cd "$OUTPUT_DIR"
            zip -r ../ffpb-notarize.zip ffpb
            cd ..
            
            # Submit for notarization
            echo "Submitting binary for notarization..."
            xcrun notarytool submit ffpb-notarize.zip \
              --apple-id "$APPLE_ID" \
              --password "$APPLE_APP_PASSWORD" \
              --team-id "$APPLE_TEAM_ID" \
              --wait
            
            # Clean up
            rm ffpb-notarize.zip
            echo "✅ Binary notarized successfully"
        else
            echo "❌ Notarization requested but missing credentials (APPLE_ID, APPLE_APP_PASSWORD, APPLE_TEAM_ID)"
            exit 1
        fi
    else
        echo "❌ Cannot notarize unsigned binary. Please sign first."
        exit 1
    fi
elif [ -n "$APPLE_ID" ] && [ -n "$APPLE_APP_PASSWORD" ] && [ -n "$APPLE_TEAM_ID" ]; then
    echo "ℹ️  Notarization credentials available. Use --notarize flag to enable notarization."
fi

# Clean up temp files
rm -rf "$TEMP_DIR"

echo "Universal macOS binary created at: $OUTPUT_DIR/ffpb"
echo "File info:"
file "$OUTPUT_DIR/ffpb"

# Display signature info if signed
if codesign --verify "$OUTPUT_DIR/ffpb" 2>/dev/null; then
    echo "Signature info:"
    codesign --display --verbose=2 "$OUTPUT_DIR/ffpb"
fi