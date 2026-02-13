#!/bin/bash
# Build script with GitVersion integration

set -e

CONFIGURATION="${1:-Release}"

echo "Gmail MCP Build Script"
echo "====================="
echo ""

# Get version from GitVersion
echo "Determining version with GitVersion..."
VERSION=$(dotnet-gitversion /showvariable FullSemVer)
echo "Version: $VERSION"
echo ""

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore src/GmailMcp/GmailMcp.csproj
echo ""

# Build
echo "Building project..."
dotnet build src/GmailMcp/GmailMcp.csproj --configuration "$CONFIGURATION" --no-restore /p:Version="$VERSION"
echo ""

# Pack if --pack flag is provided
if [[ "$*" == *"--pack"* ]]; then
    echo "Packing NuGet package..."
    dotnet pack src/GmailMcp/GmailMcp.csproj --configuration "$CONFIGURATION" --no-build --output ./packages /p:PackageVersion="$VERSION"
    echo ""
    echo "Package created:"
    ls -1 ./packages/*.nupkg
fi

echo ""
echo "Build completed successfully!"
echo "Version: $VERSION"
