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

## [2026-08-14]

### Unified Keys Class API — Breaking Change Documentation

**Category:** Pattern Update

**Summary:** All documentation files referencing `BuildPk()`/`BuildSk()` or `Keys.Key()` have been updated to use the unified `Pk()`/`Sk()` API. The `Key()` composite method and `BuildPk()`/`BuildSk()` methods have been removed from the generated Keys class. Bare keys (no prefix, no computed) no longer generate passthrough methods.

---

### File: .kiro/steering/fluentdynamodb.md

**Before:**
```csharp
// Generated methods:
Order.Keys.Pk("12345")           // Returns "ORDER#12345"
Order.Keys.Sk("abc")             // Returns "META#abc" (if SK has prefix)
Order.Keys.Key("12345", "abc")   // Returns ("ORDER#12345", "META#abc")

// Computed keys:
Event.Keys.BuildPk(2024, 12, 25)              // Returns "2024#12#25"

// Best practice:
var (pk, sk) = Order.Keys.Key(orderId, lineId);
```

**After:**
```csharp
// Generated methods:
Order.Keys.Pk("12345")           // Returns "ORDER#12345"
Order.Keys.Sk("abc")             // Returns "META#abc" (if SK has prefix)

// Computed keys use same Pk()/Sk() methods:
Event.Keys.Pk(2024, 12, 25)                   // Returns "2024#12#25"

// Best practice — use Pk() and Sk() separately:
var pk = Order.Keys.Pk(orderId);
var sk = Order.Keys.Sk(lineId);
```

**Reason:** The `Key()` composite method and `BuildPk()`/`BuildSk()` methods have been removed. All key construction now flows through unified `Pk()` and `Sk()` methods, which handle prefix-based, computed, and constant key patterns.

---

### File: .kiro/steering/entity-patterns.md

**Before:**
```csharp
// Quick Reference table:
| Key construction | `Entity.Keys.Pk(value)` | `Entity.CreatePk(value)` |
```

**After:**
```csharp
// Quick Reference table:
| Key construction (prefix) | `Entity.Keys.Pk(value)` | `Entity.CreatePk(value)` |
| Key construction (computed) | `Entity.Keys.Pk(a, b, c)` | `Entity.Keys.BuildPk(a, b, c)` |
| Construct PK and SK separately | `Entity.Keys.Pk(...)` + `Entity.Keys.Sk(...)` | `Entity.Keys.Key(pk, sk)` |
```

**Reason:** Added computed key construction pattern and separate PK/SK construction to the Quick Reference. Added "Common Mistakes" entries for `BuildPk`/`BuildSk` and `Keys.Key()`.

---

### File: docs/core-features/BasicOperations.md

**Before:**
```csharp
// Composite Key Patterns section existed with Keys.Key() examples
var (pk, sk) = User.Keys.Key(userId, profileType);
```

**After:**
Section removed entirely — `Keys.Key()` no longer exists.

**Reason:** The `Key()` composite method has been removed from the generated Keys class. Use `Pk()` and `Sk()` independently.

---

### File: docs/core-features/ConstantKeyDetection.md

**Before:**
```csharp
// Composite Key() Method section:
Customer.Keys.Key("cust-123")  // Returns ("CUSTOMER#cust-123", "PROFILE")
AppConfig.Keys.Key()           // Returns ("APP_CONFIG", "SETTINGS")
```

**After:**
Section removed — use `Pk()` and `Sk` independently.

**Reason:** The `Key()` composite method has been removed. Documentation updated to show `Pk()` and `Sk` used independently.

---

### File: DISCUSSION_whats_new_since_1.0.7.md

**Before:**
```csharp
Event.Keys.BuildPk(2024, 12, 25)
Keys.BuildSk(new DateOnly(2024, 3, 15), "electronics")
var (pk, sk) = Customer.Keys.Key("cust-123");
```

**After:**
```csharp
Event.Keys.Pk(2024, 12, 25)
Keys.Sk(new DateOnly(2024, 3, 15), "electronics")
var pk = Customer.Keys.Pk("cust-123");
var sk = Customer.Keys.Sk;  // constant key
```

**Reason:** Updated all key construction examples to unified API. Added new "Unified Keys Class API" section documenting the consolidation.

---

## [2026-07-17]

### SonarQube S6966 False Positive — Transaction Composition Pattern

