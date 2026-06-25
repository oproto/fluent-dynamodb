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

## [2026-07-06]

### Internal Simplification: ComputedFieldMetadata Format Normalization

**Category:** Pattern Update

### File: Oproto.FluentDynamoDb/Metadata/ComputedFieldMetadata.cs

**Before:**
```csharp
public class ComputedFieldMetadata
{
    public string[] SourceProperties { get; set; } = Array.Empty<string>();
    public string Separator { get; set; } = "#";
    public string? Prefix { get; set; }
    public string? PrefixSeparator { get; set; }
}
```

**After:**
```csharp
public class ComputedFieldMetadata
{
    public string[] SourceProperties { get; set; } = Array.Empty<string>();
    public string Format { get; set; } = "{0}";
}
```

**Reason:** Internal simplification — all computed field configurations (Separator, Prefix, PrefixSeparator) are now pre-compiled into a single format string at source-generator time. The runtime only needs `string.Format(Format, values)` to reconstruct computed values, aligning the Update path with the existing Put and Key builder paths. The user-facing `ComputedAttribute` API is unchanged.

---

### New Diagnostic: FDDB090 — Format Placeholder Count Mismatch

**Category:** New Feature Documentation

### File: Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs

**Before:**
No `FDDB090` diagnostic existed.

**After:**
```csharp
// FDDB090 is emitted when an explicit Format placeholder count doesn't match source property count
// Severity: Error
// Message: "Computed property '{0}' has format '{1}' with {2} placeholders but {3} source properties"
```

**Reason:** Compile-time validation of format string correctness. When a user specifies an explicit `Format` on a `ComputedAttribute`, the source generator now verifies that the number of positional placeholders (`{0}`, `{1}`, etc.) matches the number of declared source properties, and emits an error diagnostic if they don't match.

---

## [2026-07-05]

### New Documentation: Computed Field Updates

**Category:** New Feature Documentation

### File: docs/core-features/ComputedFieldUpdates.md

**Description:** Added comprehensive documentation for the computed field update model redesign. Documents the source-property-based update pattern (setting source properties to trigger automatic recomputation of computed field values), update model property exclusions (key properties, extracted-of-keys, source-of-key-computed excluded at compile time), direct assignment as an alternative, and the three new runtime diagnostics (FDDB071, FDDB072, FDDB073). Includes complete examples for GSI key updates via source properties, multiple independent computed fields, conditional updates, and best practices.

**Key Sections:**
- Update Model Property Exclusions — what's included/excluded and why
- Source-Property-Based Updates — automatic recomputation pattern with prefix handling
- Direct Assignment — alternative approach for pre-computed values
- Diagnostics (FDDB071, FDDB072, FDDB073) — causes, messages, and fixes
- Examples — GSI key updates, multiple computed fields, conditional patterns
- Best Practices — prefer source properties, local variables, don't mix approaches

**Before (runtime error):**
```csharp
// Previously, setting key properties threw at runtime
.Set(x => new ProductUpdateModel { Pk = "new-value" })
// Threw InvalidUpdateOperationException at runtime
```

**After (compile-time error):**
```csharp
// Now, key properties are excluded from the update model entirely
// .Set(x => new ProductUpdateModel { Pk = "new-value" })
// ↑ Compile error: ProductUpdateModel does not contain 'Pk'

// Source-property-based update for non-key computed fields:
.Set(x => new ProductUpdateModel { Status = "Active", Region = "US-East" })
// Automatically generates: SET #gsi1pk = :p0 (value: "Active#US-East")
```

**Reason:** New feature enables type-safe computed field updates with automatic recomputation, replacing manual string concatenation and preventing common bugs (partial updates, mixed assignment, entity parameter references).

## [2026-06-23]

### New Documentation: Put Key Prefix Automatic Application

**Category:** Added

### File: docs/core-features/PutKeyPrefixBehavior.md

**Description:** Added documentation explaining automatic key prefix application during Put operations. Covers how Auto mode detects and applies prefixes to key properties, how Value mode always prepends the prefix, how Raw mode passes key values unchanged, computed key exclusion, and per-call `WithKeyMode(KeyInputMode)` overrides. Includes code examples for each mode and migration guidance for existing users.

