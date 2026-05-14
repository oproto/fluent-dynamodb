# Design Document: AWS Encryption SDK Migration

## Overview

This design document covers the migration from the obsolete `AWS.EncryptionSDK` package to the replacement `AWS.Cryptography.EncryptionSDK` package. The current implementation has the correct structure but throws `NotImplementedException` because the actual AWS SDK integration was never completed.

The new package (`AWS.Cryptography.EncryptionSDK` v4.x) has a completely different API surface, generated from Dafny specifications. This design documents the new API patterns and how to integrate them into the existing `AwsEncryptionSdkFieldEncryptor` class.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Application Layer                             │
│  [Encrypted] attribute on entity properties                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              IFieldEncryptor Interface                           │
│  EncryptAsync(plaintext, fieldName, context)                     │
│  DecryptAsync(ciphertext, fieldName, context)                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│           AwsEncryptionSdkFieldEncryptor                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ IKmsKeyResolver │  │ MaterialProviders│  │     ESDK        │  │
│  │ (key lookup)    │  │ (keyring factory)│  │ (encrypt/decrypt)│ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘  │
│                              │                      │            │
│                              ▼                      │            │
│                    ┌─────────────────┐              │            │
│                    │   KMS Keyring   │◄─────────────┘            │
│                    └─────────────────┘                           │
│                              │                                   │
│                              ▼                                   │
│                    ┌─────────────────┐                           │
│                    │ Caching CMM     │ (optional)                │
│                    └─────────────────┘                           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    AWS KMS Service                               │
│  GenerateDataKey / Decrypt                                       │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### Package Dependencies

**Old (to be removed):**
```xml
<PackageReference Include="AWS.EncryptionSDK" Version="[3.0.0,4.0.0)" />
```

**New:**
```xml
<PackageReference Include="AWS.Cryptography.EncryptionSDK" Version="[4.0.0,5.0.0)" />
```

The new package brings transitive dependencies:
- `AWS.Cryptography.MaterialProviders` - Keyring and CMM factories
- `DafnyRuntime` - Runtime support for Dafny-generated code
- `BouncyCastle.Cryptography` - Cryptographic primitives

### New API Surface

The new SDK uses a different pattern:

```csharp
using AWS.Cryptography.EncryptionSDK;
using AWS.Cryptography.MaterialProviders;

// 1. Create Material Providers client (factory for keyrings)
var materialProviders = new MaterialProviders(new MaterialProvidersConfig());

// 2. Create KMS Keyring
var keyring = materialProviders.CreateAwsKmsKeyring(new CreateAwsKmsKeyringInput
{
    KmsKeyId = "arn:aws:kms:us-east-1:123456789012:key/..."
});

// 3. Create ESDK client
var esdk = new ESDK(new AwsEncryptionSdkConfig());

// 4. Encrypt
var encryptOutput = esdk.Encrypt(new EncryptInput
{
    Plaintext = new MemoryStream(plaintextBytes),
    Keyring = keyring,
    EncryptionContext = new Dictionary<string, string> { ... },
    AlgorithmSuiteId = ESDKAlgorithmSuiteId.ALG_AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384
});

// 5. Decrypt
var decryptOutput = esdk.Decrypt(new DecryptInput
{
    Ciphertext = encryptOutput.Ciphertext,
    Keyring = keyring
});
```

### Caching Implementation

The new SDK uses a different caching approach:

```csharp
// Create a cache
var cache = materialProviders.CreateCryptographicMaterialsCache(
    new CreateCryptographicMaterialsCacheInput
    {
        Cache = new CacheType
        {
            Default = new DefaultCache { EntryCapacity = 1000 }
        }
    });

// Create a caching CMM (wraps the keyring)
var cachingCmm = materialProviders.CreateCachingCMM(new CreateCachingCMMInput
{
    UnderlyingCMM = materialProviders.CreateDefaultCryptographicMaterialsManager(
        new CreateDefaultCryptographicMaterialsManagerInput { Keyring = keyring }),
    Cache = cache,
    CacheLimitTtl = 300 // seconds
});

// Use CMM instead of keyring for encryption
var encryptOutput = esdk.Encrypt(new EncryptInput
{
    Plaintext = new MemoryStream(plaintextBytes),
    MaterialsManager = cachingCmm, // Use CMM instead of Keyring
    EncryptionContext = encryptionContext
});
```

