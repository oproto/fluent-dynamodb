#!/bin/bash

# Generate Test Report Script
# This script analyzes test results and generates a comprehensive report

set -e

RESULTS_DIR="${1:-./TestResults}"
OUTPUT_FILE="${2:-test-report.md}"

echo "Generating test report from: $RESULTS_DIR"
echo "Output file: $OUTPUT_FILE"

# Initialize report
cat > "$OUTPUT_FILE" << 'EOF'
# Test Results Report

## Summary

EOF

# Count test result files
unit_files=$(find "$RESULTS_DIR/Unit" -name "*.trx" 2>/dev/null | wc -l || echo "0")
integration_files=$(find "$RESULTS_DIR/Integration" -name "*.trx" 2>/dev/null | wc -l || echo "0")

echo "- Unit Test Result Files: $unit_files" >> "$OUTPUT_FILE"
echo "- Integration Test Result Files: $integration_files" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"

# Parse TRX files for test counts (basic parsing)
parse_trx_summary() {
    local trx_file="$1"
    local test_type="$2"
    
    if [ -f "$trx_file" ]; then
        # Extract test counts from TRX XML
        # This is a simplified parser - real implementation would use proper XML parsing
        local total=$(grep -o 'total="[0-9]*"' "$trx_file" | head -1 | grep -o '[0-9]*' || echo "0")
        local passed=$(grep -o 'passed="[0-9]*"' "$trx_file" | head -1 | grep -o '[0-9]*' || echo "0")
        local failed=$(grep -o 'failed="[0-9]*"' "$trx_file" | head -1 | grep -o '[0-9]*' || echo "0")
        
        echo "### $test_type" >> "$OUTPUT_FILE"
        echo "" >> "$OUTPUT_FILE"
        echo "- Total: $total" >> "$OUTPUT_FILE"
        echo "- Passed: ✅ $passed" >> "$OUTPUT_FILE"
        echo "- Failed: ❌ $failed" >> "$OUTPUT_FILE"
        echo "" >> "$OUTPUT_FILE"
    fi
}

# Process unit test results
if [ -d "$RESULTS_DIR/Unit" ]; then
    echo "## Unit Test Results" >> "$OUTPUT_FILE"
    echo "" >> "$OUTPUT_FILE"
    
    for trx_file in "$RESULTS_DIR/Unit"/*.trx; do
        if [ -f "$trx_file" ]; then
            filename=$(basename "$trx_file")
            parse_trx_summary "$trx_file" "$filename"
        fi
    done
fi

# Process integration test results
if [ -d "$RESULTS_DIR/Integration" ]; then
    echo "## Integration Test Results" >> "$OUTPUT_FILE"
    echo "" >> "$OUTPUT_FILE"
    
    for trx_file in "$RESULTS_DIR/Integration"/*.trx; do
        if [ -f "$trx_file" ]; then
            filename=$(basename "$trx_file")
            parse_trx_summary "$trx_file" "$filename"
        fi
    done
fi

# Add timestamp
echo "" >> "$OUTPUT_FILE"
echo "---" >> "$OUTPUT_FILE"
echo "" >> "$OUTPUT_FILE"
echo "Report generated at: $(date -u +"%Y-%m-%d %H:%M:%S UTC")" >> "$OUTPUT_FILE"

echo "Test report generated successfully: $OUTPUT_FILE"
