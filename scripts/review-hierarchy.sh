#!/bin/bash

# Script to review documentation hierarchy
# Usage: ./scripts/review-hierarchy.sh

DOCS_DIR="docs"

echo "==================================="
echo "Documentation Hierarchy Review"
echo "==================================="
echo ""

ISSUES_FOUND=0

# Expected structure
declare -a EXPECTED_DIRS=(
    "docs/getting-started"
    "docs/core-features"
    "docs/advanced-topics"
    "docs/reference"
)

declare -a EXPECTED_FILES=(
    "README.md"
    "docs/README.md"
    "docs/getting-started/README.md"
    "docs/getting-started/QuickStart.md"
    "docs/getting-started/Installation.md"
    "docs/getting-started/FirstEntity.md"
    "docs/core-features/README.md"
    "docs/core-features/EntityDefinition.md"
    "docs/core-features/BasicOperations.md"
    "docs/core-features/QueryingData.md"
    "docs/core-features/ExpressionFormatting.md"
    "docs/core-features/BatchOperations.md"
    "docs/core-features/Transactions.md"
    "docs/advanced-topics/README.md"
    "docs/advanced-topics/CompositeEntities.md"
    "docs/advanced-topics/GlobalSecondaryIndexes.md"
    "docs/advanced-topics/STSIntegration.md"
    "docs/advanced-topics/PerformanceOptimization.md"
    "docs/advanced-topics/ManualPatterns.md"
    "docs/reference/README.md"
    "docs/reference/AttributeReference.md"
    "docs/reference/FormatSpecifiers.md"
    "docs/reference/ErrorHandling.md"
    "docs/reference/Troubleshooting.md"
)

echo "1. Checking directory structure..."
echo ""

for dir in "${EXPECTED_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        echo "✅ $dir"
    else
        echo "❌ Missing directory: $dir"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi
done

echo ""
echo "2. Checking required files..."
echo ""

for file in "${EXPECTED_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "✅ $file"
    else
        echo "❌ Missing file: $file"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi
done

echo ""
echo "3. Checking for orphaned files..."
echo ""

# Find markdown files not in expected locations
ORPHANED=0
find "$DOCS_DIR" -name "*.md" -type f ! -path "*/templates/*" | sort | while read -r file; do
    # Check if file is in expected list or is a legacy file
    basename=$(basename "$file")
    dirname=$(dirname "$file")
    
    # Skip if it's in the expected files
    found=0
    for expected in "${EXPECTED_FILES[@]}"; do
        if [ "$file" = "$expected" ]; then
            found=1
            break
        fi
    done
    
    if [ $found -eq 0 ]; then
        # Check if it's a known legacy file
        if [[ "$basename" =~ ^(CodeExamples|DeveloperGuide|MigrationGuide|PerformanceOptimizationGuide|SourceGeneratorGuide|STSIntegrationGuide|TroubleshootingGuide|INDEX|QUICK_REFERENCE)\.md$ ]]; then
            echo "ℹ️  Legacy file (consider consolidating): $file"
        else
            echo "⚠️  Orphaned file: $file"
            ORPHANED=$((ORPHANED + 1))
        fi
    fi
done

if [ $ORPHANED -gt 0 ]; then
    ISSUES_FOUND=$((ISSUES_FOUND + ORPHANED))
fi

echo ""
echo "4. Checking navigation structure..."
echo ""

# Check if main README links to documentation sections
if grep -q "docs/getting-started" README.md && \
   grep -q "docs/core-features" README.md && \
   grep -q "docs/advanced-topics" README.md && \
   grep -q "docs/reference" README.md; then
    echo "✅ Main README.md has navigation to all sections"
else
    echo "❌ Main README.md missing navigation links"
    ISSUES_FOUND=$((ISSUES_FOUND + 1))
fi

# Check if section README files exist and have content
for section in "getting-started" "core-features" "advanced-topics" "reference"; do
    readme="docs/$section/README.md"
    if [ -f "$readme" ]; then
        if [ $(wc -l < "$readme") -gt 10 ]; then
            echo "✅ $readme has content"
        else
            echo "⚠️  $readme exists but may be too short"
        fi
    fi
done

echo ""
echo "5. Checking logical flow..."
echo ""

# Check if getting-started files link to next steps
if grep -qE "(Next|See also|Learn more)" docs/getting-started/QuickStart.md; then
    echo "✅ QuickStart.md has next steps"
else
    echo "⚠️  QuickStart.md may be missing next steps"
fi

# Check if files have breadcrumb navigation
MISSING_BREADCRUMBS=0
for file in "${EXPECTED_FILES[@]}"; do
    if [ -f "$file" ] && [ "$file" != "README.md" ] && [ "$file" != "docs/README.md" ]; then
        if ! grep -qE "\[Documentation\].*>" "$file" && ! grep -qE "^# " "$file" | head -1 | grep -q "README"; then
            # Skip if it's a README file
            if [[ ! "$file" =~ README\.md$ ]]; then
                MISSING_BREADCRUMBS=$((MISSING_BREADCRUMBS + 1))
            fi
        fi
    fi
done

if [ $MISSING_BREADCRUMBS -eq 0 ]; then
    echo "✅ All files have breadcrumb navigation"
else
    echo "ℹ️  $MISSING_BREADCRUMBS files may be missing breadcrumb navigation"
fi

echo ""
echo "==================================="
echo "Documentation Structure Summary"
echo "==================================="
echo ""
echo "Expected structure:"
echo "  docs/"
echo "  ├── getting-started/    (3 guides)"
echo "  ├── core-features/      (6 guides)"
echo "  ├── advanced-topics/    (5 guides)"
echo "  └── reference/          (4 references)"
echo ""

echo "==================================="
echo "Review Summary"
echo "==================================="
echo "Critical issues found: $ISSUES_FOUND"
echo ""

if [ $ISSUES_FOUND -eq 0 ]; then
    echo "✅ Documentation hierarchy is well-structured!"
    exit 0
else
    echo "⚠️  Please review the issues above"
    exit 0  # Don't fail, just inform
fi