**Category:** Clarification

### File: docs/reference/Troubleshooting.md

**Description:** Added new "Third-Party Analyzer False Positives" section documenting SonarQube rule S6966 ("Awaitable method should be used") incorrectly firing on transaction composition patterns. When users pass builders to `DynamoDbTransactions.Write.Add(...)`, SonarQube detects that the builder has an async terminal method (`PutAsync()`, `UpdateAsync()`, `DeleteAsync()`) and suggests awaiting it — but the builder is being composed into a transaction, not executed independently. The section explains the false positive, why it's incorrect, and provides `#pragma warning disable S6966` suppression guidance.

**Before:**
No documentation existed for this third-party analyzer interaction.

**After:**
```csharp
// SonarQube S6966 fires here — suppress with pragma
#pragma warning disable S6966
await DynamoDbTransactions.Write
    .Add(table.Transactions.Put(transaction))
    .Add(table.Orders.Update(orderId).Set(x => new OrderUpdateModel { Status = "shipped" }))
    .ExecuteAsync();
#pragma warning restore S6966
```

**Reason:** Users reported SonarQube S6966 warnings when composing operations into transactions via `.Add()`. The warning is a false positive from SonarAnalyzer.CSharp — the analyzer doesn't understand that builder objects passed to `.Add()` are not meant to be awaited individually. This is the same class of false positive reported for EF Core `DbContext.Add` vs `AddAsync`. Documentation added to help users understand and suppress the warning.

---

## [2026-07-12]

### New Diagnostics: FDDB120–FDDB123 — Constant Key Conflict Detection

**Category:** New Feature Documentation

### Files: docs/diagnostics/FDDB/FDDB120.md, FDDB121.md, FDDB122.md, FDDB123.md

**Description:** Added per-code diagnostic documentation pages for the four new constant key detection diagnostics. Each page includes code identifier, severity, message format, description, triggering example, and fix example. Updated `docs/diagnostics/README.md` index to include the new codes.

| Code | Severity | Title |
|------|----------|-------|
| FDDB120 | Error | Constant key conflicts with computed attribute |
| FDDB121 | Error | Prefix not applicable to constant key |
| FDDB122 | Error | Cannot extract from constant key |
| FDDB123 | Error | Empty constant key value |

**Reason:** The constant key detection feature (introduced in this release) adds four new compile-time diagnostics that catch invalid configurations. Each diagnostic halts code generation for the affected entity. Documentation pages enable the `helpLinkUri` on each `DiagnosticDescriptor` to resolve to a useful page at `https://fluentdynamodb.dev/diagnostics/FDDB12x`.

---

## [2026-07-12]

### New Feature Documentation: Constant Key Detection

**Category:** New Feature Documentation

### File: docs/core-features/ConstantKeyDetection.md

**Description:** Added comprehensive documentation for the constant key detection feature. The source generator now detects key properties returning fixed compile-time string values via expression-body (`=>`) or read-only auto-property syntax and propagates the constant through the entire generation pipeline. Documentation covers: detection patterns, Keys class simplification, convenience method simplification, serialization/deserialization behavior, update model exclusion, auto-discriminator derivation, and four new diagnostics (FDDB120–FDDB123).

**Before:**
```csharp
// Manual discriminator configuration required for fixed sort key values
[DynamoDbTable("Customers", DiscriminatorProperty = "sk", DiscriminatorValue = "PROFILE")]
public partial class Customer
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// Convenience methods required passing the constant sort key value
var customer = await table.Customers.Get(customerId, "PROFILE").GetItemAsync();
```

**After:**
```csharp
// Constant key detected automatically — no manual discriminator needed
[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";  // Constant key — expression body
}

// Convenience methods simplified — constant SK injected internally
var customer = await table.Customers.Get(customerId).GetItemAsync();
```

**Reason:** New feature enables concise declaration of fixed key values using C# language constructs (expression-body and read-only auto-property syntax). The source generator auto-derives discriminator patterns, simplifies generated convenience methods, and handles serialization/deserialization correctly for properties that may lack setters. Four new diagnostics (FDDB120–FDDB123) catch invalid configurations at compile time.

---

## [2026-06-29]

### Fix: Update Method Parameter Ordering in KeyInputMode Documentation

**Category:** API Correction

### File: docs/core-features/KeyInputMode.md

