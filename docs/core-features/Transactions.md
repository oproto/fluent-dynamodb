---
title: "Transactions"
category: "core-features"
order: 6
keywords: ["transactions", "ACID", "atomic", "transact write", "transact get", "condition check", "rollback"]
related: ["BasicOperations.md", "BatchOperations.md", "ExpressionFormatting.md", "../reference/ErrorHandling.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Transactions

# Transactions

[Previous: Batch Operations](BatchOperations.md)

---

DynamoDB transactions provide ACID (Atomicity, Consistency, Isolation, Durability) guarantees for multiple operations across one or more tables. All operations in a transaction succeed together or fail together, ensuring data consistency.

## Overview

DynamoDB supports two types of transactions:

**TransactWriteItems:**
- Put, Update, Delete, and ConditionCheck operations
- Up to 100 unique items or 4MB of data
- All operations succeed or all fail atomically
- Supports conditional expressions

**TransactGetItems:**
- Get operations with snapshot isolation
- Up to 100 unique items or 4MB of data
- All reads occur at the same point in time
- Provides consistent view across items

## Quick Start

The new transaction API uses static entry points and reuses existing request builders:

```csharp
// Write transaction
await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Update(pk, sk).Set(x => new { Status = "confirmed" }))
    .Add(inventoryTable.Delete(productId))
    .ExecuteAsync();

// Read transaction with deserialization
var (user, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();
```


## Write Transactions

Write transactions allow you to perform multiple write operations atomically using the `DynamoDbTransactions.Write` entry point.

### Basic Transaction

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Create user and audit log atomically
await DynamoDbTransactions.Write
    .Add(userTable.Put(user).Where("attribute_not_exists(pk)"))
    .Add(auditTable.Put(auditEntry))
    .ExecuteAsync();
```

### Put Operations

Put operations create new items or replace existing items:

```csharp
var newUser = new User
{
    UserId = "user123",
    Name = "John Doe",
    Email = "john@example.com"
};

await DynamoDbTransactions.Write
    .Add(userTable.Put(newUser).Where("attribute_not_exists(pk)"))
    .ExecuteAsync();
```

**With String Formatting:**
```csharp
await DynamoDbTransactions.Write
    .Add(userTable.Put(user).Where("version = {0}", currentVersion))
    .ExecuteAsync();
```

**With Lambda Expressions:**
```csharp
await DynamoDbTransactions.Write
    .Add(userTable.Put(user).Where(x => x.Version == currentVersion))
    .ExecuteAsync();
```


### Update Operations

Update operations modify existing items using the fluent update builder:

```csharp
// String formatting
await DynamoDbTransactions.Write
    .Add(userTable.Update(userId).Set("name = {0}, updatedAt = {1:o}", 
         "Jane Doe", DateTime.UtcNow))
    .ExecuteAsync();

// Lambda expressions (type-safe)
await DynamoDbTransactions.Write
    .Add(userTable.Update(userId).Set(x => new UpdateModel
    {
        Name = "Jane Doe",
        UpdatedAt = DateTime.UtcNow
    }))
    .ExecuteAsync();

// Source-generated methods (no generic parameters)
await DynamoDbTransactions.Write
    .Add(userTable.Update("user123", "profile")
        .Set(x => new { Name = "Jane Doe" }))
    .ExecuteAsync();
```

**With Conditions:**
```csharp
// Conditional update with string formatting
await DynamoDbTransactions.Write
    .Add(accountTable.Update(accountId)
        .Set("balance = balance - {0:F2}, updatedAt = {1:o}", 
             100.00m, DateTime.UtcNow)
        .Where("balance >= {0:F2}", 100.00m))
    .ExecuteAsync();

// Conditional update with lambda expressions
await DynamoDbTransactions.Write
    .Add(accountTable.Update(accountId)
        .Set(x => new UpdateModel
        {
            Balance = x.Balance - 100.00m,
            UpdatedAt = DateTime.UtcNow
        })
        .Where(x => x.Balance >= 100.00m))
    .ExecuteAsync();
```


### Delete Operations

Delete operations remove items:

```csharp
// Simple delete
await DynamoDbTransactions.Write
    .Add(userTable.Delete(userId))
    .ExecuteAsync();

// Delete with composite key (source-generated)
await DynamoDbTransactions.Write
    .Add(orderTable.Delete("customer123", "order456"))
    .ExecuteAsync();

// Conditional delete with string formatting
await DynamoDbTransactions.Write
    .Add(userTable.Delete(userId).Where("status = {0}", "inactive"))
    .ExecuteAsync();

// Conditional delete with lambda expressions
await DynamoDbTransactions.Write
    .Add(userTable.Delete(userId).Where(x => x.Status == "inactive"))
    .ExecuteAsync();
```

### Condition Check Operations

Condition checks verify conditions without modifying data:

```csharp
// Check inventory before confirming order
await DynamoDbTransactions.Write
    .Add(inventoryTable.ConditionCheck(productId)
        .Where("quantity >= {0}", requiredQuantity))
    .Add(orderTable.Update(orderId).Set("status = {0}", "confirmed"))
    .ExecuteAsync();

// With lambda expressions
await DynamoDbTransactions.Write
    .Add(inventoryTable.ConditionCheck(productId)
        .Where(x => x.Quantity >= requiredQuantity))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ExecuteAsync();

// Source-generated ConditionCheck with composite keys
await DynamoDbTransactions.Write
    .Add(orderTable.ConditionCheck("customer123", "order456")
        .Where(x => x.Status == "pending"))
    .Add(paymentTable.Put(payment))
    .ExecuteAsync();
```

**Use Case:** Verify inventory before confirming an order. If inventory is insufficient, the entire transaction fails.


## Complete Transaction Examples

### Money Transfer

```csharp
public async Task TransferMoney(
    string fromAccountId,
    string toAccountId,
    decimal amount)
{
    try
    {
        await DynamoDbTransactions.Write
            // Debit from account
            .Add(accountTable.Update(fromAccountId)
                .Set(x => new UpdateModel
                {
                    Balance = x.Balance - amount,
                    UpdatedAt = DateTime.UtcNow
                })
                .Where(x => x.Balance >= amount))
            
            // Credit to account
            .Add(accountTable.Update(toAccountId)
                .Set(x => new UpdateModel
                {
                    Balance = x.Balance + amount,
                    UpdatedAt = DateTime.UtcNow
                }))
            
            // Create transaction record
            .Add(transactionTable.Put(new Transaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                FromAccount = fromAccountId,
                ToAccount = toAccountId,
                Amount = amount,
                Timestamp = DateTime.UtcNow
            }))
            
            .ExecuteAsync();
        
        Console.WriteLine("Transfer successful");
    }
    catch (TransactionCanceledException ex)
    {
        Console.WriteLine($"Transfer failed: {ex.Message}");
        foreach (var reason in ex.CancellationReasons)
        {
            Console.WriteLine($"Reason: {reason.Code} - {reason.Message}");
        }
    }
}
```


### Order Processing

```csharp
public async Task ProcessOrder(Order order, List<OrderItem> items)
{
    var transaction = DynamoDbTransactions.Write;
    
    // Create order
    transaction.Add(orderTable.Put(order)
        .Where("attribute_not_exists(orderId)"));
    
    // Check and update inventory for each item
    foreach (var item in items)
    {
        // Check inventory availability
        transaction.Add(inventoryTable.ConditionCheck(item.ProductId)
            .Where(x => x.Quantity >= item.Quantity));
        
        // Decrement inventory
        transaction.Add(inventoryTable.Update(item.ProductId)
            .Set(x => new { Quantity = x.Quantity - item.Quantity }));
        
        // Create order item record
        transaction.Add(orderItemTable.Put(item));
    }
    
    try
    {
        await transaction.ExecuteAsync();
        Console.WriteLine("Order processed successfully");
    }
    catch (TransactionCanceledException ex)
    {
        Console.WriteLine("Order processing failed - insufficient inventory or order already exists");
    }
}
```

### User Registration with Unique Email

```csharp
public async Task RegisterUser(User user)
{
    try
    {
        await DynamoDbTransactions.Write
            // Create user record
            .Add(userTable.Put(user)
                .Where("attribute_not_exists(userId)"))
            
            // Create email index entry (ensures email uniqueness)
            .Add(emailIndexTable.Put(new EmailIndex
            {
                Email = user.Email,
                UserId = user.UserId
            }).Where("attribute_not_exists(email)"))
            
            // Create audit log
            .Add(auditTable.Put(new AuditEntry
            {
                Action = "USER_REGISTERED",
                UserId = user.UserId,
                Timestamp = DateTime.UtcNow
            }))
            
            .ExecuteAsync();
        
        Console.WriteLine("User registered successfully");
    }
    catch (TransactionCanceledException ex)
    {
        Console.WriteLine("Registration failed - user ID or email already exists");
    }
}
```


## Read Transactions

Read transactions provide snapshot isolation, ensuring all reads occur at the same point in time.

### Basic Read Transaction

```csharp
// Execute and get response wrapper
var response = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(accountTable.Get(accountId))
    .ExecuteAsync();

