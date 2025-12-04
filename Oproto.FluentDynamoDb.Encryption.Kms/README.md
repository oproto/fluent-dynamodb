# Oproto.FluentDynamoDb.Encryption.Kms

Field-level encryption for Oproto.FluentDynamoDb using AWS Key Management Service (KMS) and the AWS Encryption SDK.

## Overview

This package provides transparent field-level encryption for DynamoDB entities using AWS KMS. It integrates seamlessly with the Oproto.FluentDynamoDb source generator, allowing you to encrypt sensitive fields with a simple `[Encrypted]` attribute.

**Key Features:**
- 🔐 **AWS KMS Integration** - Industry-standard encryption using AWS Key Management Service
- 🔄 **Transparent Encryption/Decryption** - Automatic encryption on write, decryption on read
- 🏢 **Multi-Tenant Support** - Different encryption keys per tenant/customer/context
- ⚡ **Data Key Caching** - Minimize KMS API calls with configurable caching
- 🛡️ **AWS Encryption SDK** - Battle-tested encryption library with key commitment
- 📊 **CloudTrail Integration** - Audit trail of encryption operations
- 🔗 **Blob Storage Support** - Integrate with external storage for large encrypted fields

## When to Use This Package

Use this package when you need to:
- Encrypt sensitive data at rest in DynamoDB
- Comply with data protection regulations (GDPR, HIPAA, PCI-DSS)
- Implement multi-tenant data isolation with separate encryption keys
- Provide customers with their own encryption keys
- Meet data residency requirements
- Create audit trails of data access

**Note:** If you only need to exclude sensitive fields from logs, use the built-in `[Sensitive]` attribute instead. This package is only required for encryption at rest.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

**Prerequisites:**
- Oproto.FluentDynamoDb (core library)
- Oproto.FluentDynamoDb.Attributes
- AWS account with KMS access
- IAM permissions for `kms:GenerateDataKey` and `kms:Decrypt`

## Quick Start

### 1. Mark Fields for Encryption

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbEntity]
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

### 2. Configure Encryption

```csharp
using Oproto.FluentDynamoDb.Encryption.Kms;

// Load KMS key ARN from secure configuration
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: configuration["Kms:DefaultKeyArn"]);

var options = new AwsEncryptionSdkOptions
{
    DefaultKeyId = configuration["Kms:DefaultKeyArn"],
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300  // 5 minutes
};

var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options);
```

### 3. Use with Table

```csharp
var table = new CustomerDataTable(dynamoClient, "customers", encryptor);

// Encryption happens automatically
await table.PutItem(customerData).ExecuteAsync();

// Decryption happens automatically
var result = await table.GetItem("customer-123").ExecuteAsync();
```

## Configuration

### AwsEncryptionSdkOptions

Complete configuration options:

```csharp
var options = new AwsEncryptionSdkOptions
{
    // Required: Default KMS key ARN
    DefaultKeyId = "arn:aws:kms:us-east-1:123456789012:key/abc-123",
    
    // Optional: Context-specific keys for multi-tenancy
    ContextKeyMap = new Dictionary<string, string>
    {
        ["tenant-a"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-a",
        ["tenant-b"] = "arn:aws:kms:us-east-1:123456789012:key/tenant-b"
    },
    
    // Enable data key caching (recommended)
    EnableCaching = true,
    
    // Cache TTL for data keys (seconds)
    DefaultCacheTtlSeconds = 300,  // 5 minutes
    
    // Maximum messages encrypted with a single data key
    MaxMessagesPerDataKey = 100,
    
    // Maximum bytes encrypted with a single data key
    MaxBytesPerDataKey = 100 * 1024 * 1024,  // 100 MB
    
    // Algorithm suite (default uses key commitment)
    Algorithm = CryptoAlgorithm.AES_256_GCM_HKDF_SHA512_COMMIT_KEY_ECDSA_P384,
    
    // External blob storage configuration
    ExternalBlobBucket = "my-encrypted-blobs",
    ExternalBlobKeyPrefix = "encrypted-fields/",
    AutoExternalBlobThreshold = 350 * 1024  // 350KB
};
```

### Per-Field Configuration

Override cache TTL per field:

```csharp
[Encrypted(CacheTtlSeconds = 600)]  // 10 minutes for this field
public string HighFrequencyField { get; set; } = string.Empty;

[Encrypted(CacheTtlSeconds = 60)]  // 1 minute for this field
public string LowFrequencyField { get; set; } = string.Empty;
```

## Multi-Tenant Encryption

### Per-Operation Context

Pass encryption context per operation (recommended):

```csharp
await customerTable.PutItem(customerData)
    .WithEncryptionContext("tenant-123")
    .ExecuteAsync();

await customerTable.GetItem("customer-id")
    .WithEncryptionContext("tenant-123")
    .ExecuteAsync();
```

### Ambient Context

Use ambient context for middleware scenarios:

