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