// Deserialize items by index
var user = response.GetItem<User>(0);
var account = response.GetItem<Account>(1);
```

### ExecuteAndMapAsync - Tuple Destructuring

For convenience with small numbers of items, use `ExecuteAndMapAsync` to get a tuple:

```csharp
// 2 items
var (user, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Order>();

// 3 items
var (user, account, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId))
    .Add(accountTable.Get(accountId))
    .Add(orderTable.Get(orderId))
    .ExecuteAndMapAsync<User, Account, Order>();

// Up to 8 items supported
var (item1, item2, item3, item4, item5, item6, item7, item8) = 
    await DynamoDbTransactions.Get
        .Add(table1.Get(key1))
        .Add(table2.Get(key2))
        // ... up to 8 items
        .ExecuteAndMapAsync<T1, T2, T3, T4, T5, T6, T7, T8>();
```


### Response Deserialization Methods

The `TransactionGetResponse` provides multiple ways to deserialize items:

```csharp
var response = await DynamoDbTransactions.Get
    .Add(userTable.Get("user1"))
    .Add(userTable.Get("user2"))
    .Add(userTable.Get("user3"))
    .Add(orderTable.Get("order1"))
    .ExecuteAsync();

// Get single item by index
var user1 = response.GetItem<User>(0);

