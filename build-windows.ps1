# Build script for Windows binaries
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$PROJECT_PATH = "src/ffpbdotnet/ffpbdotnet.csproj"
$OUTPUT_DIR = "dist-windows"

Write-Host "Building Windows binaries for ffpbdotnet..." -ForegroundColor Green

# Clean up previous builds
if (Test-Path $OUTPUT_DIR) {
    Write-Host "Cleaning up previous builds..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OUTPUT_DIR
}
New-Item -ItemType Directory -Path $OUTPUT_DIR -Force | Out-Null

# Build for Windows x64
Write-Host "Building Windows x64 binary..." -ForegroundColor Yellow
dotnet publish $PROJECT_PATH `
    -c $Configuration `
    -r win-x64 `
    --self-contained `
    -o "$OUTPUT_DIR/x64"

# Build for Windows ARM64
Write-Host "Building Windows ARM64 binary..." -ForegroundColor Yellow
dotnet publish $PROJECT_PATH `
    -c $Configuration `
    -r win-arm64 `
    --self-contained `
    -o "$OUTPUT_DIR/arm64"

Write-Host "Windows binaries created:" -ForegroundColor Green
Write-Host "  x64:   $OUTPUT_DIR/x64/ffpb.exe" -ForegroundColor Cyan
Write-Host "  ARM64: $OUTPUT_DIR/arm64/ffpb.exe" -ForegroundColor Cyan

# Show file info
Write-Host ""
Write-Host "x64 binary info:" -ForegroundColor Yellow
Get-ChildItem "$OUTPUT_DIR/x64/ffpb.exe" | Format-Table Name, Length, LastWriteTime

Write-Host "ARM64 binary info:" -ForegroundColor Yellow
Get-ChildItem "$OUTPUT_DIR/arm64/ffpb.exe" | Format-Table Name, Length, LastWriteTime