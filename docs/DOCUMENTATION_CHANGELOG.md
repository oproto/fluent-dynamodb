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

## [2026-01-04]

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added String Comparison Operators documentation using `CompareTo()` method.

**Changes:**

1. **Updated "Lambda Expression Functions" table** - Added `CompareTo()` method:
   - Added row: `| \`.CompareTo(value)\` | \`<\`, \`>\`, \`<=\`, \`>=\` | \`x => x.SortKey.CompareTo("2024-01-01") >= 0\` |`

2. **Added "String Comparison Operators" subsection** after the functions table:
   - Explains that C# doesn't support `<`, `>`, `<=`, `>=` on strings directly
   - Shows `CompareTo()` usage examples for all comparison operators
   - Includes range query example combining two `CompareTo()` calls
   - Provides pattern reference table mapping `CompareTo()` patterns to DynamoDB operators
   - Notes that `Between()` is preferred for inclusive ranges

**Reason:** New feature added to support `string.CompareTo()` instance method in lambda expressions for intuitive string range comparisons.

---

### File: docs/core-features/LinqExpressions.md

**Category:** New Feature Documentation

**Summary:** Added String Comparison Operators section to the Supported Operators documentation.

**Changes:**

1. **Added "String Comparison Operators" subsection** after Comparison Operators:
   - Documents that C# doesn't support `<`, `>`, `<=`, `>=` on strings
   - Shows `CompareTo()` usage for string greater than, greater than or equal, less than, less than or equal
   - Includes string range query example
   - Provides CompareTo Pattern Reference table
   - Includes tip about using `Between()` for inclusive ranges

**Reason:** New feature added to support `string.CompareTo()` instance method in lambda expressions for intuitive string range comparisons.

---

### File: CHANGELOG.md

**Category:** New Feature Documentation

**Summary:** Added String CompareTo Support entry to Unreleased section.

**Changes:**

1. **Added "String CompareTo Support in Lambda Expressions" entry** to the Added section:
   - Documents the new `string.CompareTo()` support
   - Lists supported comparison operators
   - Includes usage examples for range queries

**Reason:** New feature added to support `string.CompareTo()` instance method in lambda expressions.

---

## [2026-01-03]

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added comprehensive Dynamic Fields Enhancements documentation to the Dynamic Fields section.

**Changes:**

1. **Updated "Dynamic Fields" section** with prefix-based operations:
   - Added `GetFieldNamesByPrefix(prefix)` method documentation
   - Added `GetByPrefix(prefix)` and `GetByPrefixWithStrippedKeys(prefix)` methods
   - Added `RemoveByPrefix(prefix)` method documentation

2. **Added "Typed Map Operations" subsection**:
   - Documents `GetMap<T>(fieldName)` for retrieving nested `[DynamoDbEntity]` types
   - Documents `TryGetMap<T>(fieldName, out T?)` for safe retrieval
   - Documents `SetMap<T>(fieldName, entity)` for storing typed entities as Map attributes
   - Documents `GetMapsByPrefix<T>(prefix)` and `GetMapsByPrefixWithStrippedKeys<T>(prefix)`

3. **Added "Bulk Operations" subsection**:
   - Documents `SetMany(fields)` for setting multiple AttributeValues
   - Documents `SetManyWithPrefix(prefix, fields)` for prefixed bulk sets
   - Documents `SetMapsWithPrefix<T>(prefix, entities)` for typed bulk sets
   - Documents `RemoveMany(fieldNames)` for bulk removal

4. **Added "Sparse Attribute Pattern Example"** showing complete BalanceTreeNode-style usage:
   - Tree node entity with dynamic children
   - ChildReference nested entity definition
   - Reading, modifying, and saving with optimistic locking

5. **Added "Method Reference" table** with all new methods:
   - Lists return types and descriptions for all 12 new methods

6. **Updated document date** from 2025-12-28 to 2026-01-03

**Reason:** New feature added to enhance `DynamicFieldCollection` with prefix-based accessors, typed Map operations using entity interfaces, and bulk Set/Remove operations for efficient handling of sparse attribute patterns.

---

### File: CHANGELOG.md

**Category:** New Feature Documentation

**Summary:** Added Dynamic Fields Enhancements feature to Unreleased section.

**Changes:**

1. **Added "Dynamic Fields Enhancements" entry** to the Added section:
   - Documents prefix-based accessor methods (`GetFieldNamesByPrefix`, `GetByPrefix`, `GetByPrefixWithStrippedKeys`, `RemoveByPrefix`)
   - Documents typed Map getter/setter using `[DynamoDbEntity]` interfaces (`GetMap<T>`, `TryGetMap<T>`, `SetMap<T>`, `GetMapsByPrefix<T>`, `GetMapsByPrefixWithStrippedKeys<T>`)
   - Documents bulk Set/Remove operations (`SetMany`, `SetManyWithPrefix`, `SetMapsWithPrefix<T>`, `RemoveMany`)
   - Includes usage example for sparse attribute patterns (BalanceTreeNode-style)