// Get multiple items of same type by indices
var users = response.GetItems<User>(0, 1, 2);

// Get contiguous range of items
var allUsers = response.GetItemsRange<User>(0, 2); // indices 0, 1, 2

// Get item from different table
var order = response.GetItem<Order>(3);

// Check total count
Console.WriteLine($"Retrieved {response.Count} items");

// Access raw AWS SDK response if needed
var rawResponse = response.RawResponse;
```

### Read Transaction with Projection

```csharp
// With projection expressions
var response = await DynamoDbTransactions.Get
    .Add(userTable.Get(userId).WithProjection("name, email"))
    .Add(accountTable.Get(accountId).WithProjection("balance, status"))
    .ExecuteAsync();

var user = response.GetItem<User>(0);
var account = response.GetItem<Account>(1);
```

### Read Transaction with Source-Generated Methods

```csharp
// Using source-generated Get methods (no generic parameters)
var (user, order) = await DynamoDbTransactions.Get
    .Add(userTable.Get("user123"))
    .Add(orderTable.Get("customer123", "order456"))
    .ExecuteAndMapAsync<User, Order>();
```


### Handling Null Items

Items that don't exist return null:

```csharp
var response = await DynamoDbTransactions.Get
    .Add(userTable.Get("user123"))
    .Add(userTable.Get("nonexistent"))
    .ExecuteAsync();

