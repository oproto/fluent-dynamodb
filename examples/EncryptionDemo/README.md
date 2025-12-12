# EncryptionDemo

This example demonstrates field-level encryption and sensitive data logging with FluentDynamoDb.

## Features Demonstrated

- **[Sensitive] Attribute**: Redacts property values in log output while storing actual values in DynamoDB
- **[Encrypted] Attribute**: API pattern for field-level encryption using AWS KMS (implementation pending)
- **ConsoleLogger**: Real-time logging with color-coded output and timestamps
- **FluentDynamoDbOptions Configuration**: Setting up logging and encryption providers

## Important Note

⚠️ **AWS Encryption SDK integration is pending completion.** The `[Encrypted]` attribute demonstrates the intended API pattern, but actual encryption/decryption is not yet implemented. The `[Sensitive]` attribute for log redaction is fully functional.

## Prerequisites

- .NET 8.0 SDK
- DynamoDB Local running on port 8000
- (Optional) AWS KMS key for encryption features

### Starting DynamoDB Local

```bash
# Using Docker
docker run -p 8000:8000 amazon/dynamodb-local

# Or using the local JAR
java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
```

## Running the Example

```bash
cd examples/EncryptionDemo
dotnet run
```

## KMS Configuration (Optional)

When prompted, you can provide:
- **KMS Key ARN**: The ARN of your KMS key (e.g., `arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012`)
- **AWS Profile**: The name of your AWS profile for credentials

### Required IAM Permissions

If using KMS encryption, your IAM role/user needs:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "kms:Encrypt",
                "kms:Decrypt",
                "kms:GenerateDataKey"
            ],
            "Resource": "arn:aws:kms:REGION:ACCOUNT:key/KEY-ID"
        }
    ]
}
```

## Entity Definition

The `SecureRecord` entity demonstrates different attribute combinations:

```csharp
[DynamoDbTable("encryption-demo")]
[Scannable]
public partial class SecureRecord
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }

    // Normal field - appears in logs as-is
    [DynamoDbAttribute("label")]
    public string Label { get; set; }

    // [Sensitive] - Redacted in logs, stored as plain text
    [Sensitive]
    [DynamoDbAttribute("email")]
    public string Email { get; set; }

    // [Encrypted] - Encrypted at rest (when implemented)
    [Encrypted]
    [DynamoDbAttribute("ssn")]
    public string SocialSecurityNumber { get; set; }

    // [Encrypted] + [Sensitive] - Encrypted AND redacted in logs
    [Encrypted]
    [Sensitive]
    [DynamoDbAttribute("creditCard")]
    public string CreditCardNumber { get; set; }
}
```

## Attribute Behavior

| Attribute | Log Output | DynamoDB Storage |
|-----------|------------|------------------|
| (none) | Actual value | Plain text |
| `[Sensitive]` | `[REDACTED]` | Plain text |
| `[Encrypted]` | Actual value | Encrypted (pending) |
| `[Encrypted] + [Sensitive]` | `[REDACTED]` | Encrypted (pending) |

## Console Logger

The example includes a `ConsoleLogger` implementation that:
- Displays timestamps for each log entry
- Color-codes log levels (Trace, Debug, Info, Warning, Error, Critical)
- Shows event IDs for correlation
- Demonstrates how sensitive data appears as `[REDACTED]`

## Menu Options

1. **Create Secure Record** - Create a new record with sensitive fields
2. **List All Records** - View all records (watch for redacted values in logs)
3. **View Record Details** - See actual values stored in DynamoDB
4. **Delete Record** - Remove a record
5. **Show Logging Demo** - Demonstrate different log levels

## Related Documentation

- [Logging Configuration](../../docs/core-features/LoggingConfiguration.md)
- [Field Encryption](../../docs/advanced-topics/FieldEncryption.md)
- [Sensitive Data Handling](../../docs/advanced-topics/SensitiveData.md)
