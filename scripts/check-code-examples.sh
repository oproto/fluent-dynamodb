#!/bin/bash

# Script to check code example consistency in documentation
# Usage: ./scripts/check-code-examples.sh

DOCS_DIR="docs"
README_FILE="README.md"

echo "==================================="
echo "Code Example Consistency Checker"
echo "==================================="
echo ""

ISSUES_FOUND=0
TOTAL_EXAMPLES=0

# Function to check code blocks in a file
check_code_blocks() {
    local file="$1"
    local in_code_block=0
    local code_lang=""
    local line_num=0
    local block_start=0
    local has_issues=0
    
    while IFS= read -r line; do
        line_num=$((line_num + 1))
        
        # Check for code block start
        if [[ "$line" =~ ^\`\`\`([a-z]*) ]]; then
            if [ $in_code_block -eq 0 ]; then
                in_code_block=1
                code_lang="${BASH_REMATCH[1]}"
                block_start=$line_num
                TOTAL_EXAMPLES=$((TOTAL_EXAMPLES + 1))
                
                # Check if language is specified for C# code
                if [ -z "$code_lang" ]; then
                    # Peek ahead to see if it looks like C# code
                    next_lines=$(tail -n +$((line_num + 1)) "$file" | head -20)
                    if echo "$next_lines" | grep -qE "(class|public|private|namespace|using|var |await |async )"; then
                        echo "⚠️  $file:$line_num - Code block missing language identifier (appears to be C#)"
                        has_issues=1
                        ISSUES_FOUND=$((ISSUES_FOUND + 1))
                    fi
                fi
            else
                in_code_block=0
                code_lang=""
            fi
        fi
        
        # Check for common issues in C# code blocks
        if [ $in_code_block -eq 1 ] && [ "$code_lang" = "csharp" ]; then
            # Check for incomplete examples (missing using statements for common types)
            if echo "$line" | grep -qE "DynamoDbTable|IAmazonDynamoDB" && ! grep -qE "^using (Amazon|Oproto)" "$file"; then
                if [ $has_issues -eq 0 ]; then
                    echo "ℹ️  $file:$block_start - Code block may be missing using statements"
                    has_issues=1
                fi
            fi
        fi
    done < "$file"
}

# Function to check for recommended patterns
check_patterns() {
    local file="$1"
    
    # Check if file has code examples with manual patterns but no note about recommended approach
    if grep -q "\.WithValue(" "$file" && ! grep -qE "(recommended|prefer|modern approach|expression formatting)" "$file"; then
        echo "⚠️  $file - Contains manual patterns without mentioning recommended approach"
        ISSUES_FOUND=$((ISSUES_FOUND + 1))
    fi
    
    # Check if entity examples have required attributes
    if grep -q "public.*class.*{" "$file"; then
        # Check for entity definitions
        if grep -qE "public (partial )?class" "$file"; then
            # Should have DynamoDbTable or DynamoDbAttribute nearby
            if ! grep -qE "\[DynamoDbTable\]|\[DynamoDbAttribute\]|\[PartitionKey\]" "$file"; then
                # Might be a non-entity class, skip
                :
            else
                # Check if partial keyword is used
                if grep -qE "public class [A-Z]" "$file" && ! grep -q "public partial class" "$file"; then
                    echo "⚠️  $file - Entity class definition missing 'partial' keyword"
                    ISSUES_FOUND=$((ISSUES_FOUND + 1))
                fi
            fi
        fi
    fi
}

echo "Checking code blocks in documentation files..."
echo ""

# Check README
if [ -f "$README_FILE" ]; then
    check_code_blocks "$README_FILE"
    check_patterns "$README_FILE"
fi

# Check all markdown files in docs
find "$DOCS_DIR" -name "*.md" -type f ! -path "*/templates/*" | sort | while read -r file; do
    check_code_blocks "$file"
    check_patterns "$file"
done

echo ""
echo "==================================="
echo "Code Example Best Practices"
echo "==================================="
echo ""
echo "✓ Always specify language for code blocks (e.g., \`\`\`csharp)"
echo "✓ Include necessary using statements for clarity"
echo "✓ Use 'partial' keyword for entity classes"
echo "✓ Show recommended patterns (expression formatting) first"
echo "✓ Add comments for non-obvious code sections"
echo "✓ Mark optional code with '// Optional:' comments"
echo "✓ Include complete, runnable examples when possible"
echo ""

echo "==================================="
echo "Summary"
echo "==================================="
echo "Total code examples found: $TOTAL_EXAMPLES"
echo "Issues found: $ISSUES_FOUND"
echo ""

if [ $ISSUES_FOUND -eq 0 ]; then
    echo "✅ Code examples are consistent!"
else
    echo "⚠️  Please review code examples for consistency"
    echo "    (This is informational - not blocking)"
fi

exit 0