**Before:**
```csharp
// Generated accessor signature
public GetItemRequestBuilder<Order> Get(
    string pK, 
    string sK, 
    KeyInputMode mode = KeyInputMode.Default)
```

(No clarification that Update methods have a different parameter order due to pre-existing KeyCondition parameter)

**After:**
```csharp
// Generated Get/Delete/ConditionCheck accessor signature
public GetItemRequestBuilder<Order> Get(
    string pK, 
    string sK, 
    KeyInputMode mode = KeyInputMode.Default)

// Generated Update accessor signature (KeyCondition before KeyInputMode)
public OrderUpdateBuilder Update(
    string pK,
    string sK,
    KeyCondition keyCondition = KeyCondition.None,
    KeyInputMode mode = KeyInputMode.Default)
```

**Reason:** The "Parameter Position" section only showed the Get signature, implying `KeyInputMode` is always the parameter immediately after keys. For `Update` methods, `KeyCondition` was the pre-existing optional parameter and must come before `KeyInputMode` for backwards compatibility. Added the Update signature to clarify this distinction.

---

## [2026-06-29]

### New Feature Documentation: Computed Field Format Specifiers

**Category:** New Feature Documentation

### File: docs/core-features/ComputedFieldFormatSpecifiers.md

**Description:** Added comprehensive documentation for .NET format specifier support in computed field format strings. Documents usage with DateOnly (`{0:yyyy-MM-dd}`), integer zero-padding (`{0:D4}`), and enum formatting (`{0:G}`). Covers format specifier precedence rules, source property `DynamoDbAttribute.Format` fallback behavior, `CultureInfo.InvariantCulture` usage, and backwards compatibility.

**Before:**
```csharp
// Format specifiers in computed fields were not supported — silently broken
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("EventDate", "Category", Format = "{0}#{1}")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("eventDate")]
    public DateOnly EventDate { get; set; }

    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}
// Produced: "03/15/2024#electronics" (locale-dependent ToString())
```

**After:**
```csharp
// Format specifiers now work correctly across all operation paths
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("EventDate", "Category", Format = "{0:yyyy-MM-dd}#{1}")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("eventDate")]
    public DateOnly EventDate { get; set; }

    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}
// Produces: "2024-03-15#electronics" (consistent across Put, Update, and Key builder paths)
```

**Reason:** New format specifier support for computed field format strings. Typed values are now passed directly to `string.Format` with `CultureInfo.InvariantCulture` when format specifiers are present, enabling correct formatting via `IFormattable` implementations. Additionally, source properties with `[DynamoDbAttribute(Format = "...")]` now serve as a fallback format specifier when the computed format placeholder has no explicit specifier.

---

## [2026-06-28]

### Breaking Change: Async KMS Key Resolver and Per-Property Key Alias Support

**Category:** Pattern Update

**Summary:** The `IKmsKeyResolver` interface has been converted from synchronous to asynchronous, the `[Encrypted]` attribute gains a `KeyAlias` property for per-property key selection, and the encryption pipeline now threads key alias through `FieldEncryptionContext` to `ResolveKeyIdAsync`. This is a **breaking change** — all `IKmsKeyResolver` implementations must be updated.

---

### File: docs/advanced-topics/FieldLevelSecurity.md

#### IKmsKeyResolver Interface — Breaking Change

**Before:**
```csharp
public interface IKmsKeyResolver
{
    string ResolveKeyId(string? contextId);
}
```

**After:**
```csharp
public interface IKmsKeyResolver
{
    Task<string> ResolveKeyIdAsync(
        string? contextId,
        string? keyAlias = null,
        CancellationToken cancellationToken = default);
}
```

**Reason:** The synchronous `ResolveKeyId` method was the only blocking call in an otherwise fully-async encryption pipeline, forcing multi-tenant implementations into anti-patterns (preloading all mappings, blocking on async contexts). The new async method also accepts a `keyAlias` parameter for per-property key selection and a `CancellationToken` for cooperative cancellation.

---

#### [Encrypted] Attribute — New KeyAlias Property

**Before:**
```csharp
[Encrypted]
[DynamoDbAttribute("ssn")]
public string Ssn { get; set; } = string.Empty;

[Encrypted]
[DynamoDbAttribute("accountNumber")]
public string AccountNumber { get; set; } = string.Empty;
// All encrypted properties use the same KMS key
```

