# Encryption Guide

## Overview

Oproto.FluentDynamoDb supports field-level encryption for sensitive data using the `[Encrypted]` attribute. As of the latest version, encryption now works seamlessly in update expressions, providing consistent security across all DynamoDB operations.

## Setting Up IFieldEncryptor

### Interface Definition

```csharp
public interface IFieldEncryptor
{
    Task<byte[]> EncryptAsync(
        byte[] plaintext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default);
    
    Task<byte[]> DecryptAsync(
        byte[] ciphertext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default);
}
```

### Implementation Options

#### Option 1: AWS KMS Encryption (Recommended for Production)

```csharp
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

public class KmsFieldEncryptor : IFieldEncryptor
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly string _keyId;

    public KmsFieldEncryptor(IAmazonKeyManagementService kmsClient, string keyId)
    {
        _kmsClient = kmsClient;
        _keyId = keyId;
    }

    public async Task<byte[]> EncryptAsync(
        byte[] plaintext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default)
    {
        var request = new EncryptRequest
        {
            KeyId = _keyId,
            Plaintext = new MemoryStream(plaintext),
            EncryptionContext = new Dictionary<string, string>
            {
                ["field"] = fieldName,
                ["entity"] = context.EntityType
            }
        };

        var response = await _kmsClient.EncryptAsync(request, cancellationToken);
        return response.CiphertextBlob.ToArray();
    }

    public async Task<byte[]> DecryptAsync(
        byte[] ciphertext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default)
    {
        var request = new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(ciphertext),
            EncryptionContext = new Dictionary<string, string>
            {
                ["field"] = fieldName,
                ["entity"] = context.EntityType
            }
        };

        var response = await _kmsClient.DecryptAsync(request, cancellationToken);
        return response.Plaintext.ToArray();
    }
}
```

#### Option 2: AWS Encryption SDK (Recommended for Advanced Scenarios)

The `Oproto.FluentDynamoDb.Encryption.Kms` package provides a ready-to-use implementation:

```csharp
using Oproto.FluentDynamoDb.Encryption.Kms;

var encryptor = new AwsEncryptionSdkFieldEncryptor(
    new AwsEncryptionSdkOptions
    {
        KmsKeyId = "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
        EnableCaching = true,
        CacheCapacity = 100,
        MaxCacheAge = TimeSpan.FromMinutes(5)
    });
```

#### Option 3: Simple AES Encryption (Development/Testing Only)

```csharp
using System.Security.Cryptography;

public class AesFieldEncryptor : IFieldEncryptor
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesFieldEncryptor(byte[] key, byte[] iv)
    {
        _key = key;
        _iv = iv;
    }

    public async Task<byte[]> EncryptAsync(
        byte[] plaintext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        return await Task.FromResult(
            encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length));
    }

    public async Task<byte[]> DecryptAsync(
        byte[] ciphertext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor();
        return await Task.FromResult(
            decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length));
    }
}
```

### Configuring the Encryptor

```csharp
// Create the encryptor
var encryptor = new KmsFieldEncryptor(kmsClient, kmsKeyId);

// Configure in DynamoDbOperationContext
var context = new DynamoDbOperationContext
{
    FieldEncryptor = encryptor,
    EncryptionContextId = "my-app-v1"
};

// Pass to table instance
var table = new UserTable(dynamoDbClient, context);
```

## Marking Properties for Encryption

### Basic Usage

```csharp
using Oproto.FluentDynamoDb.Attributes;

public class User
{
    [PartitionKey]
    [DynamoDbAttribute("user_id")]
    public string UserId { get; set; }

    [DynamoDbAttribute("email")]
    public string Email { get; set; }

    // Mark sensitive fields with [Encrypted]
    [DynamoDbAttribute("ssn")]
    [Encrypted]
    public string SocialSecurityNumber { get; set; }

    [DynamoDbAttribute("credit_card")]
    [Encrypted]
    public string CreditCardNumber { get; set; }
}
```

### Combining with Other Attributes