**Reason:** New feature added to enhance `DynamicFieldCollection` for efficient handling of sparse attribute patterns like tree nodes with dynamic children.

---

## [2025-12-28]

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added DateOnly and TimeOnly type examples to Entity Definition section.

**Changes:**

1. **Updated "Entity Definition" section** with DateOnly and TimeOnly examples:
   - Added example entity showing DateOnly with default ISO 8601 format
   - Added example showing DateOnly with custom format (`MM/dd/yyyy`)
   - Added example showing TimeOnly with default ISO 8601 format
   - Added example showing TimeOnly with custom format (`h:mm tt`)
   - Added example showing `List<DateOnly>` collection support

2. **Updated document date** from 2025-12-24 to 2025-12-28

**Reason:** New feature added to support native serialization of .NET 6+ `DateOnly` and `TimeOnly` types.

---

### File: docs/reference/FormatSpecifiers.md

**Category:** New Feature Documentation

**Summary:** Added DateOnly and TimeOnly format specifiers documentation.

**Changes:**

1. **Added "DateOnly Format Specifiers" section** after DateTime Format Specifiers:
   - Documents ISO 8601 round-trip format (`o` or `O`) as default
   - Documents short date (`d`), long date (`D`), and custom formats
   - Includes example table with specifiers, descriptions, and outputs
   - Provides code examples for entity definition and query usage

2. **Added "TimeOnly Format Specifiers" section** after DateOnly:
   - Documents ISO 8601 round-trip format (`o` or `O`) as default
   - Documents short time (`t`), long time (`T`), and custom formats
   - Includes example table with specifiers, descriptions, and outputs
   - Provides code examples for entity definition and query usage

**Reason:** New feature added to support native serialization of .NET 6+ `DateOnly` and `TimeOnly` types.

---

### File: CHANGELOG.md

**Category:** New Feature Documentation

**Summary:** Added DateOnly and TimeOnly Serialization feature to Unreleased section.

**Changes:**

1. **Added "DateOnly and TimeOnly Serialization" entry** to the Added section:
   - Documents DateOnly serialization to ISO 8601 date format
   - Documents TimeOnly serialization to ISO 8601 time format
   - Documents UpdateExpressionTranslator support
   - Documents collection support (`List<DateOnly>`, `List<TimeOnly>`)
   - Documents custom format string support via `[DynamoDbAttribute(Format = "...")]`
   - Documents nullable support
   - Includes comprehensive usage examples

**Reason:** New feature added to support native serialization of .NET 6+ `DateOnly` and `TimeOnly` types.

---

## [2025-12-24]

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added Key Condition Shortcuts documentation to Put, Update, and Delete Operations sections, plus a new dedicated section.

**Changes:**

1. **Updated Put Operations section** - Added key condition examples:
   - `.IfNotExists()` builder method for create-only operations
   - `.IfExists()` builder method for update-only operations
   - `KeyCondition.MustNotExist` and `KeyCondition.MustExist` convenience parameters

2. **Updated Update Operations section** - Added key condition examples:
   - `KeyCondition.MustExist` parameter to prevent upserts
   - `.IfExists()` builder method

3. **Updated Delete Operations section** - Added key condition examples:
   - `.IfExists()` builder method
   - `KeyCondition.MustExist` convenience parameter
   - Composite key examples

4. **Added new "Key Condition Shortcuts" section** after Common Patterns:
   - Method/Enum/Generated Condition reference table
   - Usage examples for all three operations
   - KeyCondition enum values documentation

**Reason:** New feature added to simplify common conditional patterns for Put, Update, and Delete operations.

---

### File: docs/core-features/BasicOperations.md

**Category:** New Feature Documentation

**Summary:** Added Key Condition Shortcuts documentation to Put, Update, and Delete Operations sections.

**Changes:**

1. **Added "Key Condition Shortcuts" subsection** under Put Operations:
   - Documents `IfNotExists()`, `IfExists()`, and `WithKeyCondition()` methods
   - KeyCondition enum values table
   - Combining with other conditions example

2. **Added "Key Condition Shortcuts for Updates" subsection** under Update Operations:
   - Documents preventing upserts with `KeyCondition.MustExist`
   - Builder method and convenience parameter examples
   - Composite key examples

3. **Added "Key Condition Shortcuts for Deletes" subsection** under Delete Operations:
   - Documents ensuring item exists before delete
   - Builder method and convenience parameter examples
   - Composite key examples

**Reason:** New feature added to simplify common conditional patterns for Put, Update, and Delete operations.

---

## [2025-12-23]

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added "Empty Expression Handling" subsection under Conditional Filter Patterns to document the new graceful handling of all-skip conditional expressions.

**Changes:**

1. **Added "Empty Expression Handling" subsection** after the Conditional Filter Patterns truth table:
   - Documents that when all conditional clauses evaluate to skip, the filter is gracefully omitted
   - Includes code example showing safe usage with multiple optional filters
   - Explains that this eliminates the need to wrap `.WithFilter()` in conditional checks