```csharp
// In middleware or request handler
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

### Custom Key Resolver

Implement `IKmsKeyResolver` for dynamic key resolution:

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

// Usage
var keyResolver = new DatabaseKmsKeyResolver(keyRepository, defaultKeyArn);
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, options);
```

## AWS Encryption SDK Integration

### Message Format

This package uses the AWS Encryption SDK, which provides:
- **Standardized Format** - Recognized by AWS services and tools
- **Algorithm Agility** - Easy to upgrade encryption algorithms
- **Key Commitment** - Prevents key substitution attacks
- **Encryption Context** - Additional authenticated data (AAD)
- **Digital Signatures** - Non-repudiation

### Encryption Context

Each encrypted field includes encryption context for auditability:

```csharp
{
    "field": "SensitiveData",
    "context": "tenant-123",  // Your context ID
    "entity": "CustomerData"
}
```

This context:
- Appears in CloudTrail logs for audit trails
- Provides additional security (AAD)
- Prevents ciphertext substitution attacks
- Is validated during decryption

### Data Key Caching

The AWS Encryption SDK's `CachingCryptoMaterialsManager` is used to minimize KMS API calls:

```csharp
var options = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300,  // Cache data keys for 5 minutes
    MaxMessagesPerDataKey = 100,   // Max 100 messages per data key
    MaxBytesPerDataKey = 100 * 1024 * 1024  // Max 100MB per data key
};
```

**Benefits:**
- Reduced KMS API calls (lower costs)
- Improved performance (fewer network round-trips)
- Configurable limits for security best practices

**Cache Key:** Includes context ID, so different contexts use different cached keys.

## External Blob Storage

### Overview

For large encrypted fields that might exceed DynamoDB's 400KB item size limit, combine encryption with external blob storage.

### Using BlobReferenceAttribute

```csharp
[DynamoDbEntity]
public partial class Document
{
    [PartitionKey]
    public string DocumentId { get; set; } = string.Empty;
    
    // Encrypted AND stored in S3
    [Encrypted]
    [BlobReference(BlobProvider.S3, BucketName = "my-encrypted-blobs", KeyPrefix = "documents/")]
    [Sensitive]
    public byte[] LargeEncryptedContent { get; set; } = Array.Empty<byte>();
}
```

### Setup

```csharp
using Oproto.FluentDynamoDb.BlobStorage.S3;

var s3Client = new AmazonS3Client();
var blobStorage = new S3BlobStorage(
    s3Client,
    bucketName: "my-encrypted-blobs",
    keyPrefix: "documents/");

var encryptor = new AwsEncryptionSdkFieldEncryptor(
    keyResolver,
    options,
    blobStorage);  // Pass blob storage provider
```

### How It Works

1. **Encryption First** - Data is encrypted using AWS Encryption SDK
2. **Blob Storage** - Encrypted data is stored in S3
3. **DynamoDB Reference** - DynamoDB stores the S3 URI
4. **Transparent Retrieval** - On read, fetches from S3 and decrypts automatically

### Automatic External Storage

Configure automatic external storage for large fields:

```csharp
var options = new AwsEncryptionSdkOptions
{
    AutoExternalBlobThreshold = 350 * 1024,  // 350KB
    ExternalBlobBucket = "my-encrypted-blobs",
    ExternalBlobKeyPrefix = "auto/"
};
```

When encrypted data exceeds the threshold, it's automatically stored externally even without `[BlobReference]`.

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

**Error:**
```
FieldEncryptionException: Failed to encrypt field 'SensitiveData' - KMS access denied
```

**Solution:** Check IAM permissions for `kms:GenerateDataKey` and `kms:Decrypt`

