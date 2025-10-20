---
title: "Error Handling"
category: "reference"
order: 3
keywords: ["errors", "exceptions", "retry", "conditional check", "throughput", "validation", "FluentResults"]
related: ["Troubleshooting.md", "FormatSpecifiers.md"]
---

[Documentation](../README.md) > [Reference](README.md) > Error Handling

# Error Handling

---

This guide covers common DynamoDB exceptions, how to handle them, and strategies for building resilient applications with Oproto.FluentDynamoDb.

## Overview

DynamoDB operations can fail for various reasons: conditional checks, throughput limits, validation errors, or resource issues. Understanding these exceptions and handling them appropriately is essential for building robust applications.

## Common DynamoDB Exceptions

### ConditionalCheckFailedException

**When It Occurs:**
- A condition expression evaluates to false
- An item already exists when using `attribute_not_exists()`
- An item doesn't exist when checking for its presence
- Optimistic locking fails due to concurrent modifications

**Exception Type:** `Amazon.DynamoDBv2.Model.ConditionalCheckFailedException`

**Example Scenarios:**

```csharp
using Amazon.DynamoDBv2.Model;

// Scenario 1: Preventing duplicate items
try
{
    await table.Put
        .WithItem(user)
        .WithCondition($"attribute_not_exists({UserFields.UserId})")
        .ExecuteAsync();
    
    Console.WriteLine("User created successfully");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("User already exists");
    // Handle duplicate - maybe return existing user or throw custom exception
}

// Scenario 2: Optimistic locking
try
{
    await table.Update
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Version} = {UserFields.Version} + {{1}}", 
             "New Name", 1)
        .WithCondition($"{UserFields.Version} = {{0}}", currentVersion)
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    throw new InvalidOperationException(
        "User was modified by another process. Please refresh and try again.");
}

// Scenario 3: Conditional delete
try
{
    await table.Delete
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .WithCondition($"{UserFields.Status} = {{0}}", "inactive")
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    throw new InvalidOperationException(
        "Cannot delete user - status is not inactive");
}
```

**Handling Strategy:**
- Catch and convert to domain-specific exceptions
- Provide clear error messages to users
- Implement retry logic for optimistic locking scenarios
- Log the failure for monitoring


### ProvisionedThroughputExceededException

**When It Occurs:**
- Request rate exceeds provisioned read or write capacity
- Too many requests in a short time period
- Hot partition receiving disproportionate traffic

**Exception Type:** `Amazon.DynamoDBv2.Model.ProvisionedThroughputExceededException`

**Example Scenarios:**

```csharp
// Basic handling with retry
try
{
    await table.Put.WithItem(user).ExecuteAsync();
}
catch (ProvisionedThroughputExceededException)
{
    // Implement exponential backoff
    await Task.Delay(TimeSpan.FromMilliseconds(100));
    await table.Put.WithItem(user).ExecuteAsync(); // Retry
}

// Exponential backoff with multiple retries
public async Task<T> ExecuteWithRetry<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    int baseDelayMs = 100)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (ProvisionedThroughputExceededException) when (i < maxRetries - 1)
        {
            // Exponential backoff: 100ms, 200ms, 400ms
            var delay = baseDelayMs * Math.Pow(2, i);
            await Task.Delay(TimeSpan.FromMilliseconds(delay));
        }
    }
    
    // Final attempt without catching
    return await operation();
}

// Usage
var user = await ExecuteWithRetry(async () =>
{
    var response = await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .ExecuteAsync<User>();
    return response.Item;
});
```

**Handling Strategy:**
- Implement exponential backoff with jitter
- Use batch operations to reduce request count
- Monitor CloudWatch metrics to identify hot partitions
- Consider switching to on-demand billing mode
- Cache frequently accessed data

### ResourceNotFoundException

**When It Occurs:**
- The specified table doesn't exist
- The specified index doesn't exist
- Table is being created or deleted

**Exception Type:** `Amazon.DynamoDBv2.Model.ResourceNotFoundException`

**Example Scenarios:**

```csharp
try
{
    await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .ExecuteAsync<User>();
}
catch (ResourceNotFoundException ex)
{
    Console.WriteLine($"Table '{ex.Message}' does not exist");
    // Handle missing table - maybe create it or fail gracefully
}

// Checking if table exists before operations
public async Task<bool> TableExistsAsync(IAmazonDynamoDB client, string tableName)
{
    try
    {
        await client.DescribeTableAsync(tableName);
        return true;
    }
    catch (ResourceNotFoundException)
    {
        return false;
    }
}
```

