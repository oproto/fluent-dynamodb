---
title: "API Improvements Migration Guide"
category: "reference"
order: 10
keywords: ["migration", "format", "encryption", "sensitive", "linq", "upgrade"]
related: ["AttributeReference.md", "FormatSpecifiers.md", "../core-features/LinqExpressions.md", "../advanced-topics/FieldLevelSecurity.md"]
---

[Documentation](../README.md) > [Reference](README.md) > API Improvements Migration Guide

# API Improvements Migration Guide

---

This guide helps you migrate to the latest API improvements, including:
1. Format property on `[DynamoDbAttribute]`
2. Sensitive data redaction in LINQ expressions
3. Manual encryption helpers for queries

## Overview

These improvements enhance the FluentDynamoDb API with:
- **Consistent Formatting**: Define formats once on attributes, applied automatically in LINQ expressions
- **Automatic Redaction**: Sensitive property values are redacted from logs
- **Manual Encryption**: Explicit encryption helpers for querying encrypted fields

All improvements are **backward compatible** - existing code continues to work without changes.

## Format Property Migration

### What Changed

You can now specify a `Format` property on `[DynamoDbAttribute]` that is automatically applied when the property is used in LINQ expressions.

### Before

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
}

// Format specified in every query
await table.Query()
    .Where($"{TransactionFields.TransactionId} = {{0}} AND " +
           $"{TransactionFields.CreatedAt} > {{0:o}}", txId, date)
    .ExecuteAsync();

await table.Query()
    .Where($"{TransactionFields.Amount} > {{0:F2}}", 100.00m)
    .ExecuteAsync();
```

### After

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    // Format defined once
    [DynamoDbAttribute("created_at", Format = "o")]
    public DateTime CreatedAt { get; set; }
    
    // Format defined once
    [DynamoDbAttribute("amount", Format = "F2")]
    public decimal Amount { get; set; }
}

// Format applied automatically in LINQ expressions
await table.Query<Transaction>()
    .Where(x => x.TransactionId == txId && x.CreatedAt > date)
    .ToListAsync();

await table.Query<Transaction>()
    .WithFilter<Transaction>(x => x.Amount > 100.00m)
    .ToListAsync();
```

### Migration Steps

1. **Identify Properties with Repeated Formats**
   - Look for properties that use the same format specifier in multiple queries
   - Common candidates: DateTime, decimal, int (for sequences)

2. **Add Format Property**
   ```csharp
   // Before
   [DynamoDbAttribute("created_at")]
   public DateTime CreatedAt { get; set; }
   
   // After
   [DynamoDbAttribute("created_at", Format = "o")]
   public DateTime CreatedAt { get; set; }
   ```

3. **Rebuild Project**
   - The source generator will emit the format in property metadata
   - No code changes required - format is applied automatically

4. **Optional: Migrate to LINQ Expressions**
   - If using string-based expressions, consider migrating to LINQ for type safety
   - Format is applied automatically in LINQ expressions
   - String-based expressions still work with format specifiers

### When to Use Format Property

**Use Format property when:**
- ✅ Property is used in multiple queries with the same format
- ✅ Using LINQ expressions (format applied automatically)
- ✅ Want consistent formatting across the application

**Continue using format specifiers when:**
- ✅ Using string-based expressions
- ✅ Need different formats in different queries (rare)
- ✅ Format is query-specific, not property-specific

### Common Format Patterns

```csharp
// DateTime - ISO 8601 for sortable dates
[DynamoDbAttribute("timestamp", Format = "o")]
public DateTime Timestamp { get; set; }

// DateTime - Date only for partitioning
[DynamoDbAttribute("date_key", Format = "yyyy-MM-dd")]
public DateTime DateKey { get; set; }

// Decimal - Two decimal places for money
[DynamoDbAttribute("price", Format = "F2")]
public decimal Price { get; set; }

// Integer - Zero-padded for sortable sequences
[DynamoDbAttribute("sequence", Format = "D10")]
public int Sequence { get; set; }

// Double - Four decimal places for measurements
[DynamoDbAttribute("weight", Format = "F4")]
public double Weight { get; set; }
```