```csharp
public class HealthRecord
{
    [PartitionKey]
    [DynamoDbAttribute("record_id")]
    public string RecordId { get; set; }

    // Encrypted and sensitive (redacted in logs)
    [DynamoDbAttribute("diagnosis")]
    [Encrypted]
    [Sensitive]
    public string Diagnosis { get; set; }

    // Encrypted with format string
    [DynamoDbAttribute("test_date", Format = "yyyy-MM-dd")]
    [Encrypted]
    public DateTime TestDate { get; set; }
}
```

## How Encryption Works in Update Expressions

### Architecture Overview

Encryption in update expressions uses a **deferred encryption** approach:

1. **Expression Translation**: The `UpdateExpressionTranslator` analyzes the expression and identifies encrypted properties
2. **Parameter Metadata Tracking**: Parameters requiring encryption are marked in `ExpressionContext.ParameterMetadata`
3. **Deferred Encryption**: The actual encryption is deferred to the request builder (e.g., `UpdateItemRequestBuilder`)
4. **Async Encryption**: The request builder calls `IFieldEncryptor.EncryptAsync` before sending the request to DynamoDB
5. **Value Replacement**: Encrypted values (base64-encoded) replace plaintext values in `ExpressionAttributeValues`

### Example Flow

```csharp
// 1. Define update expression with encrypted property
await table.Update
    .WithKey("user_id", userId)
    .Set(x => new UserUpdateModel 
    { 
        SocialSecurityNumber = newSsn  // Marked for encryption
    })
    .ExecuteAsync();

// 2. Translator marks parameter for encryption
// ExpressionContext.ParameterMetadata contains:
// {
//     ParameterName = ":p0",
//     RequiresEncryption = true,
//     PropertyName = "SocialSecurityNumber",
//     AttributeName = "ssn"
// }

// 3. Request builder encrypts before sending
// var ciphertext = await _fieldEncryptor.EncryptAsync(plaintext, "SocialSecurityNumber", context);
// request.ExpressionAttributeValues[":p0"] = new AttributeValue { S = Convert.ToBase64String(ciphertext) };

// 4. DynamoDB receives encrypted value
```

### Supported Operations

Encryption works in all update expression operations:

```csharp
// Simple SET
await table.Update
    .WithKey("pk", id)
    .Set(x => new UpdateModel { EncryptedField = "new value" })
    .ExecuteAsync();

// Conditional update
await table.Update
    .WithKey("pk", id)
    .Set(x => new UpdateModel { EncryptedField = "new value" })
    .WithCondition("attribute_exists(pk)")
    .ExecuteAsync();

// Multiple encrypted fields
await table.Update
    .WithKey("pk", id)
    .Set(x => new UpdateModel 
    { 
        EncryptedField1 = "value1",
        EncryptedField2 = "value2"
    })
    .ExecuteAsync();

// Transaction updates
await table.TransactWrite
    .Update(u => u
        .WithKey("pk", id)
        .Set(x => new UpdateModel { EncryptedField = "new value" }))
    .ExecuteAsync();
```

## Performance Considerations

### Encryption Overhead

Encryption adds latency to DynamoDB operations:

| Encryption Method | Typical Latency | Notes |
|-------------------|-----------------|-------|
| AWS KMS (no cache) | 10-50ms per field | Network call to KMS |
| AWS KMS (with cache) | 1-5ms per field | Cache hit avoids KMS call |
| AWS Encryption SDK | 1-5ms per field | Built-in caching |
| Local AES | <1ms per field | No network calls |

### Best Practices for Performance

#### 1. Enable KMS Caching

```csharp
var encryptor = new AwsEncryptionSdkFieldEncryptor(
    new AwsEncryptionSdkOptions
    {
        KmsKeyId = kmsKeyId,
        EnableCaching = true,           // Enable caching
        CacheCapacity = 100,            // Cache up to 100 data keys
        MaxCacheAge = TimeSpan.FromMinutes(5)  // Refresh every 5 minutes
    });
```

