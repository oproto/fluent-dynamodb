#!/bin/bash

# Script to generate code coverage reports
# Usage: ./scripts/generate-coverage-report.sh [format]
#
# Formats: html, json, cobertura, lcov, all (default)
# Example: ./scripts/generate-coverage-report.sh html

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
FORMAT="${1:-all}"
COVERAGE_DIR="./TestResults/Coverage"
REPORT_DIR="./TestResults/CoverageReport"

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Code Coverage Report Generator${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo ""

# Clean previous results
if [ -d "$COVERAGE_DIR" ]; then
    echo -e "${YELLOW}Cleaning previous coverage data...${NC}"
    rm -rf "$COVERAGE_DIR"
fi

if [ -d "$REPORT_DIR" ]; then
    rm -rf "$REPORT_DIR"
fi

mkdir -p "$COVERAGE_DIR"
mkdir -p "$REPORT_DIR"

echo -e "${GREEN}✓${NC} Directories prepared"
echo ""

# Run tests with coverage
echo -e "${BLUE}Running tests with coverage collection...${NC}"
echo ""

echo -e "${YELLOW}Unit Tests:${NC}"
dotnet test \
    --configuration Release \
    --filter "Category=Unit" \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COVERAGE_DIR/Unit" \
    --settings coverlet.runsettings \
    --verbosity minimal \
    || true

echo ""
echo -e "${YELLOW}Integration Tests:${NC}"
dotnet test Oproto.FluentDynamoDb.IntegrationTests \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory "$COVERAGE_DIR/Integration" \
    --settings coverlet.runsettings \
    --verbosity minimal \
    || true

echo ""
echo -e "${GREEN}✓${NC} Test execution completed"
echo ""

# Find coverage files
echo -e "${BLUE}Locating coverage files...${NC}"
COVERAGE_FILES=$(find "$COVERAGE_DIR" -name "coverage.cobertura.xml" -o -name "coverage.json")

if [ -z "$COVERAGE_FILES" ]; then
    echo -e "${RED}✗${NC} No coverage files found"
    echo "  Expected location: $COVERAGE_DIR"
    exit 1
fi

echo -e "${GREEN}✓${NC} Found coverage data"
echo ""

# Check if reportgenerator is installed
if ! command -v reportgenerator &> /dev/null; then
    echo -e "${YELLOW}Installing ReportGenerator tool...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool || dotnet tool update --global dotnet-reportgenerator-globaltool
    echo -e "${GREEN}✓${NC} ReportGenerator installed"
    echo ""
fi

# Generate reports
echo -e "${BLUE}Generating coverage reports...${NC}"
echo ""

# Determine report types based on format
case "$FORMAT" in
    html)
        REPORT_TYPES="Html"
        ;;
    json)
        REPORT_TYPES="JsonSummary"
        ;;
    cobertura)
        REPORT_TYPES="Cobertura"
        ;;
    lcov)
        REPORT_TYPES="lcov"
        ;;
    all)
        REPORT_TYPES="Html;JsonSummary;Cobertura;Badges;TextSummary"
        ;;
    *)
        echo -e "${RED}✗${NC} Invalid format: $FORMAT"
        echo "  Valid formats: html, json, cobertura, lcov, all"
        exit 1
        ;;
esac

# Generate the report
reportgenerator \
    "-reports:$COVERAGE_DIR/**/coverage.cobertura.xml" \
    "-targetdir:$REPORT_DIR" \
    "-reporttypes:$REPORT_TYPES" \
    "-verbosity:Info" \
    "-title:Oproto.FluentDynamoDb Coverage Report" \
    "-tag:$(git rev-parse --short HEAD 2>/dev/null || echo 'local')" \
    "-historydir:$REPORT_DIR/history"

echo ""
echo -e "${GREEN}✓${NC} Reports generated successfully"
echo ""

