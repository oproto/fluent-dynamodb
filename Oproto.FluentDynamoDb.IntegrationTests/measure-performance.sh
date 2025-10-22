#!/bin/bash

# Script to measure integration test performance
# Usage: ./measure-performance.sh

set -e

echo "=== Integration Test Performance Measurement ==="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Target performance: 30 seconds for full suite
TARGET_SECONDS=30

echo "Building project..."
dotnet build --configuration Release --no-restore > /dev/null 2>&1

echo "Running integration tests..."
echo ""

# Run tests and capture timing
START_TIME=$(date +%s)

dotnet test \
    --configuration Release \
    --no-build \
    --filter "Category=Integration" \
    --logger "console;verbosity=normal" \
    --verbosity quiet

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo "=== Performance Results ==="
echo ""
echo "Total execution time: ${DURATION} seconds"
echo "Target time: ${TARGET_SECONDS} seconds"
echo ""

# Check if we met the target
if [ $DURATION -le $TARGET_SECONDS ]; then
    echo -e "${GREEN}✓ Performance target met!${NC}"
    echo "Tests completed ${$((TARGET_SECONDS - DURATION))} seconds under target."
    exit 0
else
    echo -e "${YELLOW}⚠ Performance target not met${NC}"
    echo "Tests took $((DURATION - TARGET_SECONDS)) seconds longer than target."
    echo ""
    echo "Suggestions:"
    echo "  - Ensure DynamoDB Local is already running"
    echo "  - Check for slow tests in the output above"
    echo "  - Consider running with parallel execution enabled"
    exit 1
fi