**Required IAM Policy:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "kms:GenerateDataKey",
        "kms:Decrypt"
      ],
      "Resource": "arn:aws:kms:us-east-1:123456789012:key/*"
    }
  ]
}
```

#### Data Key Generation Failed

**Error:**
```
FieldEncryptionException: Failed to generate data key for field 'SensitiveData'
```

**Solution:** Verify KMS key exists and is enabled

#### Decryption Failed

**Error:**
```
FieldEncryptionException: Failed to decrypt field 'SensitiveData' - data corruption or wrong key
```

**Solutions:**
- Verify correct KMS key is being used
- Check encryption context matches
- Verify data is not corrupted

## Performance Considerations

### KMS API Costs

KMS API calls have costs:
- `GenerateDataKey`: ~$0.03 per 10,000 requests
- `Decrypt`: ~$0.03 per 10,000 requests

**Recommendation:** Enable caching to minimize costs.

### Latency

Encryption adds latency:
- First encryption (no cache): ~50-100ms (KMS API call)
- Cached encryption: ~1-5ms (local encryption only)
- Decryption: Similar to encryption

**Recommendation:** Use caching and tune cache TTL based on your requirements.

### Throughput

Encryption is CPU-bound:
- AES-256-GCM: ~1-5 microseconds per KB
- Minimal impact on throughput for typical field sizes

**Recommendation:** Only encrypt truly sensitive fields.

## Security Best Practices

### Key Management

1. **Never Hardcode Keys** - Load KMS key ARNs from secure configuration
2. **Use IAM Policies** - Restrict KMS key access using IAM and key policies
3. **Enable CloudTrail** - Monitor KMS API calls for audit trails
4. **Rotate Keys** - Use KMS automatic key rotation
5. **Separate Keys** - Use different keys per tenant/environment

### Encryption

1. **Combine Attributes** - Use both `[Encrypted]` and `[Sensitive]`
2. **Selective Encryption** - Only encrypt truly sensitive fields
3. **Validate Context** - Ensure encryption context is set correctly
4. **Test Isolation** - Verify tenant data isolation

### Logging

1. **Always Use [Sensitive]** - Mark encrypted fields as sensitive
2. **Structured Logging** - Use structured logging to filter sensitive data
3. **Production Logging** - Consider disabling detailed logging in production

## Troubleshooting

### Diagnostic Warning

If you use `[Encrypted]` without this package, the source generator emits a warning:

```
Warning FDDB4001: Property 'SensitiveData' has [Encrypted] attribute but Oproto.FluentDynamoDb.Encryption.Kms package is not referenced
```

**Solution:** Add the package reference:

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

### Encryption Not Working

**Problem:** Data stored as plaintext

**Solutions:**
1. Verify `IFieldEncryptor` is passed to table constructor
2. Check `[Encrypted]` attribute is applied
3. Rebuild project to regenerate source code
4. Verify KMS key ARN is valid

### Context Not Flowing

**Problem:** Wrong encryption key used

**Solutions:**
1. Verify `WithEncryptionContext()` is called
2. Check `EncryptionContext.Current` is set
3. Verify `IKmsKeyResolver` is configured correctly
4. Test key resolution logic

### Performance Issues

**Problem:** High latency or KMS costs

**Solutions:**
1. Enable caching: `EnableCaching = true`
2. Increase cache TTL: `DefaultCacheTtlSeconds = 600`
3. Reduce encrypted field count
4. Monitor KMS API calls in CloudWatch

## Examples

### Basic Encryption

```csharp
// Entity definition
[DynamoDbEntity]
public partial class User
{
    [PartitionKey]
    public string UserId { get; set; } = string.Empty;
    
    [Encrypted]
    [Sensitive]
    public string Email { get; set; } = string.Empty;
}

// Setup
var keyResolver = new DefaultKmsKeyResolver(kmsKeyArn);
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
var table = new UserTable(dynamoClient, "users", encryptor);

// Usage
await table.PutItem(user).ExecuteAsync();
```

### Multi-Tenant Encryption

```csharp
// Setup with tenant-specific keys
var keyResolver = new DefaultKmsKeyResolver(
    defaultKeyId: defaultKeyArn,
    contextKeyMap: new Dictionary<string, string>
    {
        ["tenant-a"] = tenantAKeyArn,
        ["tenant-b"] = tenantBKeyArn
    });

var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
var table = new CustomerTable(dynamoClient, "customers", encryptor);

// Usage with context
await table.PutItem(customer)
    .WithEncryptionContext("tenant-a")
    .ExecuteAsync();
```

### Custom Key Resolver

```csharp
// Custom resolver
public class DynamicKeyResolver : IKmsKeyResolver
{
    private readonly IConfiguration _config;
    
    public string ResolveKeyId(string? contextId)
    {
        if (contextId == null)
            return _config["Kms:DefaultKey"];
            
        return _config[$"Kms:Tenant:{contextId}:Key"] 
            ?? _config["Kms:DefaultKey"];
    }
}

// Usage
var keyResolver = new DynamicKeyResolver(configuration);
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);
```

### External Blob Storage

```csharp
// Setup with blob storage
var s3Client = new AmazonS3Client();
var blobStorage = new S3BlobStorage(s3Client, "my-bucket", "encrypted/");

var options = new AwsEncryptionSdkOptions
{
    DefaultKeyId = kmsKeyArn,
    AutoExternalBlobThreshold = 350 * 1024
};

var encryptor = new AwsEncryptionSdkFieldEncryptor(
    keyResolver,
    options,
    blobStorage);

// Entity with large encrypted field
[DynamoDbEntity]
public partial class Document
{
    [PartitionKey]
    public string DocumentId { get; set; } = string.Empty;
    
    [Encrypted]
    [BlobReference(BlobProvider.S3, BucketName = "my-bucket", KeyPrefix = "docs/")]
    public byte[] LargeContent { get; set; } = Array.Empty<byte>();
}
```

## See Also

- **[Field-Level Security Guide](../docs/advanced-topics/FieldLevelSecurity.md)** - Complete security guide
- **[Attribute Reference](../docs/reference/AttributeReference.md)** - Attribute documentation
- **[Advanced Types](../docs/advanced-topics/AdvancedTypes.md)** - Blob storage integration
- **[Error Handling](../docs/reference/ErrorHandling.md)** - Exception handling

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.Encryption.Kms](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Encryption.Kms)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