var user1 = response.GetItem<User>(0); // Returns User object
var user2 = response.GetItem<User>(1); // Returns null

if (user2 == null)
{
    Console.WriteLine("User not found");
}
```

### Read Transaction Across Multiple Tables

```csharp
public async Task<UserSnapshot> GetUserSnapshot(string userId, string accountId)
{
    var (user, account, recentOrder) = await DynamoDbTransactions.Get
        .Add(userTable.Get(userId))
        .Add(accountTable.Get(accountId))
        .Add(orderTable.Get(userId, "ORDER#LATEST"))
        .ExecuteAndMapAsync<User, Account, Order>();
    
    // All items are read at the same point in time
    return new UserSnapshot
    {
        User = user,
        Account = account,
        RecentOrder = recentOrder
    };
}
```

**Use Case:** Get a consistent snapshot of user data, account balance, and recent orders at the exact same moment.


## Client Configuration

The transaction builders automatically infer the DynamoDB client from the first request builder, or you can explicitly specify it.

### Automatic Client Inference

```csharp
// Client is automatically extracted from userTable
await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ExecuteAsync();
```

**How it works:**
1. The client is extracted from the first builder added (`userTable.Put(user)`)
2. Subsequent builders are verified to use the same client instance
3. If builders use different clients, an `InvalidOperationException` is thrown

### Explicit Client with WithClient()

```csharp
var scopedClient = GetScopedDynamoDbClient(); // e.g., with STS credentials

await DynamoDbTransactions.Write
    .WithClient(scopedClient)
    .Add(userTable.Put(user))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ExecuteAsync();
```

### Client as ExecuteAsync Parameter

```csharp
var client = GetDynamoDbClient();

await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ExecuteAsync(client); // Highest precedence
```

### Client Precedence

The client is determined in this order (highest to lowest precedence):

1. **ExecuteAsync parameter** - `ExecuteAsync(client)`
2. **Explicit WithClient()** - `.WithClient(client)`
3. **Inferred from first builder** - Extracted automatically


### Use Cases for Explicit Client

**STS Credentials (Scoped IAM):**
```csharp
// Assume role for cross-account access
var stsClient = new AmazonSecurityTokenServiceClient();
var assumeRoleResponse = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
{
    RoleArn = "arn:aws:iam::123456789012:role/CrossAccountRole",
    RoleSessionName = "transaction-session"
});

var credentials = assumeRoleResponse.Credentials;
var scopedClient = new AmazonDynamoDBClient(
    credentials.AccessKeyId,
    credentials.SecretAccessKey,
    credentials.SessionToken
);

await DynamoDbTransactions.Write
    .WithClient(scopedClient)
    .Add(userTable.Put(user))
    .ExecuteAsync();
```

**Multi-Region Setup:**
```csharp
var usEastClient = new AmazonDynamoDBClient(RegionEndpoint.USEast1);
var euWestClient = new AmazonDynamoDBClient(RegionEndpoint.EUWest1);

// Transaction in US East region
await DynamoDbTransactions.Write
    .WithClient(usEastClient)
    .Add(usTable.Put(user))
    .ExecuteAsync();

// Transaction in EU West region
await DynamoDbTransactions.Write
    .WithClient(euWestClient)
    .Add(euTable.Put(user))
    .ExecuteAsync();
```


## Transaction Limits and Validation

### Size Limits

- **Maximum items:** 100 unique items per transaction
- **Maximum data:** 4MB total across all items
- **Item size:** Each item can be up to 400KB

### Validation Errors

The transaction builder validates operations before execution:

```csharp
// Empty transaction
try
{
    await DynamoDbTransactions.Write.ExecuteAsync();
}
catch (InvalidOperationException ex)
{
    // "Transaction contains no operations. Add at least one operation using Add()."
}