#### 2. Minimize Encrypted Fields

Only encrypt truly sensitive data:

```csharp
public class User
{
    public string UserId { get; set; }        // Not encrypted
    public string Email { get; set; }         // Not encrypted
    
    [Encrypted]
    public string SocialSecurityNumber { get; set; }  // Encrypted - truly sensitive
    
    [Encrypted]
    public string CreditCardNumber { get; set; }      // Encrypted - truly sensitive
}
```

#### 3. Batch Operations

Encryption overhead is per-field, so batching operations doesn't reduce encryption cost:

```csharp
// Each encrypted field is encrypted separately
await table.Update
    .WithKey("pk", id)
    .Set(x => new UpdateModel 
    { 
        Field1 = "value1",  // 1 encryption call
        Field2 = "value2",  // 1 encryption call
        Field3 = "value3"   // 1 encryption call
    })
    .ExecuteAsync();
```

#### 4. Use Envelope Encryption

For large encrypted values, consider envelope encryption (encrypting data with a data key, then encrypting the data key with KMS):

```csharp
// AWS Encryption SDK handles this automatically
var encryptor = new AwsEncryptionSdkFieldEncryptor(options);
```

### Benchmarks

```
Operation                          | Without Encryption | With KMS (cached) | Overhead
-----------------------------------|--------------------|--------------------|----------
PutItem (1 encrypted field)        | 15ms               | 18ms               | 20%
UpdateItem (1 encrypted field)     | 12ms               | 15ms               | 25%
UpdateItem (3 encrypted fields)    | 12ms               | 21ms               | 75%
TransactWriteItems (2 updates)     | 25ms               | 31ms               | 24%
```

## Security Best Practices

### 1. Use AWS KMS for Production

```csharp
// ✅ Recommended for production
var encryptor = new KmsFieldEncryptor(kmsClient, kmsKeyId);

// ❌ Not recommended for production (no key rotation, no audit trail)
var encryptor = new AesFieldEncryptor(key, iv);
```

### 2. Enable KMS Key Rotation

```bash
# Enable automatic key rotation in AWS KMS
aws kms enable-key-rotation --key-id $KEY_ID
```

### 3. Use Encryption Context

Encryption context provides additional authenticated data (AAD):

```csharp
public async Task<byte[]> EncryptAsync(
    byte[] plaintext, 
    string fieldName, 
    FieldEncryptionContext context, 
    CancellationToken cancellationToken = default)
{
    var request = new EncryptRequest
    {
        KeyId = _keyId,
        Plaintext = new MemoryStream(plaintext),
        EncryptionContext = new Dictionary<string, string>
        {
            ["field"] = fieldName,           // Field name
            ["entity"] = context.EntityType, // Entity type
            ["app"] = "my-app",              // Application identifier
            ["version"] = "v1"               // Schema version
        }
    };
    
    // ...
}
```

### 4. Implement Key Access Policies

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "Allow application to use KMS key",
      "Effect": "Allow",
      "Principal": {
        "AWS": "arn:aws:iam::123456789012:role/MyAppRole"
      },
      "Action": [
        "kms:Encrypt",
        "kms:Decrypt",
        "kms:GenerateDataKey"
      ],
      "Resource": "*",
      "Condition": {
        "StringEquals": {
          "kms:EncryptionContext:app": "my-app"
        }
      }
    }
  ]
}
```

### 5. Monitor Encryption Operations

```csharp
public class MonitoredFieldEncryptor : IFieldEncryptor
{
    private readonly IFieldEncryptor _inner;
    private readonly ILogger _logger;

    public async Task<byte[]> EncryptAsync(
        byte[] plaintext, 
        string fieldName, 
        FieldEncryptionContext context, 
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _inner.EncryptAsync(plaintext, fieldName, context, cancellationToken);
            _logger.LogInformation(
                "Encrypted field {FieldName} in {ElapsedMs}ms", 
                fieldName, 
                sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt field {FieldName}", fieldName);
            throw;
        }
    }
    