### AwsEncryptionSdkFieldEncryptor Changes

The class will be refactored to:

1. **Constructor**: Initialize `MaterialProviders` and `ESDK` clients
2. **Keyring Management**: Create keyrings on-demand based on resolved key ARN
3. **Caching**: Optionally wrap keyrings in caching CMM
4. **Thread Safety**: Ensure thread-safe keyring/CMM creation and caching

```csharp
public sealed class AwsEncryptionSdkFieldEncryptor : IFieldEncryptor
{
    private readonly IKmsKeyResolver _keyResolver;
    private readonly AwsEncryptionSdkOptions _options;
    private readonly MaterialProviders _materialProviders;
    private readonly ESDK _esdk;
    private readonly ICryptographicMaterialsCache? _cache;
    
    // Cache keyrings by key ARN to avoid recreating them
    private readonly ConcurrentDictionary<string, IKeyring> _keyringCache = new();
    
    public AwsEncryptionSdkFieldEncryptor(
        IKmsKeyResolver keyResolver,
        AwsEncryptionSdkOptions? options = null)
    {
        _keyResolver = keyResolver ?? throw new ArgumentNullException(nameof(keyResolver));
        _options = options ?? new AwsEncryptionSdkOptions();
        
        // Initialize SDK clients
        _materialProviders = new MaterialProviders(new MaterialProvidersConfig());
        _esdk = new ESDK(new AwsEncryptionSdkConfig());
        
        // Initialize cache if enabled
        if (_options.EnableCaching)
        {
            _cache = _materialProviders.CreateCryptographicMaterialsCache(
                new CreateCryptographicMaterialsCacheInput
                {
                    Cache = new CacheType
                    {
                        Default = new DefaultCache { EntryCapacity = 1000 }
                    }
                });
        }
    }
    
    private IKeyring GetOrCreateKeyring(string keyArn)
    {
        return _keyringCache.GetOrAdd(keyArn, arn =>
            _materialProviders.CreateAwsKmsKeyring(new CreateAwsKmsKeyringInput
            {
                KmsKeyId = arn
            }));
    }
}
```

## Data Models

### Existing Models (Unchanged)

These models remain unchanged for backward compatibility:

- `IFieldEncryptor` - Interface for field encryption/decryption
- `FieldEncryptionContext` - Context with ContextId, CacheTtlSeconds, IsExternalBlob, EntityId
- `IKmsKeyResolver` - Interface for resolving context to KMS key ARN
- `AwsEncryptionSdkOptions` - Configuration options
- `FieldEncryptionException` - Exception for encryption failures

### Internal Models