// Too many operations
try
{
    var transaction = DynamoDbTransactions.Write;
    for (int i = 0; i < 101; i++)
    {
        transaction.Add(userTable.Put(new User { UserId = $"user{i}" }));
    }
    await transaction.ExecuteAsync();
}
catch (ValidationException ex)
{
    // "Transaction contains 101 operations, but DynamoDB supports a maximum of 100 operations per transaction."
}

// Missing client
try
{
    await DynamoDbTransactions.Write
        .Add(userTable.Put(user))
        .ExecuteAsync(null); // Explicitly passing null
}
catch (InvalidOperationException ex)
{
    // "No DynamoDB client specified. Either pass a client to ExecuteAsync(), call WithClient(), or add at least one request builder to infer the client."
}
```

### Client Mismatch Detection

```csharp
var client1 = new AmazonDynamoDBClient(RegionEndpoint.USEast1);
var client2 = new AmazonDynamoDBClient(RegionEndpoint.USWest2);

var table1 = new UserTable(client1);
var table2 = new OrderTable(client2);

try
{
    await DynamoDbTransactions.Write
        .Add(table1.Put(user))
        .Add(table2.Put(order)) // Different client!
        .ExecuteAsync();
}
catch (InvalidOperationException ex)
{
    // "All request builders in a transaction must use the same DynamoDB client instance. Use WithClient() to explicitly specify a client if needed."
}
```


## Transaction-Level Configuration

Configure transaction-level settings that apply to the entire transaction:

### Return Consumed Capacity

```csharp
var response = await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync();

// Check capacity consumption
if (response.ConsumedCapacity != null)
{
    foreach (var capacity in response.ConsumedCapacity)
    {
        Console.WriteLine($"Table: {capacity.TableName}");
        Console.WriteLine($"Capacity: {capacity.CapacityUnits} units");
    }
}
```

### Client Request Token (Idempotency)

```csharp
var requestToken = Guid.NewGuid().ToString();

await DynamoDbTransactions.Write
    .WithClientRequestToken(requestToken)
    .Add(userTable.Put(user))
    .ExecuteAsync();

// If you retry with the same token within 10 minutes,
// DynamoDB will return the same result without re-executing
```

### Return Item Collection Metrics

```csharp
var response = await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .ReturnItemCollectionMetrics()
    .ExecuteAsync();

// Check item collection metrics
if (response.ItemCollectionMetrics != null)
{
    foreach (var metric in response.ItemCollectionMetrics)
    {
        Console.WriteLine($"Table: {metric.Key}");
        Console.WriteLine($"Size: {metric.Value.ItemCollectionKey}");
    }
}
```


## Encryption Support

Field encryption works automatically in transactions when using lambda expressions with encrypted properties:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    public string UserId { get; set; } = string.Empty;
    
    [Encrypted]
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

// Encryption happens automatically
await DynamoDbTransactions.Write
    .Add(userTable.Update(userId)
        .Set(x => new UpdateModel
        {
            SocialSecurityNumber = "123-45-6789" // Encrypted before sending
        }))
    .ExecuteAsync();
```

**How it works:**
1. Lambda expressions identify properties marked with `[Encrypted]`
2. Before building the final request, the transaction builder encrypts all parameters
3. Encryption uses the configured `IFieldEncryptor` from the table

**Error handling:**
```csharp
try
{
    await DynamoDbTransactions.Write
        .Add(userTable.Update(userId)
            .Set(x => new { SocialSecurityNumber = "123-45-6789" }))
        .ExecuteAsync();
}
catch (InvalidOperationException ex)
{
    // "Field encryption is required for property 'SocialSecurityNumber' but no IFieldEncryptor is configured."
}
catch (FieldEncryptionException ex)
{
    // "Failed to encrypt field 'SocialSecurityNumber': [details]"
}
```


## Error Handling

### TransactionCanceledException