**Reason:** New feature added to gracefully handle conditional filter expressions that resolve to empty strings, preventing DynamoDB "Invalid FilterExpression: The expression can not be empty" errors.

---

### File: docs/BREAKING_CHANGES_v1.0.md

**Category:** Breaking Change Documentation

**Summary:** Added "Empty Conditional Expression Handling" as a breaking change (section 3) with migration guidance.

**Changes:**

1. **Updated Overview** to list three breaking changes instead of two
2. **Added new section 3** documenting the behavior change:
   - Previous behavior: DynamoDB error when all conditionals skip
   - New behavior: Operation executes without filter/condition
   - Impact explanation (low impact, but breaking for error-dependent code)
   - Migration guidance for code that relied on catching the error
   - Code examples showing before/after behavior
3. **Updated Summary table** to include the new breaking change

**Reason:** While typically a quality-of-life improvement, this is technically a breaking change for code that relied on the DynamoDB error being thrown (e.g., in catch blocks for validation).

---

## [2025-12-23]

### File: docs/core-features/ExpressionBasedUpdates.md

**Category:** Breaking Change Documentation

**Summary:** Updated Conditional Updates section to document the new `NoUpdate()` method and the change in null handling behavior.

**Changes:**

1. **Renamed "Skip Update with Null False Branch" to "Skip Update with NoUpdate()"**
   - Before: `Name = updateName ? newName : null` (skipped update)
   - After: `Name = updateName ? newName : x.Name.NoUpdate()` (skipped update)

2. **Added "Null Assignment Sets DynamoDB NULL" subsection**
   - Documents that `= null` now sets the attribute to DynamoDB NULL type
   - Previously, `null` in conditional false branch would skip the update

3. **Added "Null vs NoUpdate() vs Remove()" comparison table**
   - `= null` → SET attr = NULL (attribute exists with NULL value)
   - `.NoUpdate()` → No operation (attribute unchanged)
   - `.Remove()` → REMOVE attr (attribute deleted)

4. **Updated all code examples** to use `x.Property.NoUpdate()` instead of `null` for skip behavior

5. **Updated "Important Rules for Conditional Updates"**
   - Changed rule 2 from "Null false branch means skip" to "Use NoUpdate() to skip, null sets NULL"
   - Added rule 4 documenting that `NoUpdate()` throws if called directly

**Reason:** Breaking change in v1.0 - `null` in conditional expressions now consistently sets DynamoDB NULL instead of skipping the update. Use `x.Property.NoUpdate()` for skip behavior.

---

### File: .kiro/steering/fluentdynamodb.md

**Category:** Breaking Change Documentation

**Summary:** Added NoUpdate() documentation and null behavior change to the Update Operations section.

**Changes:**

1. **Added conditional update example with NoUpdate()**
   ```csharp
   Name = shouldUpdate ? newName : x.Name.NoUpdate()
   ```

2. **Added null assignment example**
   ```csharp
   MiddleName = null  // Sets attribute to NULL
   ```

3. **Added "Null vs NoUpdate() vs Remove()" comparison table**

**Reason:** Breaking change in v1.0 - documenting the new NoUpdate() method and consistent null handling.

---

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

### File: .kiro/steering/fluentdynamodb.md

**Category:** New Feature Documentation

**Summary:** Added Automatic Index Projections section documenting the new feature.

**Changes:**

1. **Added "Automatic Index Projections" section** after Index Operations section:
   - Documents automatic entity projection for single-entity tables
   - Documents `ProjectionType` property on index attributes
   - Documents Keys Only projection auto-generation
   - Includes ProjectionType values table

**Reason:** New feature added to support automatic projection types for GSI/LSI indexes.

---

### File: docs/advanced-topics/GlobalSecondaryIndexes.md

**Category:** New Feature Documentation

**Summary:** Added comprehensive documentation for ProjectionType, automatic entity projections, and Keys Only auto-generation.

**Changes:**

1. **Added "GSI with ProjectionType" subsection** under Multiple GSIs:
   - Documents `ProjectionType` property on `[GlobalSecondaryIndex]`
   - Explains ProjectionType values (All, KeysOnly, Include)
   - Clarifies that ProjectionType is metadata only

2. **Added "Automatic Entity Projections for Single-Entity Tables" subsection**:
   - Documents automatic entity type usage for single-entity tables
   - Includes behavior comparison table for single vs multi-entity tables

3. **Added "Keys Only Projection Auto-Generation" subsection**:
   - Documents auto-generated projection record structure
   - Shows generated code example
   - Includes usage pattern for batch-getting full entities
   - Documents keys included for GSI vs LSI

**Reason:** New feature added to support automatic projection types for GSI/LSI indexes.

---

## [2025-12-22]

### Fresh Start - External Sources Synchronized

**Category:** Documentation Reset

**Summary:** This documentation changelog has been truncated to provide a fresh start. All external documentation sources (e.g., fluentdynamodb.dev) have been synchronized with the current state of the repository documentation.

**Reason:** Previous changelog entries have been applied to all derived documentation. Starting fresh reduces file size and improves maintainability while ensuring all documentation sources are in sync.
