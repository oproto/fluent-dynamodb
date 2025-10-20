#!/bin/bash

# Script to check terminology consistency in documentation
# Usage: ./scripts/check-terminology.sh

DOCS_DIR="docs"
README_FILE="README.md"

echo "==================================="
echo "Terminology Consistency Checker"
echo "==================================="
echo ""

ISSUES_FOUND=0

echo "Checking for inconsistent terminology..."
echo ""

# Function to check for a term and report if found
check_term() {
    local incorrect="$1"
    local correct="$2"
    local context="$3"
    
    # Search in all markdown files, excluding templates
    results=$(grep -rn "$incorrect" "$DOCS_DIR" "$README_FILE" 2>/dev/null | \
              grep -v "\.git" | \
              grep -v "docs/templates/" | \
              grep -v "# $incorrect" | \
              grep -v "\`$incorrect\`" || true)
    
    if [ -n "$results" ]; then
        count=$(echo "$results" | wc -l | tr -d ' ')
        if [ "$count" -gt 0 ]; then
            echo "⚠️  Found '$incorrect' ($count occurrences) - should be '$correct'"
            if [ -n "$context" ]; then
                echo "   Context: $context"
            fi
            echo "$results" | head -3
            if [ "$count" -gt 3 ]; then
                echo "   ... and $((count - 3)) more occurrences"
            fi
            echo ""
            ISSUES_FOUND=$((ISSUES_FOUND + 1))
        fi
    fi
}

# Check for hyphenated versions (should be space-separated in prose)
check_term "source-generation" "source generation" "Use spaces in prose, hyphens only in URLs/filenames"
check_term "expression-formatting" "expression formatting" "Use spaces in prose, hyphens only in URLs/filenames"
check_term "manual-pattern" "manual pattern" "Use spaces in prose, hyphens only in URLs/filenames"

# Check for incorrect capitalization
check_term "dynamodb" "DynamoDB" "Amazon's service name"
check_term "Dynamodb" "DynamoDB" "Amazon's service name"
check_term "dynamoDB" "DynamoDB" "Amazon's service name"
check_term " aws " " AWS " "Amazon Web Services acronym"
check_term "Aws" "AWS" "Amazon Web Services acronym"
check_term "nuget" "NuGet" ".NET package manager"
check_term "Nuget" "NuGet" ".NET package manager"
check_term " aot " " AOT " "Ahead-of-Time compilation"
check_term "Aot" "AOT" "Ahead-of-Time compilation"

echo ""
echo "==================================="
echo "Glossary of Preferred Terms"
echo "==================================="
echo ""
echo "**Source Generation**: The compile-time code generation feature"
echo "**Expression Formatting**: The string.Format-style parameter syntax"
echo "**Manual Pattern**: Lower-level approach without source generation"
echo "**DynamoDB**: Amazon's NoSQL database service"
echo "**AWS**: Amazon Web Services"
echo "**NuGet**: The .NET package manager"
echo "**AOT**: Ahead-of-Time compilation"
echo "**GSI**: Global Secondary Index"
echo "**STS**: AWS Security Token Service"
echo "**Partition Key**: The primary key component for data distribution"
echo "**Sort Key**: The optional key component for sorting within a partition"
echo "**Composite Entity**: Entity spanning multiple DynamoDB items"
echo "**Related Entity**: Entity automatically populated based on patterns"
echo ""

echo "==================================="
echo "Summary"
echo "==================================="
echo "Inconsistent terms found: $ISSUES_FOUND"
echo ""

if [ $ISSUES_FOUND -eq 0 ]; then
    echo "✅ Terminology is consistent!"
else
    echo "⚠️  Please review and update inconsistent terminology"
    echo "    (This is informational - not blocking)"
fi

exit 0
