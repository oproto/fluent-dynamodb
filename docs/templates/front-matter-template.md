# Front Matter Template

Use this template at the top of each documentation file to provide metadata for navigation and search.

## Template

```markdown
---
title: "Document Title"
category: "getting-started|core-features|advanced-topics|reference"
order: 1
keywords: ["keyword1", "keyword2", "keyword3"]
related: ["RelatedFile1.md", "RelatedFile2.md"]
---
```

## Field Descriptions

- **title**: The human-readable title of the document (used in navigation and search)
- **category**: The documentation section this file belongs to
  - `getting-started`: Introductory content for new users
  - `core-features`: Essential functionality and common operations
  - `advanced-topics`: Complex scenarios and specialized features
  - `reference`: API documentation and lookup tables
- **order**: Numeric ordering within the category (lower numbers appear first)
- **keywords**: Array of searchable terms related to this document
- **related**: Array of related documentation files (relative paths within the same category)

## Examples

### Getting Started Document
```markdown
---
title: "Quick Start Guide"
category: "getting-started"
order: 1
keywords: ["installation", "setup", "first entity", "getting started", "quickstart"]
related: ["Installation.md", "FirstEntity.md"]
---
```

### Core Features Document
```markdown
---
title: "Querying Data"
category: "core-features"
order: 3
keywords: ["query", "scan", "filter", "pagination", "GSI"]
related: ["BasicOperations.md", "ExpressionFormatting.md", "BatchOperations.md"]
---
```

### Advanced Topics Document
```markdown
---
title: "Composite Entities"
category: "advanced-topics"
order: 1
keywords: ["composite", "multi-item", "related entities", "relationships", "collections"]
related: ["GlobalSecondaryIndexes.md", "PerformanceOptimization.md"]
---
```

### Reference Document
```markdown
---
title: "Attribute Reference"
category: "reference"
order: 1
keywords: ["attributes", "annotations", "DynamoDbTable", "PartitionKey", "SortKey"]
related: ["FormatSpecifiers.md", "Troubleshooting.md"]
---
```
