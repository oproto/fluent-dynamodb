---
title: "Field-Level Security"
category: "advanced-topics"
order: 50
keywords: ["security", "encryption", "sensitive", "kms", "logging", "redaction", "multi-tenant"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Field-Level Security

# Field-Level Security

Protect sensitive data in your DynamoDB entities through logging redaction and optional KMS-based encryption. This guide covers both mechanisms and how to use them together.

## Overview

Oproto.FluentDynamoDb provides two complementary security features:

1. **Logging Redaction** (Built-in) - Exclude sensitive field values from log output
2. **Field Encryption** (Optional) - Encrypt fields at rest using AWS KMS

Both features use simple attributes and integrate seamlessly with the source generator.

## Table of Contents

- [Logging Redaction](#logging-redaction)
- [Field Encryption](#field-encryption)
- [Multi-Context Encryption](#multi-context-encryption)
- [Combined Security Features](#combined-security-features)
- [Integration with Blob Storage](#integration-with-blob-storage)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Logging Redaction

### Overview

The `[Sensitive]` attribute marks fields that should be excluded from logging output. This is useful for compliance with data protection regulations like GDPR, HIPAA, or PCI-DSS.

### Basic Usage

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    public string UserId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    [Sensitive]  // Redacted from logs
    public string Email { get; set; } = string.Empty;
    
    [Sensitive]  // Redacted from logs
    public string PhoneNumber { get; set; } = string.Empty;
}
```

### How It Works

When logging is enabled, sensitive field values are replaced with `[REDACTED]`:

```csharp
// Log output:
// { UserId: "user-123", Name: "John Doe", Email: "[REDACTED]", PhoneNumber: "[REDACTED]" }
```

The field name is preserved for debugging, but the value is hidden.

### What Gets Redacted

The `[Sensitive]` attribute affects:
- LINQ expression logging (query and filter expressions)
- String-based expression logging
- Query parameter logging
- Query results logging
- Put operation logging
- Update operation logging
- Error messages containing entity data
- All diagnostic output from `IDynamoDbLogger`

### Redaction in LINQ Expressions

When using LINQ expressions, sensitive property values are automatically redacted:

```csharp
var email = "user@example.com";
var ssn = "123-45-6789";

await table.Query<User>()
    .Where(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Email == email && x.SocialSecurityNumber == ssn)
    .ToListAsync();

// Log output:
// Filter expression: email = :p0 AND ssn = :p1
// Parameters: { :p0 = [REDACTED], :p1 = [REDACTED] }
```

### Installation

No additional packages required - logging redaction is built into the core library.

```bash
dotnet add package Oproto.FluentDynamoDb
```

---

## Field Encryption

### Overview

The `[Encrypted]` attribute marks fields for encryption at rest using AWS KMS. Encrypted data is stored in DynamoDB as binary (B) attribute type using the AWS Encryption SDK message format.

### Installation

Field encryption requires the optional encryption package:

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

This package includes:
- AWS Encryption SDK integration
- KMS keyring support
- Data key caching
- Encryption context management

### Basic Usage

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("CustomerData")]
public partial class CustomerData
{
    [PartitionKey]
    public string CustomerId { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    [Encrypted]  // Encrypted at rest
    [Sensitive]  // Also redacted from logs
    public string SocialSecurityNumber { get; set; } = string.Empty;
    
    [Encrypted]
    [Sensitive]
    public string CreditCardNumber { get; set; } = string.Empty;
}
```

### Setup and Configuration

#### 1. Configure KMS Key Resolution

```csharp
using Oproto.FluentDynamoDb.Encryption.Kms;

// Load KMS key ARNs from secure configuration (NOT hardcoded!)
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: configuration["Kms:DefaultKeyArn"],
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = configuration["Kms:TenantA:KeyArn"],
        ["tenant-b"] = configuration["Kms:TenantB:KeyArn"]
    });
```

**Security Note:** Never hardcode KMS key ARNs in source code. Always load from secure configuration, environment variables, or a secrets manager.

#### 2. Configure Encryption Options

```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300,  // 5 minutes
    MaxMessagesPerDataKey = 100,
    MaxBytesPerDataKey = 100 * 1024 * 1024  // 100 MB
};
```

#### 3. Create Field Encryptor

```csharp
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, encryptorOptions);
```

#### 4. Configure FluentDynamoDbOptions and Pass to Table

```csharp
using Oproto.FluentDynamoDb;

var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);

var table = new CustomerDataTable(dynamoClient, "customers", options);
```

#### Complete Setup Example

Here's a complete example showing all the steps together:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Encryption.Kms;

// 1. Create DynamoDB client
var dynamoClient = new AmazonDynamoDBClient();

// 2. Configure key resolver (load from secure configuration!)
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: configuration["Kms:DefaultKeyArn"]);

// 3. Configure encryption options
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300
};

// 4. Create encryptor
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, encryptorOptions);

// 5. Configure FluentDynamoDbOptions with encryption
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);

// 6. Create table with options
var table = new CustomerDataTable(dynamoClient, "customers", options);
```

### How It Works

1. **Encryption**: Before storing in DynamoDB, the source generator calls `IFieldEncryptor.EncryptAsync()`
2. **Storage**: Encrypted data is stored as Binary (B) attribute type in AWS Encryption SDK format
3. **Decryption**: When reading from DynamoDB, the source generator calls `IFieldEncryptor.DecryptAsync()`
4. **Transparency**: Your application code works with plaintext - encryption/decryption is automatic

### Encryption Format

Encrypted fields use the AWS Encryption SDK message format, which includes:
- Algorithm suite identifier
- Encrypted data key(s)
- Initialization vector (IV)
- Encrypted content
- Authentication tag
- Digital signature (for key commitment)

This format is:
- Industry-standard and interoperable with other AWS services
- Includes built-in integrity checking
- Supports algorithm agility
- Prevents key substitution attacks (key commitment)

---

## Multi-Context Encryption

### Overview

Multi-context encryption allows different encryption keys for different contexts (tenants, customers, regions, etc.). This is essential for:
- Multi-tenant applications
- Data residency requirements
- Customer-managed keys
- Regulatory compliance

### Context Flow

The encryption context is passed at runtime, not hardcoded in attributes:

```csharp
// Option 1: Per-operation context (recommended - most explicit)
await customerTable.PutItem(customerData)
    .WithEncryptionContext("tenant-123")
    .ExecuteAsync();

// Option 2: Ambient context (for middleware scenarios)
EncryptionContext.Current = "tenant-123";
await customerTable.PutItem(customerData).ExecuteAsync();
```

### Manual Encryption in Queries

For querying encrypted fields, you must manually encrypt the query parameters. This is necessary because automatic encryption would break non-equality operations like range queries and `begins_with`.

#### When to Use Manual Encryption

**Use manual encryption for:**
- ✅ Equality comparisons (`==`)
- ✅ IN queries

**Do NOT use manual encryption for:**
- ❌ Range queries (`>`, `<`, `>=`, `<=`, `BETWEEN`)
- ❌ String operations (`begins_with`, `contains`)
- ❌ Numeric operations

**Why?** Encrypted values are opaque ciphertext - they don't preserve ordering or string relationships.

#### Encrypt Method (LINQ Expressions)

Use `table.Encrypt()` directly in LINQ expressions:

```csharp
[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("ssn")]
    [Encrypted]
    [Sensitive]
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

// Set ambient encryption context (same pattern as Put/Get operations)
EncryptionContext.Current = "tenant-123";

// Encrypt value in LINQ expression
var ssn = "123-45-6789";
var users = await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => x.SocialSecurityNumber == table.Encrypt(ssn, "SocialSecurityNumber"))
    .ToListAsync();