```csharp
/// <summary>
/// Internal wrapper for caching CMM with its associated keyring.
/// </summary>
internal sealed class CachedMaterialsManager
{
    public required IKeyring Keyring { get; init; }
    public required ICryptographicMaterialsManager Cmm { get; init; }
    public required string KeyArn { get; init; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Round-trip consistency

*For any* valid plaintext byte array, field name, and encryption context, encrypting the plaintext and then decrypting the result SHALL produce a byte array identical to the original plaintext.

**Validates: Requirements 2.1, 3.1**

### Property 2: Encryption context preservation

*For any* encryption operation with a non-null context ID, the encryption context stored in the ciphertext SHALL contain the field name and context ID, and these values SHALL be retrievable during decryption.

**Validates: Requirements 2.3, 3.2**

### Property 3: Tenant isolation via cache partitioning

*For any* two different context IDs, encryption operations SHALL use independent cache entries, ensuring that data keys cached for one context are never used for another context.

**Validates: Requirements 4.6**

### Property 4: Null key rejection

*For any* encryption or decryption operation where the key resolver returns null or an empty string, the System SHALL throw a FieldEncryptionException with the field name and context ID.

**Validates: Requirements 7.1**

### Property 5: Error wrapping

*For any* exception thrown by the underlying AWS Encryption SDK during encryption or decryption, the System SHALL wrap it in a FieldEncryptionException that preserves the original exception as InnerException and includes the field name, context ID, and key ARN.

**Validates: Requirements 7.4, 7.5**

### Property 6: Data key cache round-trip (DEFERRED)

*For any* valid plaintext, field name, and context ID, when a data key cache is configured, encrypting data SHALL store the data key in the cache, and subsequent encryption operations with the same cache key SHALL use the cached data key instead of calling KMS.

**Validates: Requirements 8.2, 8.3, 8.4**

**Status: DEFERRED** - See "Data Key Cache Deferral" section below.

### Property 7: Data key cache tenant isolation (DEFERRED)

*For any* two different context IDs with a data key cache configured, encryption operations SHALL use different cache keys, ensuring data keys cached for one context are never used for another context.

**Validates: Requirements 8.7**

**Status: DEFERRED** - See "Data Key Cache Deferral" section below.

## Data Key Cache Deferral

### Decision

The pluggable data key cache feature (IDataKeyCache integration into EncryptAsync/DecryptAsync) has been **deferred** to a future enhancement.

### Reason

The AWS Encryption SDK handles data key generation internally and does not expose hooks for injecting cached data keys. Implementing data key caching would require:

1. **Custom envelope encryption** - Bypassing the AWS Encryption SDK to call KMS `GenerateDataKey` directly and use .NET's AES-GCM for encryption
2. **Custom message format** - Defining our own ciphertext format to store the encrypted data key alongside the ciphertext

### Impact of Custom Implementation

If we implemented custom envelope encryption:

- ❌ **No interoperability** - Ciphertext would be incompatible with AWS Encryption SDK in other languages (Java, Python, JavaScript)
- ❌ **Migration complexity** - Switching back to pure SDK would require re-encrypting all data
- ❌ **Security risk** - Rolling custom crypto formats is generally discouraged
- ✅ **Flexible caching** - Could use Redis, DynamoDB, in-memory, etc.

### Current State

This feature has been **removed** from the current implementation. No data key caching code exists in the codebase.

If this feature is needed in the future, it should be implemented via the AWS KMS Hierarchical Keyring or as a separate specification that carefully considers the interoperability tradeoffs.

### Recommended Alternative

For applications requiring reduced KMS API calls, consider:

1. **AWS KMS Hierarchical Keyring** - AWS's recommended solution for data key caching
   - Stores "branch keys" in a DynamoDB table
   - Maintains full AWS Encryption SDK interoperability
   - Requires additional infrastructure setup
   - Should be evaluated in a separate specification

2. **Accept the KMS call overhead** - For most applications, the cost (~$0.03 per 10,000 requests) is acceptable

### Typical Usage Pattern

Most applications use a single KMS key with tenant isolation via encryption context:

```
KMS Master Key (1 per environment)
    │
    ├── generates → Data Key A → encrypts Tenant 1's field (context: tenant-1)
    ├── generates → Data Key B → encrypts Tenant 2's field (context: tenant-2)
    └── generates → Data Key C → encrypts Tenant 3's field (context: tenant-3)