**After:**
```csharp
[Encrypted(KeyAlias = "pii")]
[DynamoDbAttribute("ssn")]
public string Ssn { get; set; } = string.Empty;

[Encrypted(KeyAlias = "financial")]
[DynamoDbAttribute("accountNumber")]
public string AccountNumber { get; set; } = string.Empty;
// Different properties can use different KMS keys based on data classification
```

**Reason:** Per-property key alias support enables different encrypted fields on the same entity to use different KMS keys based on data classification (e.g., PII vs. financial data). The `KeyAlias` property defaults to `null`; when omitted, the resolver falls through to context-based or default key resolution.

---

#### DefaultKmsKeyResolver Constructor — New aliasKeyMap Parameter

**Before:**
```csharp
var resolver = new DefaultKmsKeyResolver(
    defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/default-key",
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key",
        ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key"
    });
```

**After:**
```csharp
var resolver = new DefaultKmsKeyResolver(
    defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/default-key",
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key",
        ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key"
    },
    aliasKeyMap: new Dictionary<string, string>
    {
        ["pii"] = "arn:aws:kms:us-east-1:123456789012:key/pii-key",
        ["financial"] = "arn:aws:kms:us-east-1:123456789012:key/financial-key"
    });
```

**Reason:** The new `aliasKeyMap` parameter enables mapping key aliases (declared on `[Encrypted(KeyAlias = "...")]`) to specific KMS key ARNs. Resolution priority is: aliasKeyMap → contextKeyMap → defaultKeyId. Both maps use case-sensitive lookups.

---

#### FieldEncryptionContext — New KeyAlias Property

**Before:**
```csharp
public class FieldEncryptionContext
{
    public string? ContextId { get; init; }
    public int CacheTtlSeconds { get; init; } = 300;
    public bool IsExternalBlob { get; init; }
    public string? EntityId { get; init; }
}
```

**After:**
```csharp
public class FieldEncryptionContext
{
    public string? ContextId { get; init; }
    public string? KeyAlias { get; init; }
    public int CacheTtlSeconds { get; init; } = 300;
    public bool IsExternalBlob { get; init; }
    public string? EntityId { get; init; }
}
```

**Reason:** The `KeyAlias` property carries the data classification alias from the `[Encrypted]` attribute through the pipeline to `ResolveKeyIdAsync`. The source generator populates this from `[Encrypted(KeyAlias = "...")]`; when omitted, it defaults to `null`.

---

#### Migration Guide for Existing IKmsKeyResolver Implementations

**Before (custom implementation):**
```csharp
public class MyTenantKeyResolver : IKmsKeyResolver
{
    public string ResolveKeyId(string? contextId)
    {
        // Synchronous lookup
        return _tenantKeyMap[contextId ?? "default"];
    }
}
```

**After (custom implementation):**
```csharp
public class MyTenantKeyResolver : IKmsKeyResolver
{
    public async Task<string> ResolveKeyIdAsync(
        string? contextId,
        string? keyAlias = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Can now use async operations (database lookups, vault calls, etc.)
        if (keyAlias is not null && _aliasMap.TryGetValue(keyAlias, out var aliasKey))
            return aliasKey;

        return await _tenantKeyService.GetKeyAsync(contextId ?? "default", cancellationToken);
    }
}
```

**Reason:** All `IKmsKeyResolver` implementations must migrate from `ResolveKeyId` to `ResolveKeyIdAsync`. The new method signature enables true async key resolution (e.g., from databases, secrets managers, or external APIs), per-property key selection via `keyAlias`, and cooperative cancellation via `CancellationToken`.

---

## [2026-06-27]

### New Feature Documentation: Named Blob Providers

**Category:** New Feature Documentation

### File: docs/core-features/NamedBlobProviders.md

**Description:** Added comprehensive documentation for the Named Blob Providers feature, which extends `FluentDynamoDbOptions` to support registering multiple `IBlobStorageProvider` instances by name and extends the `[BlobStorage]` attribute with an optional `Provider` property for per-property blob backend routing.

Documentation covers:
- Motivation (multiple blob backends per entity)
- `[BlobStorage(Provider = "name")]` attribute usage with examples
- `FluentDynamoDbOptions` registration patterns (default + named providers)
- `GetBlobProvider(string? name)` resolution behavior and error scenarios
- Backwards compatibility (existing entities work unchanged)
- Complete end-to-end example with multiple providers