## Sensitive Data Redaction Migration

### What Changed

Properties marked with `[Sensitive]` are now automatically redacted from logs when used in LINQ expressions, in addition to existing redaction in Put/Get operations.

### Before

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    [Sensitive]  // Only redacted in Put/Get logs
    public string Email { get; set; } = string.Empty;
}

// Query logs showed sensitive values
await table.Query()
    .Where($"{UserFields.UserId} = {{0}}", userId)
    .WithFilter($"{UserFields.Email} = {{0}}", "user@example.com")
    .ExecuteAsync();

// Log output:
// Filter: email = :p0
// Parameters: { :p0 = "user@example.com" }  // ❌ Sensitive value visible
```

### After

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    [Sensitive]  // Now redacted in all logs, including queries
    public string Email { get; set; } = string.Empty;
}

// Query logs automatically redact sensitive values
await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => x.Email == "user@example.com")
    .ToListAsync();

// Log output:
// Filter: email = :p0
// Parameters: { :p0 = [REDACTED] }  // ✅ Sensitive value redacted
```

### Migration Steps

1. **No Code Changes Required**
   - Existing `[Sensitive]` attributes automatically work with LINQ expressions
   - Redaction happens automatically when logging is enabled

2. **Review Sensitive Properties**
   - Ensure all sensitive properties have `[Sensitive]` attribute
   - Common candidates: email, phone, SSN, credit card, passwords

3. **Test Logging**
   - Enable logging and verify sensitive values are redacted
   - Check both LINQ and string-based expressions

### What Gets Redacted

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;  // Not redacted
    
    [DynamoDbAttribute("email")]
    [Sensitive]
    public string Email { get; set; } = string.Empty;  // Redacted
    
    [DynamoDbAttribute("ssn")]
    [Sensitive]
    public string SocialSecurityNumber { get; set; } = string.Empty;  // Redacted
}

await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => 
        x.Name == "John Doe" &&           // Not redacted
        x.Email == "user@example.com" &&  // Redacted
        x.SocialSecurityNumber == "123-45-6789")  // Redacted
    .ToListAsync();

// Log output:
// Filter: name = :p0 AND email = :p1 AND ssn = :p2
// Parameters: { :p0 = "John Doe", :p1 = [REDACTED], :p2 = [REDACTED] }
```

### Important Notes

- Redaction only affects logging, not actual query values
- Property names are preserved for debugging
- No performance impact when logging is disabled
- Works with all expression types (LINQ and string-based)

## Manual Encryption Migration

### What Changed

You can now manually encrypt query parameters using `table.Encrypt()` or `table.EncryptValue()` methods. This is necessary for querying encrypted fields because automatic encryption would break non-equality operations.

### Before

```csharp
[DynamoDbTable("users")]
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

// Manual encryption was complex
var context = new FieldEncryptionContext { ContextId = "tenant-123" };
var plaintext = System.Text.Encoding.UTF8.GetBytes(ssn);
var ciphertext = await encryptor.EncryptAsync(plaintext, "SocialSecurityNumber", context);
var encryptedValue = Convert.ToBase64String(ciphertext);

await table.Query()
    .Where($"{UserFields.UserId} = {{0}}", userId)
    .WithFilter($"{UserFields.SocialSecurityNumber} = {{0}}", encryptedValue)
    .ExecuteAsync();
```

### After

```csharp
[DynamoDbTable("users")]
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

// Option 1: Encrypt in LINQ expression
EncryptionContext.Current = "tenant-123";
await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => x.SocialSecurityNumber == table.Encrypt(ssn, "SocialSecurityNumber"))
    .ToListAsync();

// Option 2: Pre-encrypt with helper
EncryptionContext.Current = "tenant-123";
var encryptedSsn = table.EncryptValue(ssn, "SocialSecurityNumber");
await table.Query<User>()
    .Where(x => x.UserId == userId)
    .WithFilter<User>(x => x.SocialSecurityNumber == encryptedSsn)
    .ToListAsync();