The most common exception when a transaction fails:

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await DynamoDbTransactions.Write
        .Add(accountTable.Update(accountId)
            .Set(x => new { Balance = x.Balance - 100.00m })
            .Where(x => x.Balance >= 100.00m))
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    Console.WriteLine($"Transaction failed: {ex.Message}");
    
    // Check cancellation reasons
    foreach (var reason in ex.CancellationReasons)
    {
        Console.WriteLine($"Code: {reason.Code}");
        Console.WriteLine($"Message: {reason.Message}");
        
        // Common codes:
        // - ConditionalCheckFailed: Condition expression failed
        // - ItemCollectionSizeLimitExceeded: Item collection too large
        // - TransactionConflict: Concurrent transaction conflict
        // - ProvisionedThroughputExceeded: Capacity exceeded
        // - ValidationError: Invalid request
    }
}
```

### Handling Specific Failure Reasons

```csharp
try
{
    await DynamoDbTransactions.Write
        .Add(accountTable.Update(accountId)
            .Set(x => new { Balance = x.Balance - amount })
            .Where(x => x.Balance >= amount))
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    var hasConditionalCheckFailure = ex.CancellationReasons
        .Any(r => r.Code == "ConditionalCheckFailed");
    
    if (hasConditionalCheckFailure)
    {
        Console.WriteLine("Insufficient balance");
        // Handle insufficient balance
    }
    
    var hasConflict = ex.CancellationReasons
        .Any(r => r.Code == "TransactionConflict");
    
    if (hasConflict)
    {
        Console.WriteLine("Transaction conflict - retry with exponential backoff");
        // Implement retry logic
    }
}
```


### Retry Strategy

Implement exponential backoff for transaction conflicts:

```csharp
public async Task<TransactWriteItemsResponse> ExecuteTransactionWithRetry(
    TransactionWriteBuilder transaction,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await transaction.ExecuteAsync();
        }
        catch (TransactionCanceledException ex)
        {
            var hasConflict = ex.CancellationReasons
                .Any(r => r.Code == "TransactionConflict");
            
            if (hasConflict && i < maxRetries - 1)
            {
                // Exponential backoff: 100ms, 200ms, 400ms
                var delayMs = 100 * (int)Math.Pow(2, i);
                Console.WriteLine($"Transaction conflict, retry {i + 1} after {delayMs}ms");
                await Task.Delay(delayMs);
            }
            else
            {
                throw;
            }
        }
    }
    
    throw new Exception("Transaction failed after maximum retries");
}

// Usage
var transaction = DynamoDbTransactions.Write
    .Add(accountTable.Update(fromAccountId)
        .Set(x => new { Balance = x.Balance - amount })
        .Where(x => x.Balance >= amount))
    .Add(accountTable.Update(toAccountId)
        .Set(x => new { Balance = x.Balance + amount }));

await ExecuteTransactionWithRetry(transaction);
```


## Capacity Consumption

Transactions consume 2x the capacity of standard operations:

**Write Transactions:**
- Consume 2x the write capacity of standard writes
- Each operation consumes capacity even if the transaction fails

**Read Transactions:**
- Consume 2x the read capacity of standard reads
- All reads consume capacity even if some items don't exist

```csharp
var response = await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(accountTable.Update(accountId).Set(x => new { Balance = x.Balance + 100.00m }))
    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    .ExecuteAsync();

// Check capacity consumption
if (response.ConsumedCapacity != null)
{
    foreach (var capacity in response.ConsumedCapacity)
    {
        Console.WriteLine($"Table: {capacity.TableName}");
        Console.WriteLine($"Capacity: {capacity.CapacityUnits} units");
        Console.WriteLine($"Read: {capacity.ReadCapacityUnits} RCUs");
        Console.WriteLine($"Write: {capacity.WriteCapacityUnits} WCUs");
    }
}
```


## Best Practices

### 1. Use Transactions for ACID Requirements

```csharp
// ✅ Good - use transactions for atomic operations
await DynamoDbTransactions.Write
    .Add(accountTable.Update(fromAccount)
        .Set(x => new { Balance = x.Balance - amount }))
    .Add(accountTable.Update(toAccount)
        .Set(x => new { Balance = x.Balance + amount }))
    .ExecuteAsync();