### New Attribute Property: `BlobStorageAttribute.Provider`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Provider` | `string?` | `null` | Name of the blob storage provider to use. When null, the default provider is used. |

**Before:**
```csharp
// Single provider for all blob properties — no per-property routing
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"));

[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [BlobStorage]
    [DynamoDbAttribute("content")]
    public Stream Content { get; set; } = Stream.Null;

    [BlobStorage]
    [DynamoDbAttribute("thumbnail")]
    public Stream Thumbnail { get; set; } = Stream.Null;
}
// Both Content and Thumbnail use the same S3 bucket
```

**After:**
```csharp
// Multiple named providers for different blob properties
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(new S3BlobProvider(s3Client, "default-bucket"))
    .WithBlobStorage("images", new S3BlobProvider(s3Client, "images-bucket"))
    .WithBlobStorage("documents", new S3BlobProvider(s3Client, "docs-bucket"));

[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [BlobStorage(Provider = "documents")]
    [DynamoDbAttribute("content")]
    public Stream Content { get; set; } = Stream.Null;

    [BlobStorage(Provider = "images")]
    [DynamoDbAttribute("thumbnail")]
    public Stream Thumbnail { get; set; } = Stream.Null;
}
// Content routes to "docs-bucket", Thumbnail routes to "images-bucket"
```

**Reason:** New feature enables per-property blob provider routing. Entities can now store different blob properties in different backends (e.g., images in one S3 bucket, documents in another). The `Provider` property on `[BlobStorage]` specifies which named provider to use; when omitted, the default provider is used, preserving full backwards compatibility.

---

## [2026-06-27]

### Removed Per-Call IBlobStorageProvider Examples from FluentResults Documentation

**Category:** Pattern Update

### File: docs/core-features/FluentResults.md

**Description:** Removed four code examples that showed passing `IBlobStorageProvider` directly to FluentResults terminal methods. These overloads were removed as part of the hydration path consolidation (see CHANGELOG.md `[Unreleased]` → `### Removed`). Blob storage is now configured exclusively via `FluentDynamoDbOptions.WithBlobStorage(...)` at table construction time.

**Before:**
```csharp
// With blob storage support
var result = await table.Users.Get(userId)
    .GetItemAsyncResult(blobProvider);

// With blob storage
var result = await table.Users.Put(user).PutAsyncResult(blobProvider);

// With blob storage
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .ToListAsyncResult(blobProvider);

// With blob storage
var result = await table.Logs.Scan()
    .ToListAsyncResult(blobProvider);
```

**After:**
```csharp
// All terminal methods resolve the blob provider automatically from options
var result = await table.Users.Get(userId).GetItemAsyncResult();
var result = await table.Users.Put(user).PutAsyncResult();
var result = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .ToListAsyncResult();
var result = await table.Logs.Scan().ToListAsyncResult();
```

**Reason:** The `IBlobStorageProvider` parameter overloads on `GetItemAsyncResult`, `PutAsyncResult`, and `ToListAsyncResult` have been removed. Blob storage is now configured at table construction time via `new FluentDynamoDbOptions().WithBlobStorage(provider)`. Documentation updated to reflect the only supported pattern.

---

## [2026-06-27]

### New Feature Documentation: Schema Versioning Attribute

**Category:** New Feature Documentation

### File: docs/advanced-topics/SchemaVersioning.md

**Description:** Added comprehensive documentation for the new `FluentDynamoDbSchemaVersionAttribute` assembly-level attribute, which provides a schema versioning mechanism that decouples generated code shapes from the NuGet package version. Consumers declare a target schema version, and the source generator uses that declaration to determine which code shape to emit.

Documentation covers:
- Purpose and motivation (graceful generated code evolution)
- Consumer usage with `[assembly: FluentDynamoDbSchemaVersion(1, 0)]`
- Versioning semantics (major = breaking changes, minor = additive-only)
- Support window policy (N and N-1 major versions)
- Migration guidance template for future version bumps
- All seven new diagnostic codes (FDDB110–FDDB116)

### New Diagnostics: FDDB110–FDDB116

