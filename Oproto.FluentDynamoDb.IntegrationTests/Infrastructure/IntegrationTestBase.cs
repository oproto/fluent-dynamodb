using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;
using Xunit;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that provides common functionality for table management,
/// entity operations, and cleanup.
/// </summary>
[Collection("DynamoDB Local")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly List<string> _tablesToCleanup = new();
    
    /// <summary>
    /// Gets the DynamoDB client connected to DynamoDB Local.
    /// </summary>
    protected IAmazonDynamoDB DynamoDb { get; }
    
    /// <summary>
    /// Gets the unique table name for this test class instance.
    /// Each test class gets a unique table name to avoid conflicts when running tests in parallel.
    /// </summary>
    protected string TableName { get; }
    
    /// <summary>
    /// Initializes a new instance of the IntegrationTestBase class.
    /// </summary>
    /// <param name="fixture">The DynamoDB Local fixture that manages the DynamoDB Local instance.</param>
    protected IntegrationTestBase(DynamoDbLocalFixture fixture)
    {
        DynamoDb = fixture.Client;
        // Generate unique table name per test class instance to support parallel execution
        TableName = $"test_{GetType().Name}_{Guid.NewGuid():N}";
    }
    
    /// <summary>
    /// Called before each test to perform setup.
    /// Override in derived classes to create tables or perform other setup.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Called after each test to perform cleanup.
    /// Automatically deletes all tables created during the test.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        // Clean up all tables created during tests
        var cleanupErrors = new List<Exception>();
        
        foreach (var tableName in _tablesToCleanup)
        {
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
                Console.WriteLine($"[Cleanup] Deleted table: {tableName}");
            }
            catch (ResourceNotFoundException)
            {
                // Table already deleted, ignore
                Console.WriteLine($"[Cleanup] Table already deleted: {tableName}");
            }
            catch (Exception ex)
            {
                cleanupErrors.Add(ex);
                Console.WriteLine($"[Cleanup] Warning: Failed to delete table {tableName}: {ex.Message}");
            }
        }
        
        // Don't fail the test due to cleanup issues, but log them
        if (cleanupErrors.Any())
        {
            Console.WriteLine($"[Cleanup] Completed with {cleanupErrors.Count} error(s)");
        }
    }
    
    /// <summary>
    /// Creates a DynamoDB table for the specified entity type using its metadata.
    /// The table is automatically tracked for cleanup after the test completes.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task CreateTableAsync<TEntity>() where TEntity : IDynamoDbEntity
    {
        var metadata = TEntity.GetEntityMetadata();
        
        // Find partition key property
        var partitionKeyProp = metadata.Properties.FirstOrDefault(p => p.IsPartitionKey);
        if (partitionKeyProp == null)
        {
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity).Name} does not have a partition key property");
        }
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = partitionKeyProp.AttributeName,
                    KeyType = KeyType.HASH
                }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition
                {
                    AttributeName = partitionKeyProp.AttributeName,
                    AttributeType = GetScalarAttributeType(partitionKeyProp.PropertyType)
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        // Add sort key if present
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        if (sortKeyProp != null)
        {
            request.KeySchema.Add(new KeySchemaElement
            {
                AttributeName = sortKeyProp.AttributeName,
                KeyType = KeyType.RANGE
            });
            
            request.AttributeDefinitions.Add(new AttributeDefinition
            {
                AttributeName = sortKeyProp.AttributeName,
                AttributeType = GetScalarAttributeType(sortKeyProp.PropertyType)
            });
        }
        
        await DynamoDb.CreateTableAsync(request);
        _tablesToCleanup.Add(TableName);
        
        Console.WriteLine($"[Setup] Created table: {TableName}");
        
        // Wait for table to be active
        await WaitForTableActiveAsync(TableName);
    }
    
    /// <summary>
    /// Waits for a DynamoDB table to become active.
    /// </summary>
    /// <param name="tableName">The name of the table to wait for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected async Task WaitForTableActiveAsync(string tableName)
    {
        var maxAttempts = 30;
        var delayMs = 500;
        
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await DynamoDb.DescribeTableAsync(tableName);
                
                if (response.Table.TableStatus == TableStatus.ACTIVE)
                {
                    Console.WriteLine($"[Setup] Table {tableName} is active after {attempt} attempt(s)");
                    return;
                }
                
                await Task.Delay(delayMs);
            }
            catch (ResourceNotFoundException)
            {
                // Table not found yet, wait and retry
                await Task.Delay(delayMs);
            }
        }
        
        throw new TimeoutException(
            $"Table {tableName} did not become active after {maxAttempts} attempts");
    }
    
    /// <summary>
    /// Saves an entity to DynamoDB and loads it back to verify round-trip correctness.
    /// This is a common pattern in integration tests to verify serialization/deserialization.
    /// </summary>
    /// <typeparam name="TEntity">The entity type that implements IDynamoDbEntity.</typeparam>
    /// <param name="entity">The entity to save and load.</param>
    /// <returns>The entity loaded back from DynamoDB.</returns>
    protected async Task<TEntity> SaveAndLoadAsync<TEntity>(TEntity entity) 
        where TEntity : IDynamoDbEntity, new()
    {
        // Convert entity to DynamoDB item
        var item = TEntity.ToDynamoDb(entity);
        
        // Save entity to DynamoDB
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Get the partition key attribute name and value
        var metadata = TEntity.GetEntityMetadata();
        var partitionKeyProp = metadata.Properties.First(p => p.IsPartitionKey);
        var partitionKeyValue = item[partitionKeyProp.AttributeName];
        
        // Build the key for GetItem
        var key = new Dictionary<string, AttributeValue>
        {
            [partitionKeyProp.AttributeName] = partitionKeyValue
        };
        
        // Add sort key if present
        var sortKeyProp = metadata.Properties.FirstOrDefault(p => p.IsSortKey);
        if (sortKeyProp != null)
        {
            key[sortKeyProp.AttributeName] = item[sortKeyProp.AttributeName];
        }
        
        // Load entity back from DynamoDB
        var getRequest = new GetItemRequest
        {
            TableName = TableName,
            Key = key
        };
        
        var response = await DynamoDb.GetItemAsync(getRequest);
        
        if (!response.IsItemSet)
        {
            throw new InvalidOperationException(
                $"Item not found in table {TableName} after saving");
        }
        
        // Convert DynamoDB item back to entity
        return TEntity.FromDynamoDb<TEntity>(response.Item);
    }
    
    /// <summary>
    /// Gets the DynamoDB scalar attribute type for a C# type.
    /// </summary>
    private static ScalarAttributeType GetScalarAttributeType(Type type)
    {
        // Unwrap nullable types
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        // String types map to S (String)
        if (underlyingType == typeof(string))
        {
            return ScalarAttributeType.S;
        }
        
        // Numeric types map to N (Number)
        if (underlyingType == typeof(int) || 
            underlyingType == typeof(long) || 
            underlyingType == typeof(decimal) || 
            underlyingType == typeof(double) || 
            underlyingType == typeof(float) ||
            underlyingType == typeof(short) ||
            underlyingType == typeof(byte))
        {
            return ScalarAttributeType.N;
        }
        
        // Binary types map to B (Binary)
        if (underlyingType == typeof(byte[]))
        {
            return ScalarAttributeType.B;
        }
        
        // Default to string for unknown types
        return ScalarAttributeType.S;
    }
}