// ❌ Avoid - separate operations can leave inconsistent state
await accountTable.Update(fromAccount).Set(x => new { Balance = x.Balance - amount }).ExecuteAsync();
await accountTable.Update(toAccount).Set(x => new { Balance = x.Balance + amount }).ExecuteAsync();
```

### 2. Use Condition Checks for Validation

```csharp
// ✅ Good - verify conditions before modifying data
await DynamoDbTransactions.Write
    .Add(inventoryTable.ConditionCheck(productId)
        .Where(x => x.Quantity >= requiredQuantity))
    .Add(orderTable.Update(orderId).Set(x => new { Status = "confirmed" }))
    .ExecuteAsync();
```

### 3. Use Lambda Expressions for Type Safety

```csharp
// ✅ Good - compile-time type checking
await DynamoDbTransactions.Write
    .Add(userTable.Update(userId)
        .Set(x => new UpdateModel { Name = "John", Age = 30 }))
    .ExecuteAsync();

// ⚠️ Acceptable - string formatting (less type-safe)
await DynamoDbTransactions.Write
    .Add(userTable.Update(userId)
        .Set("name = {0}, age = {1}", "John", 30))
    .ExecuteAsync();
```

### 4. Use Source-Generated Methods

```csharp
// ✅ Good - no generic parameters, cleaner code
await DynamoDbTransactions.Write
    .Add(userTable.Update("user123", "profile")
        .Set(x => new { Name = "John" }))
    .ExecuteAsync();

// ⚠️ Acceptable - generic parameters required
await DynamoDbTransactions.Write
    .Add(userTable.Update<User>().WithKey("pk", "user123").WithKey("sk", "profile")
        .Set(x => new { Name = "John" }))
    .ExecuteAsync();
```


### 5. Handle TransactionCanceledException

```csharp
// ✅ Good - handle transaction failures
try
{
    await DynamoDbTransactions.Write
        .Add(userTable.Put(user))
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    // Check reasons and handle appropriately
    foreach (var reason in ex.CancellationReasons)
    {
        Console.WriteLine($"{reason.Code}: {reason.Message}");
    }
}

// ❌ Avoid - ignoring transaction failures
await DynamoDbTransactions.Write.Add(userTable.Put(user)).ExecuteAsync();
```

### 6. Use Client Request Tokens for Idempotency

```csharp
// ✅ Good - prevents duplicate transactions
var requestToken = Guid.NewGuid().ToString();
await DynamoDbTransactions.Write
    .WithClientRequestToken(requestToken)
    .Add(userTable.Put(user))
    .ExecuteAsync();
```

### 7. Keep Transactions Small

```csharp
// ✅ Good - small, focused transaction
await DynamoDbTransactions.Write
    .Add(userTable.Put(user))
    .Add(auditTable.Put(audit))
    .ExecuteAsync();

// ❌ Avoid - large transaction with many items
// (increases chance of conflicts and capacity issues)
```

### 8. Use Batch Operations for Independent Writes

```csharp
// ✅ Good - use batch for independent operations
await DynamoDbBatch.Write
    .Add(userTable.Put(user1))
    .Add(userTable.Put(user2))
    .ExecuteAsync();

// ❌ Avoid - using transactions when atomicity isn't needed
await DynamoDbTransactions.Write
    .Add(userTable.Put(user1))
    .Add(userTable.Put(user2))
    .ExecuteAsync();
