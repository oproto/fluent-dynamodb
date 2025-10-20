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

## Write Transactions

Write transactions allow you to perform multiple write operations atomically.

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
var response = await new TransactWriteItemsRequestBuilder(client)
    .Put(userTable, put => put
        .WithItem(user, UserMapper.ToAttributeMap)
        .Where($"attribute_not_exists({UserFields.UserId})"))
    .Put(auditTable, put => put
        .WithItem(auditEntry, AuditMapper.ToAttributeMap))
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

await new TransactWriteItemsRequestBuilder(client)
    .Put(userTable, put => put
        .WithItem(newUser, UserMapper.ToAttributeMap)
        .Where($"attribute_not_exists({UserFields.UserId})"))
    .ExecuteAsync();
```

**With Condition:**
```csharp
// Only put if item doesn't exist
.Put(userTable, put => put
    .WithItem(user, UserMapper.ToAttributeMap)
    .Where($"attribute_not_exists({UserFields.UserId})"))

// Only put if version matches
.Put(userTable, put => put
    .WithItem(user, UserMapper.ToAttributeMap)
    .Where($"{UserFields.Version} = {{0}}", currentVersion))
```

### Update Operations

Update operations modify existing items:

```csharp
await new TransactWriteItemsRequestBuilder(client)
    .Update(userTable, update => update
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
             "Jane Doe",
             DateTime.UtcNow))
    .ExecuteAsync();
```

**With Expression Formatting:**
```csharp
.Update(accountTable, update => update
    .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
    .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}, " +
         $"{AccountFields.UpdatedAt} = {{1:o}}", 
         100.00m,
         DateTime.UtcNow)
    .Where($"{AccountFields.Balance} >= {{0:F2}}", 100.00m))
```

### Delete Operations

Delete operations remove items:

```csharp
await new TransactWriteItemsRequestBuilder(client)
    .Delete(userTable, delete => delete
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Where($"attribute_exists({UserFields.UserId})"))
    .ExecuteAsync();
```

**With Condition:**
```csharp
// Only delete if status is inactive
.Delete(userTable, delete => delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "inactive"))