| Code | Severity | Description |
|------|----------|-------------|
| FDDB110 | Warning | Assembly does not declare `[FluentDynamoDbSchemaVersion]` — defaults to 1.0 |
| FDDB111 | Error | Declared schema version is below the minimum supported version |
| FDDB112 | Error | Declared schema version is above the current (unrecognized future version) |
| FDDB113 | Info | Declared version is older but still supported — upgrade available |
| FDDB114 | Error | Major version must be at least 1 |
| FDDB115 | Error | Minor version must be at least 0 |
| FDDB116 | Error | Multiple `[FluentDynamoDbSchemaVersion]` attributes detected |

**Before:**
```csharp
// No schema version declaration — consumer has no explicit contract with the
// source generator about which code shape they target. Generated code may
// change silently on NuGet package upgrade.
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

**After:**
```csharp
// Explicit schema version contract — consumer declares which generated code
// shape they target. Generator emits compatible code and diagnostics guide
// migration when needed.
[assembly: FluentDynamoDbSchemaVersion(1, 0)]

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

**Reason:** New schema versioning mechanism enables graceful generated code evolution. Consumers can now migrate at their own pace by bumping their declared version when ready to adopt new code shapes, rather than being forced to adapt on NuGet package upgrade. The attribute acts as a contract: the generator emits code compatible with the declared version, and consumers use only APIs from that version.

---

## [2026-06-26]

### New Feature Documentation: Centralized Diagnostics Reference

**Category:** New Feature Documentation

**Description:** Added `docs/diagnostics/` directory containing structured documentation for all 103 diagnostic codes emitted by the source generator. Organized into five prefix subdirectories:
- `DYNDB/` — Core entity validation (62 codes)
- `FDDB/` — Table/index generation (25 codes)
- `PROJ/` — Projection model validation (8 codes)
- `DISC/` — Discriminator configuration (6 codes)
- `SEC/` — Security and package dependencies (2 codes)

Each code has a dedicated markdown page with: code identifier, severity, message format, description, triggering example, and fix example.

An index page at `docs/diagnostics/README.md` provides grouped tables with links to all per-code pages.

**Reason:** The fluentdynamodb.dev website team can serve per-code diagnostic pages at the URL pattern `https://fluentdynamodb.dev/diagnostics/{CODE}` (e.g., `https://fluentdynamodb.dev/diagnostics/DYNDB001`). This matches the `helpLinkUri` now set on all `DiagnosticDescriptor` definitions, making diagnostics clickable directly from the IDE error list.

---

## [2026-07-07]

### New Diagnostics: FDDB100–FDDB103 — Unified Key Format & Discriminator Conflict Detection

**Category:** New Feature Documentation

### File: docs/advanced-topics/Discriminators.md

**Description:** Added documentation for four new compile-time diagnostics that detect conflicts and redundancy in key format and discriminator pattern configurations. These diagnostics are emitted by the source generator during entity analysis.

---

#### FDDB100 — PrefixFormatConflict (Error)

**Before:**
```csharp
// No compile-time detection — runtime mismatch between prefix and format
[PartitionKey(Prefix = "ORDER")]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "TENANT#{0}#{1}")]
public string Pk { get; set; } = string.Empty;
// Key prefix says "ORDER#..." but format produces "TENANT#..." — silent contradiction
```

**After:**
```csharp
// FDDB100 Error: Property 'Pk' has Prefix='ORDER' (expecting format to start with 'ORDER#')
// but ComputedAttribute.Format='TENANT#{0}#{1}' does not match
[PartitionKey(Prefix = "ORDER")]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "TENANT#{0}#{1}")]
public string Pk { get; set; } = string.Empty;

// Fix: Either remove the Prefix or align the Format
[PartitionKey(Prefix = "ORDER")]
[DynamoDbAttribute("pk")]
[Computed("CustomerId", "OrderId", Format = "ORDER#{0}#{1}")]
public string Pk { get; set; } = string.Empty;
```

**Reason:** FDDB100 detects when a key property's explicit `Prefix` conflicts with an explicit `ComputedAttribute.Format` that doesn't start with `"{Prefix}{Separator}"`. This prevents silent runtime mismatches between key building and discriminator logic.

---

#### FDDB101 — DiscriminatorKeyFormatConflict (Error)

**Before:**
```csharp
// No compile-time detection — explicit discriminator silently disagrees with key shape
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "USER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Key produces "ORDER#..." but discriminator expects "USER#..." — incorrect filtering
}
```