```

#### EncryptValue Helper (Pre-Encryption)

Use `table.EncryptValue()` to encrypt values before the query:

```csharp
// Set ambient encryption context
EncryptionContext.Current = "tenant-123";

// Pre-encrypt the value
var ssn = "123-45-6789";
var encryptedSsn = table.EncryptValue(ssn, "SocialSecurityNumber");

// Use encrypted value in query
var users = await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => x.SocialSecurityNumber == encryptedSsn)
    .ToListAsync();
```

#### String-Based Expressions

Manual encryption also works with string-based expressions:

```csharp
// With format strings
EncryptionContext.Current = "tenant-123";
await table.Query()
    .Where("pk = {0}", userId)
    .WithFilter("ssn = {0}", table.Encrypt(ssn, "SocialSecurityNumber"))
    .ExecuteAsync();

// With named parameters
EncryptionContext.Current = "tenant-123";
await table.Query()
    .Where("pk = :pk")
    .WithValue(":pk", userId)
    .WithFilter("ssn = :ssn")
    .WithValue(":ssn", table.Encrypt(ssn, "SocialSecurityNumber"))
    .ExecuteAsync();
```

#### Encryption Context

Manual encryption uses the same ambient `EncryptionContext.Current` pattern as Put/Get operations:

```csharp
// Set context before encryption
EncryptionContext.Current = "tenant-123";

