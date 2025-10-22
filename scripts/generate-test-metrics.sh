#!/bin/bash

# Script to generate test metrics reports from test execution
# Usage: ./scripts/generate-test-metrics.sh [output-format] [output-path]
#
# Formats: text, json, github
# Example: ./scripts/generate-test-metrics.sh json ./test-metrics.json

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
FORMAT="${1:-text}"
OUTPUT_PATH="${2:-./test-metrics-report.txt}"

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Test Metrics Report Generator${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo ""

# Validate format
case "$FORMAT" in
    text|json|github)
        echo -e "${GREEN}✓${NC} Format: $FORMAT"
        ;;
    *)
        echo -e "${RED}✗${NC} Invalid format: $FORMAT"
        echo "  Valid formats: text, json, github"
        exit 1
        ;;
esac

echo -e "${GREEN}✓${NC} Output: $OUTPUT_PATH"
echo ""

# Set environment variables for test execution
export TEST_METRICS_EXPORT_PATH="$OUTPUT_PATH"
export TEST_METRICS_FORMAT="$FORMAT"

echo -e "${YELLOW}Running tests with metrics collection...${NC}"
echo ""

# Run unit tests
echo -e "${BLUE}Running Unit Tests...${NC}"
dotnet test --filter "Category=Unit" \
    --logger "console;verbosity=minimal" \
    --results-directory ./TestResults/Metrics/Unit \
    || true

echo ""

# Run integration tests
echo -e "${BLUE}Running Integration Tests...${NC}"
dotnet test Oproto.FluentDynamoDb.IntegrationTests \
    --logger "console;verbosity=minimal" \
    --results-directory ./TestResults/Metrics/Integration \
    || true

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"

# Check if report was generated
if [ -f "$OUTPUT_PATH" ]; then
    echo -e "${GREEN}✓${NC} Metrics report generated successfully"
    echo -e "  Location: ${BLUE}$OUTPUT_PATH${NC}"
    echo ""
    
    # Show file size
    FILE_SIZE=$(du -h "$OUTPUT_PATH" | cut -f1)
    echo -e "  Size: $FILE_SIZE"
    
    # For text format, show a preview
    if [ "$FORMAT" = "text" ]; then
        echo ""
        echo -e "${YELLOW}Report Preview:${NC}"
        echo -e "${BLUE}───────────────────────────────────────────────────────────${NC}"
        head -n 30 "$OUTPUT_PATH"
        
        TOTAL_LINES=$(wc -l < "$OUTPUT_PATH")
        if [ "$TOTAL_LINES" -gt 30 ]; then
            echo ""
            echo -e "${YELLOW}... (showing first 30 lines of $TOTAL_LINES total)${NC}"
        fi
        echo -e "${BLUE}───────────────────────────────────────────────────────────${NC}"
    fi
    
    # For JSON format, validate it
    if [ "$FORMAT" = "json" ]; then
        if command -v jq &> /dev/null; then
            echo ""
            echo -e "${YELLOW}Validating JSON...${NC}"
            if jq empty "$OUTPUT_PATH" 2>/dev/null; then
                echo -e "${GREEN}✓${NC} Valid JSON"
                
                # Show summary from JSON
                echo ""
                echo -e "${YELLOW}Summary:${NC}"
                jq -r '"  Total Tests: \(.totalTests)\n  Passed: \(.passedTests)\n  Failed: \(.failedTests)\n  Duration: \(.totalDurationMs)ms"' "$OUTPUT_PATH"
            else
                echo -e "${RED}✗${NC} Invalid JSON"
            fi
        fi
    fi
else
    echo -e "${RED}✗${NC} Failed to generate metrics report"
    echo "  Expected location: $OUTPUT_PATH"
    exit 1
fi

echo ""
echo -e "${GREEN}Done!${NC}"
echo ""
