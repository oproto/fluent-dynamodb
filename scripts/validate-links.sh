#!/bin/bash

# Script to validate all internal markdown links in documentation
# Usage: ./scripts/validate-links.sh

DOCS_DIR="docs"
README_FILE="README.md"
BROKEN_LINKS_FILE="/tmp/broken_links_$$.txt"
TOTAL_LINKS=0
BROKEN_LINKS=0

echo "==================================="
echo "Documentation Link Validator"
echo "==================================="
echo ""

# Clean up temp file
> "$BROKEN_LINKS_FILE"

# Function to check if a file exists
check_file_exists() {
    local link_path="$1"
    local source_file="$2"
    
    if [ -f "$link_path" ]; then
        return 0
    else
        echo "❌ BROKEN LINK in $source_file" >> "$BROKEN_LINKS_FILE"
        echo "   Target not found: $link_path" >> "$BROKEN_LINKS_FILE"
        echo "" >> "$BROKEN_LINKS_FILE"
        return 1
    fi
}

# Function to resolve relative path
resolve_path() {
    local source_dir="$1"
    local link="$2"
    
    # Remove anchor if present
    local path_only="${link%%#*}"
    
    # If empty after removing anchor, it's just an anchor link (valid)
    if [ -z "$path_only" ]; then
        echo ""
        return 0
    fi
    
    # Resolve the path relative to source directory
    local resolved
    if [[ "$path_only" == /* ]]; then
        # Absolute path from repo root
        resolved="${path_only:1}"
    else
        # Relative path
        resolved="$source_dir/$path_only"
    fi
    
    # Normalize the path (remove ./ and ../)
    while [[ "$resolved" =~ /\./ ]]; do
        resolved="${resolved//\/.\//\/}"
    done
    
    while [[ "$resolved" =~ /[^/]+/\.\./ ]]; do
        resolved=$(echo "$resolved" | sed 's#/[^/]*/\.\./#/#')
    done
    
    echo "$resolved"
}

# Function to extract and validate links from a markdown file
validate_links_in_file() {
    local file="$1"
    local file_dir=$(dirname "$file")
    local file_links=0
    local file_broken=0
    
    # Extract markdown links: [text](url)
    while IFS= read -r match; do
        # Extract the URL part
        link=$(echo "$match" | sed -E 's/\[[^]]+\]\(([^)]+)\)/\1/')
        
        # Skip external links (http://, https://, mailto:)
        if [[ "$link" =~ ^https?:// ]] || [[ "$link" =~ ^mailto: ]]; then
            continue
        fi
        
        TOTAL_LINKS=$((TOTAL_LINKS + 1))
        file_links=$((file_links + 1))
        
        # Resolve the path
        resolved_path=$(resolve_path "$file_dir" "$link")
        
        # Check if it's just an anchor (empty path after removing anchor)
        if [ -z "$resolved_path" ]; then
            continue
        fi
        
        # Check if file exists
        if ! check_file_exists "$resolved_path" "$file"; then
            BROKEN_LINKS=$((BROKEN_LINKS + 1))
            file_broken=$((file_broken + 1))
        fi
    done < <(grep -oE '\[[^]]+\]\([^)]+\)' "$file" 2>/dev/null || true)
    
    if [ $file_broken -eq 0 ] && [ $file_links -gt 0 ]; then
        echo "✅ $file ($file_links internal links)"
    elif [ $file_links -eq 0 ]; then
        echo "⚪ $file (no internal links)"
    else
        echo "❌ $file ($file_broken broken out of $file_links links)"
    fi
}

# Validate README.md
if [ -f "$README_FILE" ]; then
    echo "Checking $README_FILE..."
    validate_links_in_file "$README_FILE"
    echo ""
fi

# Validate all markdown files in docs directory
if [ -d "$DOCS_DIR" ]; then
    echo "Checking documentation files in $DOCS_DIR/..."
    echo ""
    
    while IFS= read -r file; do
        validate_links_in_file "$file"
    done < <(find "$DOCS_DIR" -name "*.md" -type f | sort)
fi

echo ""
echo "==================================="
echo "Validation Summary"
echo "==================================="
echo "Total internal links checked: $TOTAL_LINKS"
echo "Broken links found: $BROKEN_LINKS"
echo ""

if [ -s "$BROKEN_LINKS_FILE" ]; then
    echo "Broken links details:"
    echo ""
    cat "$BROKEN_LINKS_FILE"
fi

# Clean up
rm -f "$BROKEN_LINKS_FILE"

if [ $BROKEN_LINKS -eq 0 ]; then
    echo "✅ All links are valid!"
    exit 0
else
    echo "❌ Found $BROKEN_LINKS broken link(s)"
    echo "Please fix the broken links above."
    exit 1
fi