// All encryption operations in this async flow use the context
var encryptedValue = table.Encrypt(value, fieldName);
await table.PutItem(entity).ExecuteAsync();
await table.Query<User>()
    .WithFilter<User>(x => x.EncryptedField == table.Encrypt(value, "EncryptedField"))
    .ToListAsync();

// Context automatically cleared when async flow completes
```

#### Error Handling

If encryption is not configured, a clear error is thrown:

```csharp
try
{
    var encrypted = table.Encrypt(value, "FieldName");
}
catch (InvalidOperationException ex)
{
    // "Cannot encrypt value: IFieldEncryptor not configured. 
    //  Call options.WithEncryption(encryptor) when creating your table."
}
```

**Solution**: Configure encryption using `FluentDynamoDbOptions`:

```csharp
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);
var table = new SecretsTable(client, "secrets", options);
```

#### Important Notes

- Manual encryption is explicit - you control when encryption happens
- Use ambient `EncryptionContext.Current` for context (same as Put/Get)
- Only use for equality comparisons
- Encrypted values cannot be used in range queries or string operations
- Combine with `[Sensitive]` to redact encrypted values from logs
- The `Encrypt()` and `EncryptValue()` methods are equivalent (EncryptValue is an alias for clarity)

### Key Resolution

The context string (e.g., "tenant-123") is passed to `IKmsKeyResolver`, which returns the appropriate KMS key ARN:

```csharp
public interface IKmsKeyResolver
{
    string ResolveKeyId(string? contextId);
}
```

### Default Implementation

The `DefaultKmsKeyResolver` uses a dictionary lookup with fallback:

```csharp
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/default-key-id",
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key",
        ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key",
        ["tenant-c"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-c-key"
    });

// Usage:
// WithEncryptionContext("tenant-a") → uses tenant-a-key
// WithEncryptionContext("tenant-b") → uses tenant-b-key
// WithEncryptionContext("unknown") → uses default-key-id
// No context provided → uses default-key-id
```

### Custom Key Resolver

For dynamic key resolution (database, external service, etc.):

```csharp
public class DatabaseKmsKeyResolver : IKmsKeyResolver
{
    private readonly IKeyRepository _keyRepo;
    private readonly string _defaultKey;
    
    public DatabaseKmsKeyResolver(IKeyRepository keyRepo, string defaultKey)
    {
        _keyRepo = keyRepo;
        _defaultKey = defaultKey;
    }
    
    public string ResolveKeyId(string? contextId)
    {
        if (contextId == null)
            return _defaultKey;
            
        // Load from database, cache, external service, etc.
        var keyArn = _keyRepo.GetKmsKeyForTenant(contextId);
        return keyArn ?? _defaultKey;
    }
}
```

### Ambient Context (AsyncLocal)

For middleware scenarios, use the ambient context:

```csharp
// In middleware or request handler
// AsyncLocal ensures thread-safety and prevents cross-request leakage
EncryptionContext.Current = httpContext.GetTenantId();

