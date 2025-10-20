# Documentation Validation Scripts

This directory contains scripts for validating and maintaining the Oproto.FluentDynamoDb documentation.

## Scripts

### validate-links.sh

Validates all internal markdown links in the documentation.

**Usage:**
```bash
./scripts/validate-links.sh
```

**What it checks:**
- All markdown links in README.md and docs/ directory
- Resolves relative paths correctly
- Skips external links (http://, https://, mailto:)
- Reports broken links with file locations

**Exit codes:**
- 0: All links valid
- 1: Broken links found

---

### check-terminology.sh

Checks for consistent terminology usage across documentation.

**Usage:**
```bash
./scripts/check-terminology.sh
```

**What it checks:**
- Consistent capitalization (DynamoDB, AWS, NuGet, AOT, etc.)
- Proper spacing in compound terms
- Common terminology mistakes

**Output:**
- Warnings for inconsistent terms
- Glossary of preferred terms
- Always exits with code 0 (informational only)

---

### check-code-examples.sh

Reviews code examples for consistency and completeness.

**Usage:**
```bash
./scripts/check-code-examples.sh
```

**What it checks:**
- Code blocks have language identifiers
- C# examples follow best practices
- Entity classes use 'partial' keyword
- Manual patterns include notes about recommended approach

**Output:**
- Warnings for missing language identifiers
- Best practices checklist
- Always exits with code 0 (informational only)

---

### review-hierarchy.sh

Verifies the documentation structure and organization.

**Usage:**
```bash
./scripts/review-hierarchy.sh
```

**What it checks:**
- All expected directories exist
- All required files are present
- Navigation structure is complete
- Breadcrumb navigation exists
- Identifies orphaned or legacy files

**Output:**
- Directory structure validation
- File presence validation
- Navigation completeness
- Always exits with code 0 (informational only)

---

## Running All Validations

To run all validation scripts at once:

```bash
for script in scripts/*.sh; do
    echo "Running $script..."
    $script
    echo ""
done
```

## Integration with CI/CD

These scripts can be integrated into your CI/CD pipeline:

```yaml
# Example GitHub Actions workflow
- name: Validate Documentation
  run: |
    chmod +x scripts/*.sh
    ./scripts/validate-links.sh
    ./scripts/review-hierarchy.sh
```

## Maintenance

- Run these scripts before committing documentation changes
- Run periodically to catch drift in terminology or structure
- Update scripts as documentation structure evolves

## Requirements

- Bash 3.2+ (macOS default)
- Standard Unix tools (grep, find, sed)
- No external dependencies required