**Handling Strategy:**
- Verify table exists during application startup
- Implement table creation logic if needed
- Provide clear error messages
- Consider using infrastructure-as-code to manage tables


### TransactionCanceledException

**When It Occurs:**
- A condition check in a transaction fails
- A conflict occurs between concurrent transactions
- Transaction validation fails

**Exception Type:** `Amazon.DynamoDBv2.Model.TransactionCanceledException`

**Example Scenarios:**

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.TransactWrite
        .AddPut(new User { UserId = "user123", Name = "John" })
            .WithCondition($"attribute_not_exists({UserFields.UserId})")
        .AddUpdate()
            .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
            .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0}}", 100m)
            .WithCondition($"{AccountFields.Balance} >= {{0}}", 100m)
        .ExecuteAsync();
}
catch (TransactionCanceledException ex)
{
    // Check which condition failed
    foreach (var reason in ex.CancellationReasons)
    {
        if (reason.Code == "ConditionalCheckFailed")
        {
            Console.WriteLine($"Condition failed: {reason.Message}");
        }
    }
    
    throw new InvalidOperationException(
        "Transaction failed - either user exists or insufficient balance");
}

// Handling specific transaction failures
public async Task TransferFundsAsync(string fromAccount, string toAccount, decimal amount)
{
    try
    {
        await table.TransactWrite
            .AddUpdate()
                .WithKey(AccountFields.AccountId, AccountKeys.Pk(fromAccount))
                .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} - {{0}}", amount)
                .WithCondition($"{AccountFields.Balance} >= {{0}}", amount)
            .AddUpdate()
                .WithKey(AccountFields.AccountId, AccountKeys.Pk(toAccount))
                .Set($"SET {AccountFields.Balance} = {AccountFields.Balance} + {{0}}", amount)
            .ExecuteAsync();
    }
    catch (TransactionCanceledException ex)
    {
        var insufficientFunds = ex.CancellationReasons
            .Any(r => r.Code == "ConditionalCheckFailed");
            
        if (insufficientFunds)
        {
            throw new InvalidOperationException("Insufficient funds for transfer");
        }
        
        throw;
    }
}
```

**Handling Strategy:**
- Examine `CancellationReasons` to identify which condition failed
- Provide specific error messages based on the failure reason
- Implement retry logic for transient conflicts
- Log transaction failures for debugging

### ValidationException

**When It Occurs:**
- Invalid expression syntax
- Invalid attribute names or values
- Malformed request parameters
- Exceeding DynamoDB limits (item size, expression length, etc.)

**Exception Type:** `Amazon.DynamoDBv2.Model.ValidationException`

**Example Scenarios:**

```csharp
try
{
    // Invalid expression syntax
    await table.Query
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Where($"{UserFields.Status} == {{0}}", "active") // Wrong: == instead of =
        .ExecuteAsync<User>();
}
catch (ValidationException ex)
{
    Console.WriteLine($"Invalid expression: {ex.Message}");
    // Log and fix the expression
}

// Handling item size limits
try
{
    var largeItem = new Document { Content = new string('x', 500_000) }; // > 400KB
    await table.Put.WithItem(largeItem).ExecuteAsync();
}
catch (ValidationException ex) when (ex.Message.Contains("Item size"))
{
    throw new InvalidOperationException(
        "Document exceeds DynamoDB's 400KB item size limit. Consider storing large content in S3.");
}
```

**Handling Strategy:**
- Validate expressions before sending to DynamoDB
- Check item sizes before put operations
- Validate attribute names against reserved words
- Use the library's expression formatting to avoid syntax errors


### ItemCollectionSizeLimitExceededException

**When It Occurs:**
- A collection of items with the same partition key exceeds 10GB
- Occurs during write operations that would exceed the limit

**Exception Type:** `Amazon.DynamoDBv2.Model.ItemCollectionSizeLimitExceededException`

**Example Scenarios:**

```csharp
try
{
    await table.Put.WithItem(orderItem).ExecuteAsync();
}
catch (ItemCollectionSizeLimitExceededException)
{
    throw new InvalidOperationException(
        "Cannot add more items - partition key collection exceeds 10GB limit. " +
        "Consider using a different partition key strategy.");
}
```

**Handling Strategy:**
- Design partition keys to distribute data evenly
- Monitor item collection sizes
- Implement data archival strategies
- Consider splitting large collections across multiple partition keys

## Retry Strategies

### Exponential Backoff

Implement exponential backoff for transient errors:

```csharp
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly int _baseDelayMs;
    private readonly Random _random = new();

    public RetryPolicy(int maxRetries = 3, int baseDelayMs = 100)
    {
        _maxRetries = maxRetries;
        _baseDelayMs = baseDelayMs;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (ProvisionedThroughputExceededException ex)
            {
                lastException = ex;
                if (attempt < _maxRetries - 1)
                {
                    await DelayWithJitter(attempt);
                }
            }
            catch (TransactionCanceledException ex) 
                when (ex.Message.Contains("Transaction conflict"))
            {
                lastException = ex;
                if (attempt < _maxRetries - 1)
                {
                    await DelayWithJitter(attempt);
                }
            }
        }

        throw lastException!;
    }

    private async Task DelayWithJitter(int attempt)
    {
        // Exponential backoff with jitter
        var exponentialDelay = _baseDelayMs * Math.Pow(2, attempt);
        var jitter = _random.Next(0, (int)(exponentialDelay * 0.1));
        await Task.Delay(TimeSpan.FromMilliseconds(exponentialDelay + jitter));
    }
}