**Before:**
```csharp
// Manual prefix construction required before Put
var order = new Order
{
    Pk = Order.Keys.Pk(orderId),  // "ORDER#12345"
    Sk = Order.Keys.Sk(lineId),   // "LINE#abc"
    Total = 99.99m
};
await table.Orders.Put(order).PutAsync();
```

**After:**
```csharp
// Auto mode applies prefix automatically during Put serialization
var order = new Order
{
    Pk = orderId,   // Automatically becomes "ORDER#12345"
    Sk = lineId,    // Automatically becomes "LINE#abc"
    Total = 99.99m
};
await table.Orders.Put(order).PutAsync();
```

**Reason:** Put operations now automatically apply configured key prefixes during serialization based on the resolved `KeyInputMode`. This eliminates the most common source of bugs for new library users who forgot to call `Entity.Keys.Pk(value)` before Put operations. Existing code using `Entity.Keys.Pk(value)` continues to work unchanged because Auto mode detects the prefix is already present and passes through.

## [2026-07-03]

### New Documentation: Computed Key Typed Parameter Overloads

**Category:** New Feature Documentation

### File: docs/core-features/ComputedKeyOverloads.md

**Description:** Added comprehensive documentation for the new typed parameter convenience overloads generated for entities with computed keys. Documents when overloads are generated vs. skipped, before/after patterns, parameter type resolution, computed key with prefix handling, both-keys-computed scenarios, consistency across CRUD methods, table-level overloads, and the relationship with KeyInputMode.

### Updated Documentation: KeyInputMode Per-Call Parameter on Generated Accessors

**Category:** New Feature Documentation

### File: docs/core-features/KeyInputMode.md

**Description:** Added "Per-Call KeyInputMode Parameter on Generated Accessors" section documenting when the `KeyInputMode mode` parameter appears on generated accessor methods, its position in the signature, per-call override examples for Auto/Value/Raw modes, propagation to convenience async methods and table-level methods, and interaction with prefix configuration on individual keys.

## [2026-06-19]

### Non-String Key Type Support — Bugfix Clarification

**Category:** Clarification

**Summary:** Added documentation clarifying that non-string key types (enum, int, long, Guid, DateTime, DateOnly, TimeOnly, and nullable value types) are fully supported as partition keys and sort keys when they have no prefix and are not computed. Previously, the source generator produced uncompilable code for these configurations. The fix ensures correct `AttributeValue` construction in generated accessor methods.

### File: docs/core-features/EntityDefinition.md

**Change:** Added "Non-String Key Types" section documenting support for entities with non-string partition/sort key types (enum, int, Guid, DateTime, etc.) without prefixes. Includes examples showing that the source generator correctly handles type-appropriate DynamoDB serialization (numeric types as `N`, string/enum/Guid/date types as `S`).

**Reason:** The source generator previously produced uncompilable code (CS1503 errors) for entities with non-string key types that had no prefix and were not computed. After the bugfix, non-string keys are a first-class supported pattern that should be documented.

### Tautological Exclusion Guard Detection — New Diagnostic (DISC006)

**Category:** Clarification

**Summary:** Added documentation for the new DISC006 diagnostic to the "Overlapping Pattern Resolution" section in `docs/advanced-topics/Discriminators.md`. DISC006 fires when a computed exclusion guard is tautological — identical to the entity's own positive match criterion — which would make `MatchesEntity` always return `false`.

### File: docs/advanced-topics/Discriminators.md

**Change:** Updated the "Compile-Time Diagnostics" table to include DISC006 (Error severity). Added a complete example showing when DISC006 fires (Contains-strategy entity `*#ROLE#*` overlapping with Complex-strategy entity `USER#*#ROLE#*`), an explanation of why this happens, and resolution steps (use StartsWith instead of Contains, use ExactMatch, or redesign key structure).

**Reason:** New source generator bugfix detects tautological exclusion guards at compile time (emitting DISC006 error) instead of silently generating unreachable code. Users encountering this diagnostic need to understand what it means and how to fix their discriminator patterns.

## [2026-06-20]

### Overlapping Discriminator Pattern Resolution — New Feature Documentation

**Category:** Clarification

**Summary:** Added a new "Overlapping Pattern Resolution" section to `docs/advanced-topics/Discriminators.md` documenting the most-specific pattern matching feature for overlapping discriminator patterns on multi-entity tables.

