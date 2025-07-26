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

# Code signing
SIGNED=false
echo "Checking for code signing setup..."

# Check for signing certificate credentials
MISSING_SIGNING=""
if [ -z "$APPLE_CERTIFICATE_BASE64" ] && [ -z "$APPLE_CODESIGN_IDENTITY" ]; then
    MISSING_SIGNING="APPLE_CERTIFICATE_BASE64 or APPLE_CODESIGN_IDENTITY"
fi

if [ -n "$APPLE_CERTIFICATE_BASE64" ] && [ -z "$APPLE_CERTIFICATE_PASSWORD" ]; then
    MISSING_SIGNING="${MISSING_SIGNING:+$MISSING_SIGNING, }APPLE_CERTIFICATE_PASSWORD"
fi

if [ -n "$MISSING_SIGNING" ]; then
    echo "⚠️  Code signing skipped - missing: $MISSING_SIGNING"
    echo "   To enable signing, provide the required environment variables"
elif [ -n "$APPLE_CERTIFICATE_BASE64" ]; then
    echo "🔐 Setting up certificate from APPLE_CERTIFICATE_BASE64..."
    
    # Create certificate file
    echo "$APPLE_CERTIFICATE_BASE64" | base64 --decode > certificate.p12
    
    # Create temporary keychain
    security create-keychain -p "temp" build.keychain
    security default-keychain -s build.keychain
    security unlock-keychain -p "temp" build.keychain
    
    # Import certificate
    security import certificate.p12 -k build.keychain -P "$APPLE_CERTIFICATE_PASSWORD" -T /usr/bin/codesign
    security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "temp" build.keychain
    
    # Find signing identity
    APPLE_CODESIGN_IDENTITY=$(security find-identity -v -p codesigning build.keychain | grep "Developer ID Application" | head -1 | grep -o '"[^"]*"' | tr -d '"')
    
    # Clean up certificate file
    rm certificate.p12
    
    if [ -n "$APPLE_CODESIGN_IDENTITY" ]; then
        echo "✅ Certificate imported successfully: $APPLE_CODESIGN_IDENTITY"
    else
        echo "❌ No valid signing identity found in certificate"
        SIGNED=false
    fi
fi

if [ -n "$APPLE_CODESIGN_IDENTITY" ]; then
    echo "🔏 Code signing with identity: $APPLE_CODESIGN_IDENTITY"
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
fi

# Notarization (if requested)
if [ "$NOTARIZE" = true ]; then
    echo "Checking notarization setup..."
    
    if [ "$SIGNED" != true ]; then
        echo "❌ Cannot notarize unsigned binary. Code signing is required for notarization."
        exit 1
    fi
    
    # Check for required notarization credentials
    MISSING_NOTARIZATION=""
    if [ -z "$APPLE_ID" ]; then
        MISSING_NOTARIZATION="APPLE_ID"
    fi
    if [ -z "$APPLE_APP_PASSWORD" ]; then
        MISSING_NOTARIZATION="${MISSING_NOTARIZATION:+$MISSING_NOTARIZATION, }APPLE_APP_PASSWORD"
    fi
    if [ -z "$APPLE_TEAM_ID" ]; then
        MISSING_NOTARIZATION="${MISSING_NOTARIZATION:+$MISSING_NOTARIZATION, }APPLE_TEAM_ID"
    fi
    
    if [ -n "$MISSING_NOTARIZATION" ]; then
        echo "❌ Notarization requested but missing credentials: $MISSING_NOTARIZATION"
        echo "   Please provide all required environment variables for notarization"
        exit 1
    fi
    
    echo "🔔 Starting notarization process..."
    
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