// Usage
var retryPolicy = new RetryPolicy(maxRetries: 5, baseDelayMs: 100);

var user = await retryPolicy.ExecuteAsync(async () =>
{
    var response = await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .ExecuteAsync<User>();
    return response.Item;
});
```

### Circuit Breaker Pattern

Implement a circuit breaker to prevent cascading failures:

```csharp
public class CircuitBreaker
{
    private int _failureCount;
    private DateTime _lastFailureTime;
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private CircuitState _state = CircuitState.Closed;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? timeout = null)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout ?? TimeSpan.FromMinutes(1);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitState.HalfOpen;
            }
            else
            {
                throw new InvalidOperationException("Circuit breaker is open");
            }
        }

        try
        {
            var result = await operation();
            
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
            }
            
            return result;
        }
        catch (Exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
            }

            throw;
        }
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}
```


## Validation Errors

### DynamoDbMappingException

**When It Occurs:**
- Entity mapping fails during serialization or deserialization
- Type conversion errors
- Missing required attributes

**Exception Type:** `Oproto.FluentDynamoDb.Storage.DynamoDbMappingException`

**Example Scenarios:**

```csharp
using Oproto.FluentDynamoDb.Storage;

try
{
    var response = await table.Get
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .ExecuteAsync<User>();
    
    var user = response.Item;
}
catch (DynamoDbMappingException ex)
{
    Console.WriteLine($"Mapping failed: {ex.Message}");
    // Log the error and handle gracefully
}

// Custom error handling for mapping
public async Task<User?> GetUserSafeAsync(string userId)
{
    try
    {
        var response = await table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();
        
        return response.Item;
    }
    catch (DynamoDbMappingException ex)
    {
        // Log the mapping error
        Console.WriteLine($"Failed to map user {userId}: {ex.Message}");
        
        // Return null or throw domain exception
        return null;
    }
}
```

**Handling Strategy:**
- Validate entity definitions match DynamoDB schema
- Use nullable types for optional properties
- Implement custom error handlers for mapping failures
- Log mapping errors for debugging

## FluentResults Integration (Optional)

For applications using the FluentResults pattern, the library provides optional integration:

```csharp
using FluentResults;
using Oproto.FluentDynamoDb.FluentResults;

// Extension methods for Result<T> pattern
public async Task<Result<User>> CreateUserAsync(User user)
{
    try
    {
        await table.Put
            .WithItem(user)
            .WithCondition($"attribute_not_exists({UserFields.UserId})")
            .ExecuteAsync();
        
        return Result.Ok(user);
    }
    catch (ConditionalCheckFailedException)
    {
        return Result.Fail<User>("User already exists");
    }
    catch (ProvisionedThroughputExceededException)
    {
        return Result.Fail<User>("Service temporarily unavailable. Please try again.");
    }
    catch (Exception ex)
    {
        return Result.Fail<User>($"Failed to create user: {ex.Message}");
    }
}

// Using the result
var result = await CreateUserAsync(newUser);

if (result.IsSuccess)
{
    Console.WriteLine($"User created: {result.Value.UserId}");
}
else
{
    Console.WriteLine($"Error: {result.Errors.First().Message}");
}