// Only delete if version matches
.Delete(userTable, delete => delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Version} = {{0}}", currentVersion))
```

### Condition Check Operations

Condition checks verify conditions without modifying data:

```csharp
await new TransactWriteItemsRequestBuilder(client)
    .CheckCondition(inventoryTable, check => check
        .WithKey(InventoryFields.ProductId, InventoryKeys.Pk("prod123"))
        .Where($"{InventoryFields.Quantity} >= {{0}}", requiredQuantity))
    .Update(orderTable, update => update
        .WithKey(OrderFields.OrderId, OrderKeys.Pk("order456"))
        .Set($"SET {OrderFields.Status} = {{0}}", "confirmed"))
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
        await new TransactWriteItemsRequestBuilder(client)
            // Debit from account
            .Update(accountTable, update => update
                .WithKey(AccountFields.AccountId, AccountKeys.Pk(fromAccountId))
                .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}, " +
                     $"{AccountFields.UpdatedAt} = {{1:o}}", 
                     amount,
                     DateTime.UtcNow)
                .Where($"{AccountFields.Balance} >= {{0:F2}}", amount))
            
            // Credit to account
            .Update(accountTable, update => update
                .WithKey(AccountFields.AccountId, AccountKeys.Pk(toAccountId))
                .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} + {{0:F2}}, " +
                     $"{AccountFields.UpdatedAt} = {{1:o}}", 
                     amount,
                     DateTime.UtcNow))
            
            // Create transaction record
            .Put(transactionTable, put => put
                .WithItem(new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    FromAccount = fromAccountId,
                    ToAccount = toAccountId,
                    Amount = amount,
                    Timestamp = DateTime.UtcNow
                }, TransactionMapper.ToAttributeMap))
            
            .ExecuteAsync();
        
        Console.WriteLine("Transfer successful");
    }
    catch (TransactionCanceledException ex)
    {
        Console.WriteLine($"Transfer failed: {ex.Message}");
        // Check which condition failed
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
    var transaction = new TransactWriteItemsRequestBuilder(client);
    
    // Create order
    transaction.Put(orderTable, put => put
        .WithItem(order, OrderMapper.ToAttributeMap)
        .Where($"attribute_not_exists({OrderFields.OrderId})"));
    
    // Check and update inventory for each item
    foreach (var item in items)
    {
        // Check inventory availability
        transaction.CheckCondition(inventoryTable, check => check
            .WithKey(InventoryFields.ProductId, InventoryKeys.Pk(item.ProductId))
            .Where($"{InventoryFields.Quantity} >= {{0}}", item.Quantity));
        
        // Decrement inventory
        transaction.Update(inventoryTable, update => update
            .WithKey(InventoryFields.ProductId, InventoryKeys.Pk(item.ProductId))
            .Set($"SET {InventoryFields.Quantity} = {InventoryFields.Quantity} - {{0}}", 
                 item.Quantity));
        
        // Create order item record
        transaction.Put(orderItemTable, put => put
            .WithItem(item, OrderItemMapper.ToAttributeMap));
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
        await new TransactWriteItemsRequestBuilder(client)
            // Create user record
            .Put(userTable, put => put
                .WithItem(user, UserMapper.ToAttributeMap)
                .Where($"attribute_not_exists({UserFields.UserId})"))
            
            // Create email index entry (ensures email uniqueness)
            .Put(emailIndexTable, put => put
                .WithItem(new EmailIndex
                {
                    Email = user.Email,
                    UserId = user.UserId
                }, EmailIndexMapper.ToAttributeMap)
                .Where($"attribute_not_exists({EmailIndexFields.Email})"))
            
            // Create audit log
            .Put(auditTable, put => put
                .WithItem(new AuditEntry
                {
                    Action = "USER_REGISTERED",
                    UserId = user.UserId,
                    Timestamp = DateTime.UtcNow
                }, AuditMapper.ToAttributeMap))
            
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
var response = await new TransactGetItemsRequestBuilder(client)
    .Get(userTable, get => get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123")))
    .Get(accountTable, get => get
        .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct456")))
    .ExecuteAsync();

// Process results
foreach (var item in response.Responses)
{
    if (item.Item != null)
    {
        // Process item
        Console.WriteLine($"Retrieved item from transaction");
    }
}
```

### Read Transaction with Projection

```csharp
var response = await new TransactGetItemsRequestBuilder(client)
    .Get(userTable, get => get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .WithProjection($"{UserFields.Name}, {UserFields.Email}")
        .WithAttributeName("#name", UserFields.Name)
        .WithAttributeName("#email", UserFields.Email))
    .Get(accountTable, get => get
        .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct456"))
        .WithProjection($"{AccountFields.Balance}, {AccountFields.Status}")
        .WithAttributeName("#balance", AccountFields.Balance)
        .WithAttributeName("#status", AccountFields.Status))
    .ExecuteAsync();
```

### Read Transaction Across Multiple Tables

```csharp
public async Task<(User user, Account account, List<Order> orders)> GetUserSnapshot(
    string userId,
    string accountId)
{
    var response = await new TransactGetItemsRequestBuilder(client)
        .Get(userTable, get => get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId)))
        .Get(accountTable, get => get
            .WithKey(AccountFields.AccountId, AccountKeys.Pk(accountId)))
        .Get(orderTable, get => get
            .WithKey(OrderFields.CustomerId, OrderKeys.Pk(userId))
            .WithKey(OrderFields.OrderId, OrderKeys.Sk("ORDER#LATEST")))
        .ExecuteAsync();
    
    // All items are read at the same point in time
    var user = UserMapper.FromAttributeMap(response.Responses[0].Item);
    var account = AccountMapper.FromAttributeMap(response.Responses[1].Item);
    var order = OrderMapper.FromAttributeMap(response.Responses[2].Item);
    
    return (user, account, new List<Order> { order });
}
```

**Use Case:** Get a consistent snapshot of user data, account balance, and recent orders at the exact same moment.

## Transaction Limits

### Size Limits

- **Maximum items:** 100 unique items per transaction
- **Maximum data:** 4MB total across all items
- **Item size:** Each item can be up to 400KB

### Operation Limits

**TransactWriteItems:**
- Up to 100 operations (Put, Update, Delete, ConditionCheck)
- Each item can only appear once in the transaction
- No duplicate keys allowed

**TransactGetItems:**
- Up to 100 get operations
- Each item can only appear once in the transaction
- No duplicate keys allowed

### Capacity Consumption

**Write Transactions:**
- Consume 2x the write capacity of standard writes
- Each operation consumes capacity even if the transaction fails

**Read Transactions:**
- Consume 2x the read capacity of standard reads
- All reads consume capacity even if some items don't exist

## Client Request Tokens

Use client request tokens for idempotency:

```csharp
var requestToken = Guid.NewGuid().ToString();

await new TransactWriteItemsRequestBuilder(client)
    .WithClientRequestToken(requestToken)
    .Put(userTable, put => put
        .WithItem(user, UserMapper.ToAttributeMap))
    .ExecuteAsync();

// If you retry with the same token within 10 minutes,
// DynamoDB will return the same result without re-executing
```

**Use Case:** Prevent duplicate transactions when retrying after network failures.

## Error Handling

### TransactionCanceledException

The most common exception when a transaction fails:

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await new TransactWriteItemsRequestBuilder(client)
        .Update(accountTable, update => update
            .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
            .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}", 100.00m)
            .Where($"{AccountFields.Balance} >= {{0:F2}}", 100.00m))
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    Console.WriteLine($"Transaction failed: {ex.Message}");
    
    // Check cancellation reasons
    foreach (var reason in ex.CancellationReasons)
    {
        Console.WriteLine($"Item: {reason.Item}");
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
    await transaction.ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    var hasConditionalCheckFailure = ex.CancellationReasons
        .Any(r => r.Code == "ConditionalCheckFailed");
    
    if (hasConditionalCheckFailure)
    {
        Console.WriteLine("Transaction failed due to condition check failure");
        // Handle insufficient balance, version mismatch, etc.
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

### Return Values on Condition Check Failure

Get the old values when a condition check fails:

```csharp
try
{
    await new TransactWriteItemsRequestBuilder(client)
        .Update(accountTable, update => update
            .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
            .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}", 100.00m)
            .Where($"{AccountFields.Balance} >= {{0:F2}}", 100.00m)
            .ReturnOldValuesOnConditionCheckFailure())
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    foreach (var reason in ex.CancellationReasons)
    {
        if (reason.Code == "ConditionalCheckFailed" && reason.Item != null)
        {
            var account = AccountMapper.FromAttributeMap(reason.Item);
            Console.WriteLine($"Current balance: {account.Balance}");
            Console.WriteLine($"Attempted withdrawal: 100.00");
            Console.WriteLine($"Insufficient funds");
        }
    }
}
```

### Other Exceptions

```csharp
try
{
    await transaction.ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    // Condition check failed or conflict
    Console.WriteLine("Transaction canceled");
}
catch (ValidationException ex)
{
    // Invalid transaction (duplicate keys, too many items, etc.)
    Console.WriteLine($"Validation error: {ex.Message}");
}
catch (ProvisionedThroughputExceededException ex)
{
    // Throughput exceeded - implement exponential backoff
    Console.WriteLine("Throughput exceeded");
}
catch (ResourceNotFoundException ex)
{
    // Table doesn't exist
    Console.WriteLine($"Table not found: {ex.Message}");
}
```

## Retry Strategy

Implement exponential backoff for transaction conflicts:

```csharp
public async Task<TransactWriteItemsResponse> ExecuteTransactionWithRetry(
    TransactWriteItemsRequestBuilder transaction,
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
```

## Monitoring Consumed Capacity

```csharp
var response = await new TransactWriteItemsRequestBuilder(client)
    .Put(userTable, put => put
        .WithItem(user, UserMapper.ToAttributeMap))
    .Update(accountTable, update => update
        .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
        .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} + {{0:F2}}", 100.00m))
    .ReturnTotalConsumedCapacity()
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
await new TransactWriteItemsRequestBuilder(client)
    .Update(accountTable, update => update
        .WithKey(AccountFields.AccountId, AccountKeys.Pk(fromAccount))
        .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}", amount))
    .Update(accountTable, update => update
        .WithKey(AccountFields.AccountId, AccountKeys.Pk(toAccount))
        .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} + {{0:F2}}", amount))
    .ExecuteAsync();

// ❌ Avoid - separate operations can leave inconsistent state
await table.Update.WithKey(...).Set(...).ExecuteAsync();
await table.Update.WithKey(...).Set(...).ExecuteAsync();
```

### 2. Use Condition Checks for Validation

```csharp
// ✅ Good - verify conditions before modifying data
.CheckCondition(inventoryTable, check => check
    .WithKey(InventoryFields.ProductId, InventoryKeys.Pk(productId))
    .Where($"{InventoryFields.Quantity} >= {{0}}", requiredQuantity))
.Update(orderTable, update => update
    .WithKey(OrderFields.OrderId, OrderKeys.Pk(orderId))
    .Set($"SET {OrderFields.Status} = {{0}}", "confirmed"))
```

### 3. Use Client Request Tokens for Idempotency

```csharp
// ✅ Good - prevents duplicate transactions
.WithClientRequestToken(Guid.NewGuid().ToString())
```

### 4. Handle TransactionCanceledException

```csharp
// ✅ Good - handle transaction failures
try
{
    await transaction.ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    // Check reasons and handle appropriately
}

// ❌ Avoid - ignoring transaction failures
await transaction.ExecuteAsync();
```

### 5. Keep Transactions Small

```csharp
// ✅ Good - small, focused transaction
await new TransactWriteItemsRequestBuilder(client)
    .Put(userTable, put => put.WithItem(user, UserMapper.ToAttributeMap))
    .Put(auditTable, put => put.WithItem(audit, AuditMapper.ToAttributeMap))
    .ExecuteAsync();

// ❌ Avoid - large transaction with many items
// (increases chance of conflicts and capacity issues)
```

### 6. Use Batch Operations for Independent Writes

```csharp
// ✅ Good - use batch for independent operations
await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder => { /* ... */ })
    .ExecuteAsync();

// ❌ Avoid - using transactions when atomicity isn't needed
await new TransactWriteItemsRequestBuilder(client)
    .Put(userTable, put => put.WithItem(user1, UserMapper.ToAttributeMap))
    .Put(userTable, put => put.WithItem(user2, UserMapper.ToAttributeMap))
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

## Complete Transaction Example

```csharp
public class TransactionService
{
    private readonly IAmazonDynamoDB _client;
    private readonly DynamoDbTableBase _accountTable;
    private readonly DynamoDbTableBase _transactionTable;
    
    public TransactionService(IAmazonDynamoDB client)
    {
        _client = client;
        _accountTable = new DynamoDbTableBase(client, "accounts");
        _transactionTable = new DynamoDbTableBase(client, "transactions");
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
                await new TransactWriteItemsRequestBuilder(_client)
                    .WithClientRequestToken(requestToken)
                    
                    // Debit from source account
                    .Update(_accountTable, update => update
                        .WithKey(AccountFields.AccountId, AccountKeys.Pk(fromAccountId))
                        .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0:F2}}, " +
                             $"{AccountFields.UpdatedAt} = {{1:o}}, " +
                             $"{AccountFields.Version} = {AccountFields.Version} + {{2}}", 
                             amount,
                             DateTime.UtcNow,
                             1)
                        .Where($"{AccountFields.Balance} >= {{0:F2}} AND {AccountFields.Status} = {{1}}", 
                               amount, "active")
                        .ReturnOldValuesOnConditionCheckFailure())
                    
                    // Credit to destination account
                    .Update(_accountTable, update => update
                        .WithKey(AccountFields.AccountId, AccountKeys.Pk(toAccountId))
                        .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} + {{0:F2}}, " +
                             $"{AccountFields.UpdatedAt} = {{1:o}}, " +
                             $"{AccountFields.Version} = {AccountFields.Version} + {{2}}", 
                             amount,
                             DateTime.UtcNow,
                             1)
                        .Where($"{AccountFields.Status} = {{0}}", "active"))
                    
                    // Create transaction record
                    .Put(_transactionTable, put => put
                        .WithItem(new TransactionRecord
                        {
                            TransactionId = transactionId,
                            FromAccount = fromAccountId,
                            ToAccount = toAccountId,
                            Amount = amount,
                            Status = "completed",
                            Timestamp = DateTime.UtcNow
                        }, TransactionMapper.ToAttributeMap))
                    
                    .ReturnTotalConsumedCapacity()
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
                    
                    if (reason.Code == "ConditionalCheckFailed" && reason.Item != null)
                    {
                        var account = AccountMapper.FromAttributeMap(reason.Item);
                        Console.WriteLine($"Account balance: {account.Balance}");
                    }
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

- **[Error Handling](../reference/ErrorHandling.md)** - Handle transaction errors
- **[Batch Operations](BatchOperations.md)** - Compare with batch operations
- **[Performance Optimization](../advanced-topics/PerformanceOptimization.md)** - Optimize transaction performance
- **[Basic Operations](BasicOperations.md)** - Individual CRUD operations

---

[Previous: Batch Operations](BatchOperations.md)

**See Also:**
- [Expression Formatting](ExpressionFormatting.md)
- [Querying Data](QueryingData.md)
- [Troubleshooting](../reference/Troubleshooting.md)