// Option 3: String-based expression
EncryptionContext.Current = "tenant-123";
await table.Query()
    .Where($"{UserFields.UserId} = {{0}}", userId)
    .WithFilter($"{UserFields.SocialSecurityNumber} = {{0}}", 
                table.Encrypt(ssn, "SocialSecurityNumber"))
    .ExecuteAsync();
```

### Migration Steps

1. **Identify Encrypted Field Queries**
   - Look for queries that filter on `[Encrypted]` properties
   - These queries need manual encryption

2. **Choose Encryption Approach**
   - **LINQ expression**: Use `table.Encrypt()` directly in expression
   - **Pre-encryption**: Use `table.EncryptValue()` before query
   - **String expression**: Use `table.Encrypt()` in format string

3. **Set Encryption Context**
   - Use ambient `EncryptionContext.Current` (same as Put/Get)
   - Set before encryption operations
   ```csharp
   EncryptionContext.Current = "tenant-123";
   ```

4. **Update Queries**
   ```csharp
   // Before
   var encryptedValue = await ManuallyEncrypt(value);
   await table.Query()
       .WithFilter($"{UserFields.EncryptedField} = {{0}}", encryptedValue)
       .ExecuteAsync();
   
   // After
   EncryptionContext.Current = "tenant-123";
   await table.Query<User>()
       .WithFilter<User>(x => x.EncryptedField == table.Encrypt(value, "EncryptedField"))
       .ToListAsync();
   ```

### When to Use Manual Encryption

**Use manual encryption for:**
- ✅ Equality comparisons (`==`)
- ✅ IN queries

**Do NOT use manual encryption for:**
- ❌ Range queries (`>`, `<`, `>=`, `<=`, `BETWEEN`)
- ❌ String operations (`begins_with`, `contains`)
- ❌ Numeric operations

**Why?** Encrypted values are opaque ciphertext - they don't preserve ordering or string relationships.

### Encryption Context Pattern

Manual encryption uses the same ambient context pattern as Put/Get operations:

```csharp
// Set context once for the async flow
EncryptionContext.Current = "tenant-123";

// All operations use the context
await table.PutItem(user).PutAsync();
var encrypted = table.Encrypt(value, "FieldName");
await table.Query<User>()
    .WithFilter<User>(x => x.EncryptedField == table.Encrypt(value, "EncryptedField"))
    .ToListAsync();

// Context automatically cleared when async flow completes
```

### Error Handling

```csharp
try
{
    var encrypted = table.Encrypt(value, "FieldName");
}
catch (InvalidOperationException ex)
{
    // "Cannot encrypt value: IFieldEncryptor not configured. 
    //  Pass an IFieldEncryptor instance to the table constructor."
}
```

### Complete Example

```csharp
[DynamoDbTable("medical_records")]
public partial class MedicalRecord
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PatientId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string RecordId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("diagnosis")]
    [Encrypted]
    [Sensitive]
    public string Diagnosis { get; set; } = string.Empty;
    
    [DynamoDbAttribute("ssn")]
    [Encrypted]
    [Sensitive]
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

// Query by encrypted SSN
public async Task<List<MedicalRecord>> FindBySSN(string ssn, string tenantId)
{
    // Set encryption context
    EncryptionContext.Current = tenantId;
    
    // Query with manual encryption
    var response = await table.Scan<MedicalRecord>()
        .WithFilter<MedicalRecord>(x => 
            x.SocialSecurityNumber == table.Encrypt(ssn, "SocialSecurityNumber"))
        .ExecuteAsync();
    
    return response.Items;
}