```

Tenant isolation is achieved through:
1. **Encryption context** - Tenant ID is cryptographically bound to ciphertext
2. **Unique data keys** - Each encryption generates a new data key (even with same KMS key)

This pattern works correctly with the current implementation without data key caching.

## Error Handling

### Exception Hierarchy

```
FieldEncryptionException
├── Key resolution failures (null/empty key ARN)
├── KMS access denied errors
├── Encryption context validation failures
├── Algorithm/format errors
└── Unexpected SDK errors (wrapped)
```

### Error Messages

| Scenario | Message Template |
|----------|-----------------|
| Null key ARN | "Key resolver returned null or empty key ARN for context '{contextId}'." |
| KMS access denied | "KMS access denied for key '{keyArn}'. Verify IAM permissions for kms:GenerateDataKey and kms:Decrypt." |
| Context mismatch | "Encryption context validation failed for field '{fieldName}'. Expected context '{expectedContext}', found '{actualContext}'." |
| Encryption failure | "Failed to encrypt field '{fieldName}': {innerMessage}" |
| Decryption failure | "Failed to decrypt field '{fieldName}': {innerMessage}" |

## Testing Strategy

### Dual Testing Approach

This implementation uses both unit tests and property-based tests:

- **Unit tests**: Verify specific examples, edge cases, and error conditions
- **Property-based tests**: Verify universal properties that should hold across all inputs

### Property-Based Testing Framework

**Framework**: FsCheck (via FsCheck.Xunit)

FsCheck is chosen because:
- Mature .NET property-based testing library
- Good xUnit integration
- Supports custom generators for complex types
- Configurable iteration count

**Configuration**: Each property test runs a minimum of 100 iterations.

### Test Categories

#### Unit Tests

1. **Constructor tests**
   - Verify MaterialProviders and ESDK are initialized
   - Verify cache is created when EnableCaching is true
   - Verify cache is null when EnableCaching is false

2. **Key resolution tests**
   - Verify key resolver is called with correct context ID
   - Verify FieldEncryptionException when resolver returns null
   - Verify FieldEncryptionException when resolver returns empty string

3. **Error handling tests**
   - Verify KMS access denied is wrapped correctly
   - Verify encryption context mismatch throws correctly
   - Verify unexpected errors are wrapped with InnerException

4. **Configuration tests**
   - Verify algorithm suite is applied correctly
   - Verify caching limits are respected

#### Property-Based Tests

Each property test is tagged with the format: `**Feature: encryption-sdk-migration, Property {number}: {property_text}**`

1. **Property 1: Round-trip consistency**
   - Generator: Random byte arrays (1-10000 bytes), random field names, random context IDs
   - Assertion: `Decrypt(Encrypt(plaintext)) == plaintext`

2. **Property 2: Encryption context preservation**
   - Generator: Random byte arrays, random field names, random non-null context IDs
   - Assertion: Decrypted output's encryption context contains expected field and context

3. **Property 3: Tenant isolation**
   - Generator: Random byte arrays, two distinct random context IDs
   - Assertion: Encrypting with context A and decrypting with context B fails (or uses different keys)

4. **Property 4: Null key rejection**
   - Generator: Random byte arrays, random field names, resolver that returns null/empty
   - Assertion: FieldEncryptionException is thrown with correct properties

5. **Property 5: Error wrapping**
   - Generator: Random byte arrays, mock SDK that throws various exceptions
   - Assertion: FieldEncryptionException wraps the original exception

### Integration Tests

Integration tests (in separate test project) will:
- Test with real KMS keys in AWS
- Verify CloudTrail audit entries
- Test cross-region key access
- Test key rotation scenarios

## AOT Compatibility Considerations

### Known Issues

The `AWS.Cryptography.EncryptionSDK` package uses:
- `DafnyRuntime` - Generated from Dafny, may have reflection usage
- `BouncyCastle.Cryptography` - Known to have some AOT issues

### Mitigation Strategy

1. **Build-time verification**: Enable trim analyzers and AOT analyzers
2. **Runtime testing**: Test in AOT environment during CI
3. **Documentation**: Document any AOT limitations discovered
4. **Fallback**: If AOT is not possible, document workarounds

### Project Configuration

```xml
<PropertyGroup>
    <IsTrimmable>true</IsTrimmable>
    <IsAotCompatible>true</IsAotCompatible>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

## Implementation Notes

### Thread Safety

- `MaterialProviders` and `ESDK` clients are thread-safe and can be shared
- Keyring cache uses `ConcurrentDictionary` for thread-safe access
- Each encryption/decryption operation is independent

### Memory Management

- `EncryptInput.Plaintext` and `DecryptInput.Ciphertext` use `MemoryStream`
- Streams should be disposed after use
- Consider pooling for high-throughput scenarios

### Performance Considerations

- Keyring creation is expensive; cache keyrings by key ARN
- Data key caching significantly reduces KMS API calls
- Algorithm suite with ECDSA signatures adds ~1ms overhead per operation