### File: docs/advanced-topics/Discriminators.md

**Change:** Added comprehensive "Overlapping Pattern Resolution" section covering:
- How specificity scoring works (split on `*`, count non-empty literal segments)
- ExactMatch always wins precedence
- Complete Invoice/InvoiceLine hierarchy example without `entity_type` attribute
- Generated exclusion guard code examples
- DISC004 (error), DISC005 (info), and DISC006 (error) compile-time diagnostics
- When overlap resolution does and does not apply

**Reason:** New source generator feature enables automatic disambiguation of overlapping discriminator patterns using compile-time specificity analysis. Users need to understand how the feature works, what diagnostics they may encounter, and how to resolve DISC004 ambiguity errors and DISC006 tautological exclusion errors.

## [2026-06-16]

### MatchesEntity Three-Tier Discrimination — Behavioral Fix

**Category:** Clarification

**Summary:** Added warnings and best practices to `docs/advanced-topics/Discriminators.md` and `docs/advanced-topics/MultiEntityTables.md` documenting that multi-entity tables without discriminator configuration will only check key attribute presence for entity type filtering. Without a discriminator, items from different entity types sharing the same key structure may pass the `MatchesEntity` check, leading to wrong-type hydration.

### File: docs/advanced-topics/Discriminators.md

**Change:** Added "Best Practice 6: Always Configure Discriminators on Multi-Entity Tables" section with a prominent warning block explaining the Tier 3 behavior, code examples showing the risky pattern vs the correct pattern, and guidance that single-entity tables do not need discriminators.

**Reason:** The `MatchesEntity` method now uses a three-tier approach. Tier 3 (multi-entity without discriminator) only checks key attributes — users need to understand this tradeoff and configure discriminators explicitly.

### File: docs/advanced-topics/MultiEntityTables.md

**Change:** Added "Best Practice 7: Always Configure Discriminators" section with a warning block and code example, cross-linking to the Discriminators guide.

**Reason:** Multi-entity table users should be warned at the point where they're configuring multi-entity tables, not only in the discriminator-specific docs.

## [2026-04-30]

### Index Attribute Redesign — Breaking API Change

**Category:** Pattern Update

**Summary:** All documentation files referencing `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` have been updated to use the new `[GsiPartitionKey]`, `[GsiSortKey]`, and `[LsiSortKey]` attributes. This is a breaking API change — the old attributes have been removed entirely.

**Reason:** The old `[GlobalSecondaryIndex]` attribute required `IsPartitionKey = true` or `IsSortKey = true` boolean flags, which were error-prone. The new attributes encode the key role and index type directly in the attribute name, making misconfiguration impossible. New diagnostic codes DYNDB120–DYNDB127 have been added for compile-time validation of index attribute configurations.

### File: docs/advanced-topics/GlobalSecondaryIndexes.md

**Before:**
```csharp
[GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;

[GlobalSecondaryIndex("status-index", IsSortKey = true)]
[DynamoDbAttribute("createdAt")]
public DateTime CreatedAt { get; set; }
```

**After:**
```csharp
[GsiPartitionKey("status-index")]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;

[GsiSortKey("status-index")]
[DynamoDbAttribute("createdAt")]
public DateTime CreatedAt { get; set; }
```

**Reason:** Replaced `[GlobalSecondaryIndex]` with `[GsiPartitionKey]` and `[GsiSortKey]`. Replaced `[LocalSecondaryIndex]` with `[LsiSortKey]`.

### File: docs/reference/AttributeReference.md

**Before:**
```csharp
[GlobalSecondaryIndex("index-name", IsPartitionKey = true)]
[LocalSecondaryIndex("index-name")]
```

**After:**
```csharp
[GsiPartitionKey("index-name")]
[GsiSortKey("index-name")]
[LsiSortKey("index-name")]
```

**Reason:** Replaced `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` sections with `[GsiPartitionKey]`, `[GsiSortKey]`, and `[LsiSortKey]` sections.

### File: docs/QUICK_REFERENCE.md

**Before:**
```csharp
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("gsi1")]
```

**Reason:** Updated GSI/LSI attribute examples to new syntax.

### File: docs/DeveloperGuide.md

**Before:**
```csharp
[GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("status-index")]
```

**Reason:** Updated index attribute examples to new syntax.

### File: docs/reference/AdoptionGuide.md

