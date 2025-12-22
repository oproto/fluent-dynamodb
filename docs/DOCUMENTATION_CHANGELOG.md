# Documentation Changelog

This changelog tracks corrections and updates to the Oproto.FluentDynamoDb documentation.

## Purpose

This file is maintained **separately from the repository `CHANGELOG.md`** to facilitate synchronization with derived documentation maintained by other teams (e.g., website documentation at [fluentdynamodb.dev](https://fluentdynamodb.dev)).

The repository `CHANGELOG.md` tracks code changes, new features, and bug fixes. This file specifically tracks:
- Documentation corrections (fixing incorrect API references, outdated patterns)
- Documentation improvements (clarifications, additional examples)
- Documentation restructuring (file moves, reorganization)

## How to Use This Changelog

### For Documentation Maintainers

When syncing derived documentation:
1. Note the date of your last sync
2. Review all entries since that date
3. Apply the same corrections to your derived documentation
4. Update your sync date

### Entry Format

Each entry follows this structure:

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

### Categories

Entries may be categorized as:
- **API Correction**: Fixing incorrect method names or signatures
- **Pattern Update**: Updating code patterns to match current best practices
- **Clarification**: Adding notes or explanations for clarity
- **Example Fix**: Correcting code examples that wouldn't compile or work correctly

---

## Changelog Entries

<!-- Add new entries below this line, with most recent at the top -->

## [2025-12-22]

### File: docs/core-features/MapsAndLists.md

**Category:** API Pattern Update

**Summary:** Updated List Operations section to use new SetAt/RemoveAt extension methods and added comprehensive dynamic index support documentation.

**Changes:**

1. **Updated "Update Element by Index" section** - Now uses `SetAt` extension method:
   - Before: `.Set(x => x.Tags[0], "updated-first-tag")`
   - After: `.Set(x => x.Tags.SetAt(0, "updated-first-tag"))`

2. **Updated "Remove Element by Index" section** - Now uses `RemoveAt` extension method:
   - Before: `.Remove(x => x.Tags[2])`
   - After: `.Set(x => x.Tags.RemoveAt(2))`

3. **Added "Dynamic Index Support" subsection** with examples for:
   - Variable index: `int index = GetIndex(); .Set(x => x.Tags.SetAt(index, "value"))`
   - Method call index: `.Set(x => x.Tags.SetAt(GetTargetIndex(), "value"))`
   - Property access index: `.Set(x => x.Tags.SetAt(config.Index, "value"))`
   - Dynamic index in filter expressions
   - Entity parameter restriction documentation
   - Index validation (non-negative requirement)

4. **Added "Chaining List Operations" subsection** documenting:
   - Multiple SetAt chaining: `.SetAt(0, "a").SetAt(1, "b")`
   - DynamoDB overlapping path limitation
   - Allowed vs disallowed chaining combinations

5. **Updated "Nested List Operations" section** with SetAt/RemoveAt examples

6. **Updated "List Operations Quick Reference" table** with new methods

7. **Updated Troubleshooting section**:
   - Removed outdated "List Index Must Be Constant" error (dynamic indices now supported)
   - Added "List Index Cannot Reference Entity Parameter" error
   - Added "List Index Must Be Non-Negative" error
   - Added "Overlapping Document Paths" error

**Reason:** The old builder methods (`.SetAt(x => x.Tags[0], "value")` and `.RemoveAt(x => x.Tags[2])`) have been replaced with extension methods (`.SetAt(index, value)` and `.RemoveAt(index)`) for API consistency with other list operations (Append, Prepend). Dynamic index support has been added to allow variables, method calls, and property accesses as indices.

---

## [2025-12-22]

### File: .kiro/steering/fluentdynamodb.md

**Category:** API Pattern Update

**Before:**
```csharp
// Update/Remove by index - generates: SET #tags[0] = :v0 or REMOVE #tags[2]
await table.Items.Update(itemId).Set(x => x.Tags[0], "updated").UpdateAsync();
await table.Items.Update(itemId).Remove(x => x.Tags[2]).UpdateAsync();
```

**After:**
```csharp
// SetAt/RemoveAt - SET #tags[0] = :v0 or REMOVE #tags[2]
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(0, "updated")).UpdateAsync();
await table.Items.Update(itemId).Set(x => x.Tags.RemoveAt(2)).UpdateAsync();

// Dynamic index - variable, method call, or property (NOT entity parameter)
int index = GetIndex();
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(index, "updated")).UpdateAsync();

// Chained SetAt - SET #tags[0] = :v0, #tags[1] = :v1
await table.Items.Update(itemId).Set(x => x.Tags.SetAt(0, "a").SetAt(1, "b")).UpdateAsync();
```

**Reason:** Updated List Expressions section to use new SetAt/RemoveAt extension methods instead of old builder methods. Added dynamic index support documentation, chained SetAt examples, and DynamoDB overlapping path limitation note. Old builder methods (.SetAt/.RemoveAt on builder) have been removed.

---

## [2025-12-22]

### Fresh Start - External Sources Synchronized

**Category:** Documentation Reset

**Summary:** This documentation changelog has been truncated to provide a fresh start. All external documentation sources (e.g., fluentdynamodb.dev) have been synchronized with the current state of the repository documentation.

**Reason:** Previous changelog entries have been applied to all derived documentation. Starting fresh reduces file size and improves maintainability while ensuring all documentation sources are in sync.