```


## Transactions vs Batch Operations

| Feature | Transactions | Batch Operations |
|---------|-------------|------------------|
| **Atomicity** | All succeed or all fail | Partial success possible |
| **Capacity Cost** | 2x standard operations | 1x standard operations |
| **Conditional Expressions** | Supported | Not supported |
| **Max Items** | 100 items or 4MB | 25 writes / 100 reads |
| **Use Case** | ACID requirements | Independent bulk operations |

**Choose Transactions When:**
- Operations must succeed or fail together
- You need conditional expressions across items
- Data consistency is critical

**Choose Batch Operations When:**
- Operations are independent
- Partial success is acceptable
- Cost optimization is important

See [Batch Operations](BatchOperations.md) for batch operation details.


## Complete Transaction Example

```csharp
public class TransactionService
{
    private readonly UserTable _userTable;
    private readonly AccountTable _accountTable;
    private readonly TransactionTable _transactionTable;
    
    public TransactionService(
        UserTable userTable,
        AccountTable accountTable,
        TransactionTable transactionTable)
    {
        _userTable = userTable;
        _accountTable = accountTable;
        _transactionTable = transactionTable;
    }
    
    public async Task<bool> TransferFunds(
        string fromAccountId,
        string toAccountId,
        decimal amount,
        int maxRetries = 3)
    {
        var transactionId = Guid.NewGuid().ToString();
        var requestToken = Guid.NewGuid().ToString();
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await DynamoDbTransactions.Write
                    .WithClientRequestToken(requestToken)
                    
                    // Debit from source account
                    .Add(_accountTable.Update(fromAccountId)
                        .Set(x => new UpdateModel
                        {
                            Balance = x.Balance - amount,
                            UpdatedAt = DateTime.UtcNow,
                            Version = x.Version + 1
                        })
                        .Where(x => x.Balance >= amount && x.Status == "active"))
                    
                    // Credit to destination account
                    .Add(_accountTable.Update(toAccountId)
                        .Set(x => new UpdateModel
                        {
                            Balance = x.Balance + amount,
                            UpdatedAt = DateTime.UtcNow,
                            Version = x.Version + 1
                        })
                        .Where(x => x.Status == "active"))
                    
                    // Create transaction record
                    .Add(_transactionTable.Put(new TransactionRecord
                    {
                        TransactionId = transactionId,
                        FromAccount = fromAccountId,
                        ToAccount = toAccountId,
                        Amount = amount,
                        Status = "completed",
                        Timestamp = DateTime.UtcNow
                    }))
                    
                    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
                    .ExecuteAsync();
                
                Console.WriteLine($"Transfer successful: {amount:C} from {fromAccountId} to {toAccountId}");
                return true;
            }
            catch (TransactionCanceledException ex)
            {
                var hasConflict = ex.CancellationReasons
                    .Any(r => r.Code == "TransactionConflict");
                
                if (hasConflict && attempt < maxRetries - 1)
                {
                    var delayMs = 100 * (int)Math.Pow(2, attempt);
                    Console.WriteLine($"Transaction conflict, retry {attempt + 1} after {delayMs}ms");
                    await Task.Delay(delayMs);
                    continue;
                }
                
                // Log failure reasons
                foreach (var reason in ex.CancellationReasons)
                {
                    Console.WriteLine($"Failure: {reason.Code} - {reason.Message}");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transaction error: {ex.Message}");
                return false;
            }
        }
        
        Console.WriteLine("Transaction failed after maximum retries");
        return false;
    }
}
```

## Next Steps

- **[Batch Operations](BatchOperations.md)** - Compare with batch operations
- **[Expression Formatting](ExpressionFormatting.md)** - String formatting syntax
- **[LINQ Expressions](LinqExpressions.md)** - Type-safe lambda expressions
- **[Error Handling](../reference/ErrorHandling.md)** - Handle transaction errors

---

[Previous: Batch Operations](BatchOperations.md)

**See Also:**
- [Basic Operations](BasicOperations.md)
- [Querying Data](QueryingData.md)
- [Troubleshooting](../reference/Troubleshooting.md)