**Before:**
```csharp
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("gsi1")]
```

**Reason:** Updated index attribute examples to new syntax.

### File: docs/reference/Troubleshooting.md

**Before:**
```csharp
[GlobalSecondaryIndex("index-name", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("index-name")]
```

**Reason:** Updated index attribute examples to new syntax.

### File: docs/core-features/EntityDefinition.md

**Before:**
```csharp
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("gsi1")]
```

**Reason:** Updated GSI/LSI attribute examples to new syntax.

### File: docs/getting-started/FirstEntity.md, docs/getting-started/SingleEntityTables.md

**Before:**
```csharp
[GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
```

**After:**
```csharp
[GsiPartitionKey("status-index")]
```

**Reason:** Updated getting-started examples to new index attribute syntax.

### File: docs/advanced-topics/MultiEntityTables.md, docs/advanced-topics/Discriminators.md

**Before:**
```csharp
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true, DiscriminatorProperty = "entityType")]
```

**After:**
```csharp
[GsiPartitionKey("gsi1", DiscriminatorProperty = "entityType")]
```

**Reason:** Updated multi-entity and discriminator examples to new syntax. Discriminator properties remain on `[GsiPartitionKey]`.

### File: docs/advanced-topics/SchemaValidation.md, docs/advanced-topics/TableCreation.md

**Before:**
```csharp
[GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
[LocalSecondaryIndex("lsi1")]
```

**After:**
```csharp
[GsiPartitionKey("gsi1")]
[LsiSortKey("lsi1")]
```

**Reason:** Updated schema validation and table creation examples. Added new diagnostic codes DYNDB120–DYNDB127 for index attribute validation.

## [2026-03-31]

### File: docs/advanced-topics/FieldLevelSecurity.md

**Category:** API Correction

**Before:**
```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300,  // 5 minutes
    MaxMessagesPerDataKey = 100,
    MaxBytesPerDataKey = 100 * 1024 * 1024  // 100 MB
};
```

**After:**
```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true
};
```

**Reason:** Removed `DefaultCacheTtlSeconds`, `MaxMessagesPerDataKey`, `MaxBytesPerDataKey`, and `CacheEntryCapacity` properties from `AwsEncryptionSdkOptions`. The AWS Encryption SDK for .NET does not support data key caching, so these properties were non-functional. Since the API is unreleased, they were removed entirely rather than deprecated. The `AwsEncryptionSdkOptions` API reference section was also updated to reflect the current class shape. The `EncryptedAttribute.CacheTtlSeconds` doc comment was updated to remove the reference to the deleted `AwsEncryptionSdkOptions.DefaultCacheTtlSeconds`. Troubleshooting section updated to remove suggestion to increase `DefaultCacheTtlSeconds`.

### File: docs/core-features/Configuration.md

**Category:** API Correction

**Before:**
```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300,
    MaxMessagesPerDataKey = 1000
};
```

**After:**
```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true
};
```

**Reason:** Same as above — removed references to deleted `AwsEncryptionSdkOptions` properties.

## [2026-01-20]

### Fresh Start - External Sources Synchronized

**Category:** Documentation Reset

**Summary:** This documentation changelog has been truncated to provide a fresh start. All external documentation sources (e.g., fluentdynamodb.dev) have been synchronized with the current state of the repository documentation.

**Changes Applied:**
- String Comparison Operators (`CompareTo()`) documentation added to Lambda Expressions
- Dynamic Fields Enhancements (prefix-based operations, typed Map operations, bulk operations)
- DateOnly and TimeOnly serialization documentation
- Key Condition Shortcuts for Put, Update, and Delete operations
- Empty Expression Handling documentation
- NoUpdate() method and null behavior change documentation
- SetAt/RemoveAt extension methods for list operations

**Reason:** Previous changelog entries have been applied to all derived documentation. Starting fresh reduces file size and improves maintainability while ensuring all documentation sources are in sync.

## [2025-07-14]

### New Documentation

### File: docs/core-features/KeyInputMode.md

**Description:** Added comprehensive documentation for the new `KeyInputMode` enum and its integration with `FluentDynamoDbOptions`. Documents all four modes (Default, Auto, Value, Raw), configuration options, examples for each mode, and migration guidance for existing users.

**Category:** New Feature Documentation