// All operations in this async flow use the context
await customerTable.PutItem(data).ExecuteAsync();
await customerTable.GetItem("key").ExecuteAsync();

// Context automatically cleared when request completes
```

**Thread Safety:** `EncryptionContext.Current` uses `AsyncLocal<string?>`, which:
- Flows through async/await calls
- Does NOT leak across threads or requests
- Is isolated per async execution context

### Encryption Context in AWS Encryption SDK

The context identifier is included in the AWS Encryption SDK encryption context:

```csharp
{
    "field": "SensitiveData",
    "context": "tenant-123",  // Your context ID
    "entity": "CustomerData"
}
```

This provides:
- Audit trail in CloudTrail logs
- Additional authenticated data (AAD)
- Protection against ciphertext substitution

---

## Combined Security Features

### Using Both Attributes

Combine `[Sensitive]` and `[Encrypted]` for maximum protection:

```csharp
[DynamoDbTable("SecureEntities")]
public partial class SecureEntity
{
    [PartitionKey]
    public string EntityId { get; set; } = string.Empty;
    
    // Encrypted at rest AND redacted from logs
    [Encrypted]
    [Sensitive]
    public string HighlyConfidentialData { get; set; } = string.Empty;
    
    // Only redacted from logs (not encrypted)
    [Sensitive]
    public string Email { get; set; } = string.Empty;
    
    // Only encrypted (still appears in logs)
    [Encrypted]
    public string EncryptedButLogged { get; set; } = string.Empty;
    
    // Neither encrypted nor redacted
    public string PublicData { get; set; } = string.Empty;
}
```

### When to Use Each

| Scenario | Use `[Sensitive]` | Use `[Encrypted]` |
|----------|-------------------|-------------------|
| PII in logs | ✅ | Optional |
| Data at rest protection | Optional | ✅ |
| Compliance (GDPR, HIPAA) | ✅ | ✅ |
| Performance-critical | ✅ | ⚠️ Consider |
| Multi-tenant isolation | Optional | ✅ |
| Audit trail needed | Optional | ✅ |

---

## Integration with Blob Storage

### Overview

For large encrypted fields that might exceed DynamoDB's 400KB item size limit, combine encryption with external blob storage.

### Using BlobReferenceAttribute

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    public string DocumentId { get; set; } = string.Empty;
    
    public string Title { get; set; } = string.Empty;
    
    // Encrypted AND stored in S3
    [Encrypted]
    [BlobReference(BlobProvider.S3, BucketName = "my-encrypted-blobs", KeyPrefix = "documents/")]
    [Sensitive]
    public byte[] LargeEncryptedContent { get; set; } = Array.Empty<byte>();
}
```

### How It Works

1. **Encryption First**: Data is encrypted using AWS Encryption SDK
2. **Blob Storage**: Encrypted data is stored in S3 via `IBlobStorageProvider`
3. **DynamoDB Reference**: DynamoDB stores the S3 URI (e.g., `s3://bucket/key`)
4. **Transparent Retrieval**: On read, the library fetches from S3 and decrypts automatically

### Automatic External Storage

Configure automatic external storage for large encrypted fields:

```csharp
var options = new AwsEncryptionSdkOptions
{
    DefaultKeyId = configuration["Kms:DefaultKeyArn"],
    AutoExternalBlobThreshold = 350 * 1024,  // 350KB
    ExternalBlobBucket = "my-encrypted-blobs",
    ExternalBlobKeyPrefix = "auto/"
};
```

When encrypted data exceeds the threshold, it's automatically stored externally even without `[BlobReference]`.

### S3 Blob Storage Setup

When combining encryption with blob storage, configure both in `FluentDynamoDbOptions`:

```csharp
using Amazon.S3;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;
using Oproto.FluentDynamoDb.Encryption.Kms;

// Create S3 blob provider
var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(
    s3Client,
    bucketName: "my-encrypted-blobs",
    keyPrefix: "documents/");

// Create encryptor
var keyResolver = new DefaultKmsKeyResolver(configuration["Kms:DefaultKeyArn"]);
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);

// Configure both blob storage and encryption
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider)
    .WithEncryption(encryptor);

var table = new DocumentsTable(dynamoClient, "documents", options);
```