**After:**
```csharp
// FDDB101 Error: Entity 'Order' specifies DiscriminatorPattern on attribute 'sk' as 'USER#*'
// but the key format derives pattern 'ORDER#*'
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "USER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// Fix: Align the discriminator with the key format, or remove it (auto-derived)
[DynamoDbTable("orders")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derives DiscriminatorPattern = "ORDER#*" from key format
}
```

**Reason:** FDDB101 detects when an explicit `DiscriminatorPattern` on `DynamoDbTableAttribute` contradicts the pattern that would be derived from the referenced key property's format. This prevents incorrect entity filtering in multi-entity tables.

---

#### FDDB102 — OverlappingAutoDerivedPatterns (Warning)

**Before:**
```csharp
// No warning — overlapping auto-derived patterns silently resolved by exclusion guards
[DynamoDbTable("shared")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Derived: "ORDER#*"
}

[DynamoDbTable("shared")]
public partial class OrderLine
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed("OrderId", "LineId", Format = "ORDER#{0}#LINE#{1}")]
    public string Sk { get; set; } = string.Empty;
    // Derived: "ORDER#*#LINE#*" — overlaps with Order's "ORDER#*"
}
```

**After:**
```csharp
// FDDB102 Warning: Entities 'Order' and 'OrderLine' have overlapping auto-derived patterns
// 'ORDER#*' and 'ORDER#*#LINE#*' on attribute 'sk' — consider adding more specificity
// to key formats

// Exclusion guards are still generated (warning is advisory).
// Fix: Add more specificity to the less-specific entity's key format
[DynamoDbTable("shared")]
public partial class Order
{
    [SortKey(Prefix = "ORDER_META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Derived: "ORDER_META#*" — no longer overlaps with "ORDER#*#LINE#*"
}
```

**Reason:** FDDB102 warns when two entities on the same table both have auto-derived discriminator patterns where one is a superset of the other. The source generator still generates correct exclusion guards, but the warning encourages clearer key design. Only emitted when both patterns are auto-derived (not explicit).

---

#### FDDB103 — RedundantExplicitDiscriminator (Info)

**Before:**
```csharp
// Explicit discriminator is specified but matches what would be auto-derived
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Developer manually specified what the generator would derive anyway
}
```

**After:**
```csharp
// FDDB103 Info: Entity 'Order' specifies DiscriminatorPattern='ORDER#*' which is
// automatically derivable from the key format — the explicit specification can be removed

// Simplified (remove redundant explicit discriminator):
[DynamoDbTable("orders")]
public partial class Order
{
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derives DiscriminatorPattern = "ORDER#*" from sort key format
}
```

**Reason:** FDDB103 informs the developer that their explicit `DiscriminatorPattern` exactly matches the auto-derived pattern from the key format. The explicit specification is redundant and can be removed to simplify the entity definition. This is informational only — no behavior change.

---

### Auto-Derivable DiscriminatorPattern from Key Format

**Category:** New Feature Documentation

### File: docs/advanced-topics/Discriminators.md

**Description:** `DiscriminatorPattern` is now automatically derivable from key format configurations. When a key property has a prefix or computed format, the source generator derives the discriminator pattern by replacing `{N}` placeholders with `*` wildcards. This eliminates the need to manually specify `DiscriminatorPattern` in most cases.

**Before:**
```csharp
// Developer must manually keep discriminator in sync with key format
[DynamoDbTable("orders", DiscriminatorProperty = "sk", DiscriminatorPattern = "ORDER#*")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

**After:**
```csharp
// Generator auto-derives discriminator from key format — no manual specification needed
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    // Auto-derived: NormalizedKeyFormat = "ORDER#{0}"
    // Auto-derived: DiscriminatorPattern = "ORDER#*" (on "sk" attribute)
    // Sort key preferred over partition key for discrimination
}
```

**Reason:** The key format string already describes the shape of a key value (e.g., `"ORDER#{0}"`). By replacing placeholders with `*`, the discriminator pattern (`"ORDER#*"`) is derived automatically. This eliminates redundancy, prevents drift between key definitions and discriminator logic, and reduces boilerplate. Sort keys are preferred over partition keys for discrimination (single-table designs typically discriminate on SK). Explicit discriminators remain supported for cases where discrimination cannot be inferred from key format.

---

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
