# Design Document: Example Documentation Update

## Overview

This design document outlines the approach for updating the documentation of four example projects (TodoList, TransactionDemo, InvoiceManager, StoreLocator) to accurately reflect the current code structure, entity patterns, and API usage. The updates will also ensure proper indexing of these examples in the main documentation.

## Architecture

The documentation update follows a straightforward file modification approach:

```
examples/
├── TodoList/
│   └── README.md          # Update project structure, entity patterns, code examples
├── TransactionDemo/
│   └── README.md          # Update project structure, transaction patterns
├── InvoiceManager/
│   └── README.md          # Update project structure, single-table design examples
└── StoreLocator/
    └── README.md          # Update project structure, geospatial patterns

docs/
├── README.md              # Add examples section
└── examples/
    └── README.md          # Add links to example applications
```

## Components and Interfaces

### Documentation Files to Update

| File | Changes Required |
|------|------------------|
| `examples/TodoList/README.md` | Fix project structure, update entity definition, update CRUD examples |
| `examples/TransactionDemo/README.md` | Fix project structure, update key patterns, update transaction examples |
| `examples/InvoiceManager/README.md` | Fix project structure, update key patterns, update query examples |
| `examples/StoreLocator/README.md` | Fix project structure, update table schema, update query examples |
| `docs/examples/README.md` | Add example applications section with links |
| `docs/README.md` | Add examples navigation section |

### Key Pattern Updates

All example READMEs need to be updated to reflect the current entity patterns:

**Old Pattern (Incorrect):**
```csharp
[DynamoDbEntity]
[DynamoDbTable("table-name")]
public partial class Entity : IDynamoDbEntity
{
    public static string CreatePk(string id) => $"PREFIX#{id}";
}
```

**New Pattern (Correct):**
```csharp
[DynamoDbTable("table-name")]
public partial class Entity
{
    [PartitionKey(Prefix = "PREFIX")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; }
}

// Usage: Entity.Keys.Pk(id) returns "PREFIX#id"
```

### API Usage Updates

All example READMEs need to show the generated entity accessor pattern:

**Old Pattern (Incorrect):**
```csharp
await table.Put(item).ExecuteAsync();
var items = await table.Scan().ToListAsync();
```

**New Pattern (Correct):**
```csharp
await table.TodoItems.PutAsync(item);
var items = await table.TodoItems.Scan().ToListAsync();
```

## Data Models

Not applicable - this is a documentation-only update with no data model changes.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, all acceptance criteria relate to documentation content accuracy rather than functional software behavior. Documentation updates are not amenable to property-based testing as they involve human-readable content that must be manually verified for accuracy and clarity.

**No testable properties identified** - This feature involves documentation updates only, which require manual review rather than automated testing.

## Error Handling

Not applicable - documentation updates do not involve runtime error handling.

## Testing Strategy

### Manual Verification

Since this is a documentation-only update, testing consists of manual verification:

1. **Structure Verification**: Compare documented project structures against actual file system layout
2. **Code Example Verification**: Ensure code examples compile and match actual implementation patterns
3. **Link Verification**: Verify all documentation links resolve correctly
4. **Consistency Check**: Ensure terminology and patterns are consistent across all example READMEs

### Verification Checklist

For each example README:
- [ ] Project structure matches actual folder layout
- [ ] Entity definitions use correct attributes (`[DynamoDbTable]`, not `[DynamoDbEntity]`)
- [ ] No manual `: IDynamoDbEntity` interface implementation shown
- [ ] Key construction uses `Entity.Keys.Pk()` pattern
- [ ] CRUD operations use generated entity accessor pattern
- [ ] Code examples are syntactically correct

For documentation index:
- [ ] All four example projects are linked
- [ ] Links resolve to correct files
- [ ] Descriptions accurately summarize each example
