#!/bin/bash

# run-local.sh - Script to run the Biatec Tokens API locally for development and testing
# This script sets up the environment and starts the API server

set -e

echo "==================================================="
echo "  Biatec Tokens API - Local Development Setup"
echo "==================================================="
echo ""

# Check if .NET SDK is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed."
    echo "Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
echo "✓ .NET SDK version: $DOTNET_VERSION"

# Navigate to the API project directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR/BiatecTokensApi"

echo ""
echo "==================================================="
echo "  Configuration Setup"
echo "==================================================="
echo ""

# Check if user secrets are configured
echo "Checking user secrets configuration..."
echo ""
echo "Important: Make sure you have configured the following user secrets:"
echo "  - App:Account (your Algorand mnemonic for transactions)"
echo ""
echo "To set user secrets, run:"
echo "  dotnet user-secrets set \"App:Account\" \"your-mnemonic-phrase-here\""
echo ""

# Prompt user to continue
read -p "Have you configured the required secrets? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo ""
    echo "Please configure user secrets before running the API."
    echo "You can also set them as environment variables:"
    echo "  export App__Account=\"your-mnemonic-phrase-here\""
    echo ""
    exit 1
fi

echo ""
echo "==================================================="
echo "  Building the API"
echo "==================================================="
echo ""

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore

# Build the project
echo "Building the project..."
dotnet build --configuration Debug

echo ""
echo "==================================================="
echo "  Starting the API"
echo "==================================================="
echo ""

echo "The API will be available at:"
echo "  - HTTPS: https://localhost:7000"
echo "  - HTTP:  http://localhost:5000"
echo ""
echo "  Swagger UI: https://localhost:7000/swagger"
echo "  OpenAPI JSON: https://localhost:7000/swagger/v1/swagger.json"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

# Run the API
dotnet run --configuration Debug --no-build