// Chaining operations with FluentResults
public async Task<Result<Order>> ProcessOrderAsync(Order order)
{
    return await CreateOrderAsync(order)
        .Bind(async o => await ReserveInventoryAsync(o))
        .Bind(async o => await ChargePaymentAsync(o))
        .Bind(async o => await SendConfirmationAsync(o));
}
```

**Installation:**
```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

**See Also:**
- [FluentResults Documentation](https://github.com/altmann/FluentResults)
- [FluentResults Integration README](../../Oproto.FluentDynamoDb.FluentResults/README.md)


## Best Practices

### 1. Catch Specific Exceptions

Always catch specific exceptions rather than generic `Exception`:

```csharp
// ✅ Good - specific exception handling
try
{
    await table.Put.WithItem(user).ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    // Handle duplicate
}
catch (ProvisionedThroughputExceededException)
{
    // Handle throttling
}
catch (ValidationException ex)
{
    // Handle validation errors
    Console.WriteLine($"Validation error: {ex.Message}");
}

// ❌ Bad - catching all exceptions
try
{
    await table.Put.WithItem(user).ExecuteAsync();
}
catch (Exception ex)
{
    // Too broad - can't handle different scenarios appropriately
}
```

### 2. Provide Meaningful Error Messages

Convert DynamoDB exceptions to domain-specific errors:

```csharp
public class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException(string userId)
        : base($"User with ID '{userId}' already exists")
    {
    }
}

public async Task CreateUserAsync(User user)
{
    try
    {
        await table.Put
            .WithItem(user)
            .WithCondition($"attribute_not_exists({UserFields.UserId})")
            .ExecuteAsync();
    }
    catch (ConditionalCheckFailedException)
    {
        throw new UserAlreadyExistsException(user.UserId);
    }
}
```

### 3. Log Errors Appropriately

Log errors with context for debugging:

```csharp
using Microsoft.Extensions.Logging;

public class UserRepository
{
    private readonly ILogger<UserRepository> _logger;
    private readonly DynamoDbTableBase _table;

    public async Task<User?> GetUserAsync(string userId)
    {
        try
        {
            var response = await _table.Get
                .WithKey(UserFields.UserId, UserKeys.Pk(userId))
                .ExecuteAsync<User>();
            
            return response.Item;
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogError(ex, "Table not found when getting user {UserId}", userId);
            throw;
        }
        catch (ProvisionedThroughputExceededException ex)
        {
            _logger.LogWarning(ex, "Throughput exceeded when getting user {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting user {UserId}", userId);
            throw;
        }
    }
}
```

### 4. Implement Idempotency

Make operations idempotent where possible:

```csharp
// Idempotent put with timestamp
public async Task SaveUserAsync(User user)
{
    user.UpdatedAt = DateTime.UtcNow;
    
    await table.Put
        .WithItem(user)
        .ExecuteAsync();
    
    // Safe to retry - will just update the timestamp
}

// Idempotent update with condition
public async Task IncrementCounterAsync(string userId)
{
    var requestId = Guid.NewGuid().ToString();
    
    try
    {
        await table.Update
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .Set($"SET {UserFields.Counter} = {UserFields.Counter} + {{0}}, " +
                 $"{UserFields.LastRequestId} = {{1}}", 
                 1, requestId)
            .WithCondition($"{UserFields.LastRequestId} <> {{0}}", requestId)
            .ExecuteAsync();
    }
    catch (ConditionalCheckFailedException)
    {
        // Already processed this request
    }
}
```

### 5. Monitor and Alert

Set up monitoring for error rates:

```csharp
public class MetricsTrackingRepository
{
    private readonly IMetrics _metrics;

    public async Task<User> GetUserAsync(string userId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await _table.Get
                .WithKey(UserFields.UserId, UserKeys.Pk(userId))
                .ExecuteAsync<User>();
            
            _metrics.RecordSuccess("GetUser", stopwatch.ElapsedMilliseconds);
            return response.Item;
        }
        catch (ProvisionedThroughputExceededException)
        {
            _metrics.RecordError("GetUser", "Throttled");
            throw;
        }
        catch (Exception)
        {
            _metrics.RecordError("GetUser", "Error");
            throw;
        }
    }
}
```

## See Also

- [Basic Operations](../core-features/BasicOperations.md)
- [Transactions](../core-features/Transactions.md)
- [Troubleshooting Guide](Troubleshooting.md)
- [AWS DynamoDB Error Handling](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Programming.Errors.html)