---

## Configuration Reference

### FluentDynamoDbOptions

The central configuration object for FluentDynamoDb. Use `WithEncryption()` to enable field-level encryption:

```csharp
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);
```

See the [Configuration Guide](../core-features/Configuration.md) for complete details on `FluentDynamoDbOptions`.

### AwsEncryptionSdkOptions

Configuration options for the AWS Encryption SDK field encryptor:

```csharp
public class AwsEncryptionSdkOptions
{
    /// <summary>
    /// Enable data key caching (default: true).
    /// Uses AWS Encryption SDK's CachingCryptoMaterialsManager.
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// Default cache TTL for data keys (seconds).
    /// Can be overridden per-field via EncryptedAttribute.
    /// </summary>
    public int DefaultCacheTtlSeconds { get; set; } = 300;
    
    /// <summary>
    /// Maximum number of messages encrypted with a single data key.
    /// AWS Encryption SDK best practice: limit reuse.
    /// </summary>
    public int MaxMessagesPerDataKey { get; set; } = 100;
    
    /// <summary>
    /// Maximum bytes encrypted with a single data key.
    /// </summary>
    public long MaxBytesPerDataKey { get; set; } = 100 * 1024 * 1024; // 100 MB
    
    /// <summary>
    /// Algorithm suite to use (default: AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384).
    /// AWS Encryption SDK 3.x uses key commitment by default.
    /// </summary>
    public string Algorithm { get; set; } = 
        "AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384";
}
```

### DefaultKmsKeyResolver

Resolves KMS key ARNs based on context identifiers:

```csharp
// Single key for all contexts
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/my-key");

// Multi-tenant with per-tenant keys
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: "arn:aws:kms:us-east-1:123456789012:key/default-key",
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a-key",
        ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b-key"
    });
```

### EncryptedAttribute

Per-field encryption configuration:

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptedAttribute : Attribute
{
    /// <summary>
    /// Cache TTL for data keys (seconds) for this specific field.
    /// Overrides AwsEncryptionSdkOptions.DefaultCacheTtlSeconds.
    /// Default: 300 (5 minutes)
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 300;
}
```

Example:

```csharp
[Encrypted(CacheTtlSeconds = 600)]  // 10 minutes for this field
public string HighFrequencyField { get; set; } = string.Empty;

[Encrypted(CacheTtlSeconds = 60)]  // 1 minute for this field
public string LowFrequencyField { get; set; } = string.Empty;
```

---

## Best Practices

### Security

1. **Never Hardcode Keys**: Load KMS key ARNs from secure configuration, not source code
2. **Use IAM Policies**: Restrict KMS key access using IAM policies and key policies
3. **Enable CloudTrail**: Monitor KMS API calls for audit trails
4. **Rotate Keys**: Use KMS automatic key rotation
5. **Combine Attributes**: Use both `[Encrypted]` and `[Sensitive]` for maximum protection

### Performance

1. **Enable Caching**: Keep `EnableCaching = true` to minimize KMS API calls
2. **Tune Cache TTL**: Balance security and performance based on your requirements
3. **Selective Encryption**: Only encrypt truly sensitive fields
4. **Monitor Costs**: KMS API calls have costs - use caching effectively
5. **Consider Field Size**: Large encrypted fields increase latency

### Multi-Tenancy

1. **Per-Tenant Keys**: Use separate KMS keys per tenant for isolation
2. **Ambient Context**: Use `EncryptionContext.Current` in middleware for automatic context flow
3. **Validate Context**: Ensure context is set before operations
4. **Test Isolation**: Verify data encrypted with one tenant's key cannot be decrypted with another's

### Logging

1. **Always Use [Sensitive]**: Mark encrypted fields as sensitive to prevent accidental logging
2. **Structured Logging**: Use structured logging to filter sensitive data
3. **Production Logging**: Consider disabling detailed logging in production
4. **Configure Both**: When using encryption with logging, configure both in `FluentDynamoDbOptions`:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(logger.ToDynamoDbLogger())
    .WithEncryption(encryptor);
```

