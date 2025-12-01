# Design Document: Documentation API Corrections

## Overview

This design addresses the systematic correction of outdated API patterns in the Oproto.FluentDynamoDb documentation. The documentation contains references to `ExecuteAsync()` methods that no longer exist and incorrect patterns for accessing return values from DynamoDB operations.

The corrections will be tracked in a dedicated documentation changelog (`docs/DOCUMENTATION_CHANGELOG.md`) that is separate from the repository's main `CHANGELOG.md`, enabling easy synchronization with derived documentation maintained by other teams.

## Architecture

The correction effort follows a systematic approach:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Documentation Correction Flow                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. Identify Incorrect Patterns                                  │
│     ├── Search docs/ for ExecuteAsync references                │
│     ├── Search docs/ for response.Attributes patterns           │
│     └── Search *.cs for XML doc ExecuteAsync references         │
│                                                                  │
│  2. Apply Corrections                                            │
│     ├── Replace ExecuteAsync with correct method names          │
│     ├── Update return value access patterns                     │
│     └── Add AsyncLocal caveats where needed                     │
│                                                                  │
│  3. Record Changes                                               │
│     └── Add entries to docs/DOCUMENTATION_CHANGELOG.md          │
│                                                                  │
│  4. Update Steering                                              │
│     └── Add changelog requirements to documentation.md          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### Documentation Files to Correct

| File Category | Location | Correction Type |
|--------------|----------|-----------------|
| Core Features | `docs/core-features/*.md` | Method names, return value patterns |
| Advanced Topics | `docs/advanced-topics/*.md` | Method names, return value patterns |
| Examples | `docs/examples/*.md` | Method names, return value patterns |
| Getting Started | `docs/getting-started/*.md` | Method names |
| Reference | `docs/reference/*.md` | Method names |
| Source XML Docs | `Oproto.FluentDynamoDb/**/*.cs` | XML comment corrections |

### Method Name Mapping

| Incorrect Pattern | Correct Pattern | Context |
|------------------|-----------------|---------|
| `.ExecuteAsync()` on Get builder | `.GetItemAsync()` | GetItemRequestBuilder |
| `.ExecuteAsync()` on Put builder | `.PutAsync()` | PutItemRequestBuilder |
| `.ExecuteAsync()` on Update builder | `.UpdateAsync()` | UpdateItemRequestBuilder |
| `.ExecuteAsync()` on Delete builder | `.DeleteAsync()` | DeleteItemRequestBuilder |
| `.ExecuteAsync()` on Query builder | `.ToListAsync()` | QueryRequestBuilder (1:1 mapping) |
| `.ExecuteAsync()` on Query builder | `.ToCompositeEntityAsync()` | QueryRequestBuilder (N:1 mapping) |
| `.ExecuteAsync()` on Scan builder | `.ToListAsync()` | ScanRequestBuilder |

### Return Value Access Patterns

**Incorrect Pattern:**
```csharp
var response = await table.Users.Put(user)
    .ReturnAllOldValues()
    .PutAsync();
var oldUser = UserMapper.FromAttributeMap(response.Attributes);
```

**Correct Pattern (Option 1 - Advanced API):**
```csharp
var response = await table.Users.Put(user)
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();
var oldUser = UserMapper.FromAttributeMap(response.Attributes);
```

**Correct Pattern (Option 2 - Context-based):**
```csharp
await table.Users.Put(user)
    .ReturnAllOldValues()
    .PutAsync();
// Note: Uses AsyncLocal - not suitable for unit testing
var oldValues = DynamoDbOperationContext.Current?.PreOperationValues;
```

## Data Models

### Documentation Changelog Entry Format

```markdown
## [YYYY-MM-DD]

### File: path/to/file.md

**Before:**
```csharp
// incorrect code example
```

**After:**
```csharp
// corrected code example
```

**Reason:** Brief explanation of why this change was made.
```

### Changelog File Structure

```markdown
# Documentation Changelog

This changelog tracks corrections and updates to the Oproto.FluentDynamoDb documentation.
It is maintained separately from the repository CHANGELOG.md to facilitate synchronization
with derived documentation (e.g., website documentation at fluentdynamodb.dev).

When updating derived documentation, review entries since your last sync date.

---

## [2024-12-01]

### File: docs/core-features/BasicOperations.md
...
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, the following properties can be verified:

### Property 1: No ExecuteAsync references in documentation
*For any* markdown file in the `docs/` directory, after corrections are applied, the file should not contain references to `ExecuteAsync()` as a terminal method on request builders.
**Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**

### Property 2: No ExecuteAsync references in XML documentation
*For any* C# source file in the project, after corrections are applied, XML documentation comments should not reference `ExecuteAsync()` as a method to call.
**Validates: Requirements 4.1, 4.2**

### Property 3: Changelog entries contain required fields
*For any* entry in `docs/DOCUMENTATION_CHANGELOG.md`, the entry should contain a date, file path, before pattern, after pattern, and reason.
**Validates: Requirements 3.2, 3.3**

### Property 4: Return value patterns use correct API
*For any* code example in documentation that shows accessing `.Attributes` from a response, the response should come from `ToDynamoDbResponseAsync()` not from `PutAsync()`, `UpdateAsync()`, or `DeleteAsync()`.
**Validates: Requirements 2.1**

## Error Handling

### Ambiguous Context Detection

When correcting `ExecuteAsync()` references, some contexts may be ambiguous:
- Query operations could use `ToListAsync()` or `ToCompositeEntityAsync()`
- The correct replacement depends on whether the query returns individual items or composite entities

**Resolution:** Review surrounding context (comments, variable names, entity types) to determine the appropriate replacement. When unclear, prefer `ToListAsync()` as the more common pattern.

### Incomplete Code Examples

Some documentation may show partial code snippets that don't clearly indicate the builder type.

**Resolution:** Look for:
- Method chain origin (e.g., `table.Query`, `table.Get<T>()`)
- Variable type annotations
- Surrounding explanatory text

## Testing Strategy

### Dual Testing Approach

This specification uses both verification scripts and manual review:

**Automated Verification:**
- Grep searches for remaining `ExecuteAsync` patterns
- Validation of changelog entry format
- Verification of steering file updates

**Manual Review:**
- Context-appropriate method replacements
- Return value pattern corrections
- Documentation clarity and accuracy

### Property-Based Testing

Property-based tests will use **xUnit** with **FsCheck** (or manual verification scripts) to verify:

1. **No ExecuteAsync in docs:** Search all `.md` files in `docs/` for `ExecuteAsync()` pattern
2. **No ExecuteAsync in XML docs:** Search all `.cs` files for `ExecuteAsync` in XML comments
3. **Changelog format:** Parse `docs/DOCUMENTATION_CHANGELOG.md` and verify entry structure
4. **Return value patterns:** Search for `.Attributes` access and verify it follows `ToDynamoDbResponseAsync()`

### Test Configuration

- Property tests should run with at least 100 iterations where applicable
- Each test should be tagged with the property it validates using format: `**Feature: documentation-api-corrections, Property {number}: {property_text}**`