// Query by patient with encrypted diagnosis filter
public async Task<List<MedicalRecord>> FindByPatientAndDiagnosis(
    string patientId, 
    string diagnosis, 
    string tenantId)
{
    // Set encryption context
    EncryptionContext.Current = tenantId;
    
    // Pre-encrypt diagnosis
    var encryptedDiagnosis = table.EncryptValue(diagnosis, "Diagnosis");
    
    // Query with encrypted filter
    var response = await table.Query<MedicalRecord>()
        .Where(x => x.PatientId == patientId)
        .WithFilter<MedicalRecord>(x => x.Diagnosis == encryptedDiagnosis)
        .ToListAsync();
    
    return response;
}
```

## Combined Migration Example

Here's a complete example showing all three improvements together:

### Before

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string AccountId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TransactionId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("card_number")]
    [Encrypted]
    [Sensitive]
    public string CardNumber { get; set; } = string.Empty;
}

// Query with manual formatting and encryption
var context = new FieldEncryptionContext { ContextId = "tenant-123" };
var plaintext = System.Text.Encoding.UTF8.GetBytes(cardNumber);
var ciphertext = await encryptor.EncryptAsync(plaintext, "CardNumber", context);
var encryptedCard = Convert.ToBase64String(ciphertext);

await table.Query()
    .Where($"{TransactionFields.AccountId} = {{0}} AND " +
           $"{TransactionFields.CreatedAt} > {{0:o}}", accountId, date)
    .WithFilter($"{TransactionFields.Amount} > {{0:F2}} AND " +
                $"{TransactionFields.CardNumber} = {{1}}", 
                100.00m, encryptedCard)
    .ExecuteAsync();

// Log output showed sensitive data:
// Filter: amount > :p0 AND card_number = :p1
// Parameters: { :p0 = "100.00", :p1 = "base64encryptedvalue" }
```

### After

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string AccountId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TransactionId { get; set; } = string.Empty;
    
    // Format defined once
    [DynamoDbAttribute("created_at", Format = "o")]
    public DateTime CreatedAt { get; set; }
    
    // Format defined once
    [DynamoDbAttribute("amount", Format = "F2")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("card_number")]
    [Encrypted]
    [Sensitive]  // Automatically redacted from logs
    public string CardNumber { get; set; } = string.Empty;
}

// Query with automatic formatting, encryption, and redaction
EncryptionContext.Current = "tenant-123";

await table.Query<Transaction>()
    .Where(x => x.AccountId == accountId && x.CreatedAt > date)
    .WithFilter<Transaction>(x => 
        x.Amount > 100.00m &&
        x.CardNumber == table.Encrypt(cardNumber, "CardNumber"))
    .ToListAsync();

// Log output with redaction:
// Filter: amount > :p0 AND card_number = :p1
// Parameters: { :p0 = "100.00", :p1 = [REDACTED] }
```

### Benefits

1. **Format Property**: No need to specify `:o` and `:F2` in every query
2. **Sensitive Redaction**: Card number automatically redacted from logs
3. **Manual Encryption**: Simple `table.Encrypt()` instead of manual encryption code
4. **Type Safety**: LINQ expressions catch errors at compile time
5. **Consistency**: Format and encryption context defined once

## Backward Compatibility

All improvements are **fully backward compatible**:

- ✅ Existing code continues to work without changes
- ✅ Format specifiers in string expressions still work
- ✅ Manual encryption code still works
- ✅ Existing `[Sensitive]` attributes work with new features
- ✅ No breaking changes to existing APIs

You can migrate incrementally:
1. Add Format properties to frequently-used properties
2. Migrate to LINQ expressions for type safety
3. Update encryption code to use new helpers
4. Test and verify logging redaction

## See Also

- **[Attribute Reference](AttributeReference.md)** - Complete attribute documentation
- **[Format Specifiers](FormatSpecifiers.md)** - Format string reference
- **[LINQ Expressions](../core-features/LinqExpressions.md)** - LINQ expression guide
- **[Field-Level Security](../advanced-topics/FieldLevelSecurity.md)** - Encryption and redaction guide
- **[Logging Configuration](../core-features/LoggingConfiguration.md)** - Configure logging

---

[Back to Reference](README.md) | [Back to Documentation Home](../README.md)
