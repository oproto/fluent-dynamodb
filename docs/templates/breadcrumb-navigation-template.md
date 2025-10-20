# Breadcrumb Navigation Template

Use this template at the top of each documentation file (after front matter) to provide hierarchical navigation.

## Template

```markdown
[Documentation](../README.md) > [Category Name](README.md) > Document Title

# Document Title

[Previous: Previous Document](PreviousDocument.md) | [Next: Next Document](NextDocument.md)
```

## Usage Guidelines

1. **Place after front matter**: The breadcrumb should appear immediately after the front matter block
2. **Update all links**: Ensure all relative paths are correct based on the file location
3. **Category link**: Links to the README.md in the current category folder
4. **Previous/Next**: Optional navigation to adjacent documents in the learning path
5. **Omit Previous/Next if not applicable**: First and last documents in a sequence may omit one direction

## Examples

### Getting Started Document
```markdown
[Documentation](../README.md) > [Getting Started](README.md) > Quick Start

# Quick Start

[Next: Installation](Installation.md)
```

### Core Features Document (Middle of Sequence)
```markdown
[Documentation](../README.md) > [Core Features](README.md) > Querying Data

# Querying Data

[Previous: Basic Operations](BasicOperations.md) | [Next: Expression Formatting](ExpressionFormatting.md)
```

### Advanced Topics Document
```markdown
[Documentation](../README.md) > [Advanced Topics](README.md) > Composite Entities

# Composite Entities

[Previous: Performance Optimization](PerformanceOptimization.md) | [Next: Global Secondary Indexes](GlobalSecondaryIndexes.md)
```

### Reference Document (No Sequence)
```markdown
[Documentation](../README.md) > [Reference](README.md) > Attribute Reference

# Attribute Reference
```

## Path Adjustments by Category

- **getting-started/**: `../README.md` for docs root, `README.md` for category
- **core-features/**: `../README.md` for docs root, `README.md` for category
- **advanced-topics/**: `../README.md` for docs root, `README.md` for category
- **reference/**: `../README.md` for docs root, `README.md` for category

## Visual Separator

Add a horizontal rule after the breadcrumb and title section to separate navigation from content:

```markdown
[Documentation](../README.md) > [Core Features](README.md) > Basic Operations

# Basic Operations

[Previous: Entity Definition](EntityDefinition.md) | [Next: Querying Data](QueryingData.md)

---
```