---

## Error Handling

### FieldEncryptionException

All encryption errors throw `FieldEncryptionException`:

```csharp
try
{
    await table.PutItem(data)
        .WithEncryptionContext("tenant-123")
        .ExecuteAsync();
}
catch (FieldEncryptionException ex)
{
    Console.WriteLine($"Field: {ex.FieldName}");
    Console.WriteLine($"Context: {ex.ContextId}");
    Console.WriteLine($"Key: {ex.KeyId}");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Inner: {ex.InnerException?.Message}");
}
```

### Common Errors

#### KMS Access Denied

```
FieldEncryptionException: Failed to encrypt field 'SensitiveData' - KMS access denied
  FieldName: SensitiveData
  ContextId: tenant-123
  KeyId: arn:aws:kms:us-east-1:123456789012:key/abc-123
```

**Solution**: Check IAM permissions for `kms:GenerateDataKey` and `kms:Decrypt`

#### Data Key Generation Failed

```
FieldEncryptionException: Failed to generate data key for field 'SensitiveData'
  FieldName: SensitiveData
  ContextId: tenant-123
  KeyId: arn:aws:kms:us-east-1:123456789012:key/abc-123
```

**Solution**: Verify KMS key exists and is enabled

#### Decryption Failed

```
FieldEncryptionException: Failed to decrypt field 'SensitiveData' - data corruption or wrong key
  FieldName: SensitiveData
  ContextId: tenant-123
```

**Solution**: Verify correct KMS key is being used, check for data corruption

---

## Troubleshooting

### Diagnostic Warning

If you use `[Encrypted]` without the Encryption.Kms package, the source generator emits a warning:

```
Warning FDDB4001: Property 'SensitiveData' has [Encrypted] attribute but Oproto.FluentDynamoDb.Encryption.Kms package is not referenced
```

**Solution**: Add the package reference:

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

### Logging Not Redacting

**Problem**: Sensitive fields appear in logs

**Solutions**:
1. Verify `[Sensitive]` attribute is applied
2. Rebuild project to regenerate source code
3. Check logging is enabled
4. Verify `FluentDynamoDbOptions` with `WithLogger()` is passed to table constructor

**Example fix**:
```csharp
using Oproto.FluentDynamoDb.Logging.Extensions;

var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<UsersTable>());
var table = new UsersTable(client, "users", options);
```

### Encryption Not Working

**Problem**: Data stored as plaintext

**Solutions**:
1. Verify `FluentDynamoDbOptions` with `WithEncryption()` is passed to table constructor
2. Check `[Encrypted]` attribute is applied to the property
3. Rebuild project to regenerate source code
4. Verify KMS key ARN is valid

**Example fix**:
```csharp
// Before (encryption not configured)
var table = new SecretsTable(client, "secrets");

// After (encryption configured)
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);
var table = new SecretsTable(client, "secrets", options);
```

### Context Not Flowing

**Problem**: Wrong encryption key used

**Solutions**:
1. Verify `WithEncryptionContext()` is called
2. Check `EncryptionContext.Current` is set
3. Verify `IKmsKeyResolver` is configured correctly
4. Test key resolution logic

### Performance Issues

**Problem**: High latency or KMS costs

**Solutions**:
1. Enable caching: `EnableCaching = true`
2. Increase cache TTL: `DefaultCacheTtlSeconds = 600`
3. Reduce encrypted field count
4. Monitor KMS API calls in CloudWatch

---

## See Also

- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation
- **[Logging Configuration](../core-features/LoggingConfiguration.md)** - Configure logging and diagnostics
- **[Advanced Types](AdvancedTypes.md)** - Blob storage integration
- **[Error Handling](../reference/ErrorHandling.md)** - Exception handling patterns
- **[Encryption.Kms Package README](../../Oproto.FluentDynamoDb.Encryption.Kms/README.md)** - Detailed encryption package documentation

---

[Back to Advanced Topics](README.md) | [Back to Documentation Home](../README.md)