    // Similar for DecryptAsync
}
```

## Troubleshooting Encryption Errors

### Error: "Field encryption is required but no IFieldEncryptor is configured"

**Cause**: Property marked with `[Encrypted]` but no encryptor configured

**Solution**: Configure IFieldEncryptor in DynamoDbOperationContext
```csharp
var context = new DynamoDbOperationContext
{
    FieldEncryptor = new KmsFieldEncryptor(kmsClient, kmsKeyId)
};

var table = new UserTable(dynamoDbClient, context);
```

### Error: "KMS key not found" or "Access denied"

**Cause**: Invalid KMS key ID or insufficient IAM permissions

**Solution**: 
1. Verify KMS key ID is correct
2. Check IAM role has `kms:Encrypt` and `kms:Decrypt` permissions
3. Verify encryption context matches key policy conditions

```bash
# Test KMS access
aws kms encrypt \
  --key-id $KEY_ID \
  --plaintext "test" \
  --encryption-context field=test,entity=User
```

### Error: "Failed to decrypt: Invalid ciphertext"

**Cause**: Data was encrypted with a different key or corrupted

**Solution**:
1. Verify you're using the same KMS key for encryption and decryption
2. Check if encryption context matches
3. Verify data hasn't been corrupted in DynamoDB

### Error: "Encryption timeout"

**Cause**: KMS service latency or network issues

**Solution**:
1. Enable KMS caching to reduce KMS calls
2. Increase timeout in AWS SDK configuration
3. Check network connectivity to KMS endpoints

```csharp
var kmsClient = new AmazonKeyManagementServiceClient(new AmazonKeyManagementServiceConfig
{
    Timeout = TimeSpan.FromSeconds(30),
    MaxErrorRetry = 3
});
```

## Migration from Unencrypted Data

### Scenario: Adding Encryption to Existing Fields

If you have existing unencrypted data:

**Step 1: Add [Encrypted] attribute**
```csharp
public class User
{
    [DynamoDbAttribute("ssn")]
    [Encrypted]  // Add this
    public string SocialSecurityNumber { get; set; }
}
```

**Step 2: Migrate existing data**
```csharp
// Scan all items and re-encrypt
await foreach (var user in table.Scan().ExecuteAsync())
{
    // Update will encrypt the value
    await table.Update()
        .WithKey("user_id", user.UserId)
        .Set(x => new UserUpdateModel 
        { 
            SocialSecurityNumber = user.SocialSecurityNumber
        })
        .ExecuteAsync();
}
```

**Step 3: Handle mixed data during transition**
```csharp
public class User
{
    [DynamoDbAttribute("ssn")]
    [Encrypted]
    public string SocialSecurityNumber { get; set; }
    
    // Temporary flag to track migration
    [DynamoDbAttribute("ssn_encrypted")]
    public bool SsnEncrypted { get; set; }
}

// Custom read logic during migration
public async Task<User> GetUserAsync(string userId)
{
    var user = await table.GetItem
        .WithKey("user_id", userId)
        .ExecuteAsync();
    
    // If not encrypted yet, encrypt and update
    if (!user.SsnEncrypted)
    {
        await table.Update
            .WithKey("user_id", userId)
            .Set(x => new UserUpdateModel 
            { 
                SocialSecurityNumber = user.SocialSecurityNumber,
                SsnEncrypted = true
            })
            .ExecuteAsync();
    }
    
    return user;
}
```

## See Also

- [Oproto.FluentDynamoDb.Encryption.Kms Package](../packages/encryption-kms.md) - Ready-to-use KMS encryption
- [AWS KMS Best Practices](https://docs.aws.amazon.com/kms/latest/developerguide/best-practices.html) - Official AWS guidance
- [AWS Encryption SDK](https://docs.aws.amazon.com/encryption-sdk/latest/developer-guide/introduction.html) - Advanced encryption features
- [Sensitive Attribute Guide](sensitive-attribute-guide.md) - Log redaction for sensitive data
