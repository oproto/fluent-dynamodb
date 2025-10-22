#!/bin/bash

# Setup script for DynamoDB Local
# Downloads and extracts DynamoDB Local if not already present
# Verifies Java installation

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
DYNAMODB_LOCAL_DIR="./dynamodb-local"
DYNAMODB_LOCAL_URL="https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.tar.gz"
DYNAMODB_LOCAL_ARCHIVE="dynamodb_local_latest.tar.gz"

echo "========================================="
echo "DynamoDB Local Setup Script"
echo "========================================="
echo ""

# Check if Java is installed
echo "Checking Java installation..."
if ! command -v java &> /dev/null; then
    echo -e "${RED}ERROR: Java is not installed or not in PATH${NC}"
    echo ""
    echo "DynamoDB Local requires Java Runtime Environment (JRE) version 8.x or newer."
    echo ""
    echo "Installation instructions:"
    echo "  - macOS: brew install openjdk@17"
    echo "  - Ubuntu/Debian: sudo apt-get install openjdk-17-jre"
    echo "  - Windows: Download from https://adoptium.net/"
    echo ""
    exit 1
fi

# Get Java version
JAVA_VERSION=$(java -version 2>&1 | head -n 1 | cut -d'"' -f2 | cut -d'.' -f1)
echo -e "${GREEN}✓ Java is installed (version: $(java -version 2>&1 | head -n 1))${NC}"
echo ""

# Check if DynamoDB Local is already installed
if [ -d "$DYNAMODB_LOCAL_DIR" ] && [ -f "$DYNAMODB_LOCAL_DIR/DynamoDBLocal.jar" ]; then
    echo -e "${GREEN}✓ DynamoDB Local is already installed at $DYNAMODB_LOCAL_DIR${NC}"
    echo ""
    echo "To reinstall, delete the directory and run this script again:"
    echo "  rm -rf $DYNAMODB_LOCAL_DIR"
    echo ""
    exit 0
fi

# Create directory for DynamoDB Local
echo "Creating directory: $DYNAMODB_LOCAL_DIR"
mkdir -p "$DYNAMODB_LOCAL_DIR"

# Download DynamoDB Local
echo "Downloading DynamoDB Local..."
echo "URL: $DYNAMODB_LOCAL_URL"
echo ""

if command -v curl &> /dev/null; then
    curl -L -o "$DYNAMODB_LOCAL_ARCHIVE" "$DYNAMODB_LOCAL_URL"
elif command -v wget &> /dev/null; then
    wget -O "$DYNAMODB_LOCAL_ARCHIVE" "$DYNAMODB_LOCAL_URL"
else
    echo -e "${RED}ERROR: Neither curl nor wget is available${NC}"
    echo "Please install curl or wget and try again."
    exit 1
fi

echo -e "${GREEN}✓ Download complete${NC}"
echo ""

# Extract DynamoDB Local
echo "Extracting DynamoDB Local to $DYNAMODB_LOCAL_DIR..."
tar -xzf "$DYNAMODB_LOCAL_ARCHIVE" -C "$DYNAMODB_LOCAL_DIR"

# Clean up archive
rm "$DYNAMODB_LOCAL_ARCHIVE"

echo -e "${GREEN}✓ Extraction complete${NC}"
echo ""

# Verify installation
if [ -f "$DYNAMODB_LOCAL_DIR/DynamoDBLocal.jar" ]; then
    echo -e "${GREEN}=========================================${NC}"
    echo -e "${GREEN}✓ DynamoDB Local setup complete!${NC}"
    echo -e "${GREEN}=========================================${NC}"
    echo ""
    echo "DynamoDB Local is installed at: $DYNAMODB_LOCAL_DIR"
    echo ""
    echo "To start DynamoDB Local manually:"
    echo "  cd $DYNAMODB_LOCAL_DIR"
    echo "  java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -inMemory -port 8000"
    echo ""
    echo "Or use the run-integration-tests.sh script to run tests with DynamoDB Local."
    echo ""
else
    echo -e "${RED}ERROR: Installation verification failed${NC}"
    echo "DynamoDBLocal.jar not found in $DYNAMODB_LOCAL_DIR"
    exit 1
fi
