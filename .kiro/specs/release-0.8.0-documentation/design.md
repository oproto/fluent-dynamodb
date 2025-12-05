# Design Document: Release 0.8.0 Documentation

## Overview

This design addresses documentation corrections and release preparation for version 0.8.0 of Oproto.FluentDynamoDb. The work involves:

1. Correcting installation instructions to reflect that the source generator is bundled
2. Updating API patterns from property-based to method-based access
3. Preparing CHANGELOG.md for the 0.8.0 release
4. Creating release notes for the first public preview
5. Documenting all changes in the documentation changelog

## Architecture

This is a documentation-only change with no code modifications. The changes affect:

```
README.md                           # Main readme with Quick Start
CHANGELOG.md                        # Version history
docs/
├── getting-started/
│   ├── QuickStart.md              # Quick start guide
│   └── Installation.md            # Installation instructions
├── DOCUMENTATION_CHANGELOG.md     # Documentation change tracking
├── core-features/
│   ├── BasicOperations.md         # CRUD operations
│   ├── LinqExpressions.md         # LINQ expression examples
│   └── ...
├── advanced-topics/
│   ├── AdvancedTypes.md           # Advanced type handling
│   ├── Discriminators.md          # Discriminator patterns
│   └── ...
└── reference/
    ├── ErrorHandling.md           # Error handling patterns
    ├── AdoptionGuide.md           # Migration guide
    └── ...
RELEASE_NOTES.md                   # New file for 0.8.0 release notes
```

## Components and Interfaces

### Installation Instructions Changes

**Current (Incorrect):**
```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
dotnet add package Oproto.FluentDynamoDb.Attributes
```

**Correct:**
```bash
dotnet add package Oproto.FluentDynamoDb
```

The source generator is bundled in the main package (included as an analyzer in the NuGet package). The attributes are also in the main package - there is no separate `Oproto.FluentDynamoDb.Attributes` package.

### API Pattern Changes

**Property-based (Old/Incorrect):**
```csharp
await table.Put.WithItem(user).PutAsync();
await table.Query.Where(...).ToListAsync();
await table.Get.WithKey(...).GetItemAsync();
await table.Update.WithKey(...).UpdateAsync();
await table.Delete.WithKey(...).DeleteAsync();
await table.Scan.ToListAsync();
```

**Method-based (Current/Correct):**

There are three API styles available, in order of preference:

**1. Convenience Methods (Simplest - for basic operations):**
```csharp
// Direct async methods when no additional options needed
await table.Users.PutAsync(user);
await table.Users.GetAsync("user123");
await table.Users.DeleteAsync("user123");
await table.Users.DeleteAsync("pk", "sk");  // For composite keys
```

**2. Entity Accessor + Builder (For operations with options):**
```csharp
// When you need conditions, projections, return values, etc.
await table.Users.Put(user)
    .Where(x => x.Pk.AttributeNotExists())
    .PutAsync();

await table.Users.Query()
    .Where(x => x.Status == "active")
    .ToListAsync();

await table.Users.Get("user123")
    .WithProjection("name, email")
    .GetItemAsync();

await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "inactive" })
    .UpdateAsync();

await table.Users.Delete("user123")
    .Where(x => x.Status == "pending")
    .DeleteAsync();
```

**3. Generic Methods (For dynamic scenarios):**
```csharp
await table.Put<User>().WithItem(user).PutAsync();
await table.Query<User>().Where(...).ToListAsync();
await table.Get<User>().WithKey(...).GetItemAsync();
```

When updating documentation examples, prefer the simplest approach that accomplishes the task:
- Use convenience methods (`PutAsync(item)`, `GetAsync(pk)`, `DeleteAsync(pk)`) for simple operations
- Use builder pattern only when additional options are needed (conditions, projections, etc.)

### Files Requiring API Pattern Updates

Based on grep search results, the following files contain old property-based patterns:

1. **README.md** - Quick Start section
2. **docs/getting-started/QuickStart.md** - Installation and examples
3. **docs/getting-started/Installation.md** - Installation instructions
4. **docs/core-features/BasicOperations.md** - CRUD examples
5. **docs/core-features/LinqExpressions.md** - LINQ examples
6. **docs/advanced-topics/AdvancedTypes.md** - Advanced type examples
7. **docs/advanced-topics/Discriminators.md** - Discriminator examples
8. **docs/reference/ErrorHandling.md** - Error handling examples
9. **docs/reference/AdoptionGuide.md** - Migration examples
10. **docs/reference/AdvancedTypesQuickReference.md** - Quick reference
11. **docs/reference/LoggingTroubleshooting.md** - Logging examples
12. **docs/TroubleshootingGuide.md** - Troubleshooting examples
13. **Oproto.FluentDynamoDb/Expressions/EXPRESSION_EXAMPLES.md** - Expression examples

## Data Models

No data model changes - this is documentation only.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Since this is a documentation-only change, correctness properties focus on verifiable documentation content:

### Property 1: No Property-Based API Patterns in Documentation
*For any* markdown file in the docs/ folder or README.md, the file should NOT contain the pattern `table.Put.`, `table.Query.`, `table.Get.`, `table.Update.`, `table.Delete.`, or `table.Scan.` followed by a method call (indicating property-based access).
**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

### Property 2: No Separate Package References for Bundled Components
*For any* installation instruction in documentation, the instruction should NOT reference `Oproto.FluentDynamoDb.SourceGenerator` or `Oproto.FluentDynamoDb.Attributes` as separate packages to install.
**Validates: Requirements 1.1, 1.2, 1.3, 1.4**

## Error Handling

Not applicable - documentation changes only.

## Testing Strategy

### Manual Verification

Since this is documentation, testing is primarily manual verification:

1. **Grep Verification**: After changes, run grep searches to verify no old patterns remain:
   ```bash
   grep -r "table\.Put\." docs/ README.md
   grep -r "table\.Query\." docs/ README.md
   grep -r "table\.Get\." docs/ README.md
   grep -r "SourceGenerator" docs/getting-started/
   grep -r "Attributes" docs/getting-started/Installation.md
   ```

2. **Visual Review**: Review updated files to ensure examples are coherent and complete.

3. **Build Verification**: Ensure any code examples in documentation would compile with the current API.

### Documentation Changelog Verification

Verify that `docs/DOCUMENTATION_CHANGELOG.md` contains entries for:
- Installation instruction corrections
- API pattern corrections (property-based to method-based)
- Date, file path, before/after patterns, and reason for each change