# Display summary
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Coverage Summary${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo ""

# Parse and display summary from JSON if available
SUMMARY_FILE="$REPORT_DIR/Summary.json"
if [ -f "$SUMMARY_FILE" ]; then
    if command -v jq &> /dev/null; then
        echo -e "${YELLOW}Overall Coverage:${NC}"
        
        LINE_COVERAGE=$(jq -r '.summary.linecoverage' "$SUMMARY_FILE" 2>/dev/null || echo "N/A")
        BRANCH_COVERAGE=$(jq -r '.summary.branchcoverage' "$SUMMARY_FILE" 2>/dev/null || echo "N/A")
        
        echo "  Line Coverage:   $LINE_COVERAGE%"
        echo "  Branch Coverage: $BRANCH_COVERAGE%"
        echo ""
        
        # Show coverage by assembly
        echo -e "${YELLOW}Coverage by Assembly:${NC}"
        jq -r '.coverage[] | "  \(.name): \(.linecoverage)% lines, \(.branchcoverage)% branches"' "$SUMMARY_FILE" 2>/dev/null || echo "  Unable to parse assembly data"
        echo ""
    else
        echo -e "${YELLOW}Install 'jq' to see detailed summary${NC}"
        echo ""
    fi
fi

# Show text summary if available
TEXT_SUMMARY="$REPORT_DIR/Summary.txt"
if [ -f "$TEXT_SUMMARY" ]; then
    cat "$TEXT_SUMMARY"
    echo ""
fi

# Show report locations
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}  Report Locations${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo ""

if [ -f "$REPORT_DIR/index.html" ]; then
    echo -e "${GREEN}✓${NC} HTML Report:"
    echo -e "  ${BLUE}file://$(pwd)/$REPORT_DIR/index.html${NC}"
    echo ""
fi

if [ -f "$REPORT_DIR/Summary.json" ]; then
    echo -e "${GREEN}✓${NC} JSON Summary:"
    echo "  $REPORT_DIR/Summary.json"
fi

if [ -f "$REPORT_DIR/Cobertura.xml" ]; then
    echo -e "${GREEN}✓${NC} Cobertura XML:"
    echo "  $REPORT_DIR/Cobertura.xml"
fi

if [ -d "$REPORT_DIR/badges" ]; then
    echo -e "${GREEN}✓${NC} Coverage Badges:"
    echo "  $REPORT_DIR/badges/"
fi

echo ""

# Check coverage thresholds
if [ -f "$SUMMARY_FILE" ] && command -v jq &> /dev/null; then
    LINE_COVERAGE_NUM=$(jq -r '.summary.linecoverage' "$SUMMARY_FILE" 2>/dev/null | sed 's/%//')
    
    if [ ! -z "$LINE_COVERAGE_NUM" ] && [ "$LINE_COVERAGE_NUM" != "N/A" ]; then
        THRESHOLD=70
        
        echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
        echo -e "${BLUE}  Coverage Threshold Check${NC}"
        echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
        echo ""
        echo "  Threshold: ${THRESHOLD}%"
        echo "  Actual:    ${LINE_COVERAGE_NUM}%"
        echo ""
        
        if (( $(echo "$LINE_COVERAGE_NUM >= $THRESHOLD" | bc -l) )); then
            echo -e "${GREEN}✓ Coverage meets threshold${NC}"
        else
            echo -e "${YELLOW}⚠ Coverage below threshold${NC}"
            DEFICIT=$(echo "$THRESHOLD - $LINE_COVERAGE_NUM" | bc)
            echo "  Need ${DEFICIT}% more coverage"
        fi
        echo ""
    fi
fi

echo -e "${GREEN}Done!${NC}"
echo ""

# Open HTML report if on macOS
if [ "$FORMAT" = "html" ] || [ "$FORMAT" = "all" ]; then
    if [ -f "$REPORT_DIR/index.html" ]; then
        if [[ "$OSTYPE" == "darwin"* ]]; then
            echo -e "${YELLOW}Opening HTML report in browser...${NC}"
            open "$REPORT_DIR/index.html"
        fi
    fi
fi
