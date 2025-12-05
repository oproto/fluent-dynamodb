using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Examples.Shared;

/// <summary>
/// Provides shared infrastructure for connecting to DynamoDB Local
/// and creating tables for example applications.
/// </summary>
public static class DynamoDbSetup
{
    /// <summary>
    /// Default DynamoDB Local endpoint.
    /// </summary>
    public const string DefaultLocalEndpoint = "http://localhost:8000";

    /// <summary>
    /// Creates a DynamoDB client configured for DynamoDB Local.
    /// </summary>
    /// <param name="endpoint">The endpoint URL. Defaults to localhost:8000.</param>
    /// <returns>An IAmazonDynamoDB client configured for local development.</returns>
    public static IAmazonDynamoDB CreateLocalClient(string endpoint = DefaultLocalEndpoint)
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = endpoint
        };

        // DynamoDB Local doesn't require real credentials
        return new AmazonDynamoDBClient("local", "local", config);
    }

    /// <summary>
    /// Creates a table if it doesn't exist. Safe to call multiple times (idempotent).
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="tableName">Name of the table to create.</param>
    /// <param name="partitionKeyName">Name of the partition key attribute.</param>
    /// <param name="sortKeyName">Optional name of the sort key attribute.</param>
    /// <param name="gsis">Optional list of Global Secondary Indexes.</param>
    /// <returns>True if the table was created, false if it already existed.</returns>
    public static async Task<bool> EnsureTableExistsAsync(
        IAmazonDynamoDB client,
        string tableName,
        string partitionKeyName,
        string? sortKeyName = null,
        List<GlobalSecondaryIndex>? gsis = null)
    {
        // Check if table already exists
        if (await TableExistsAsync(client, tableName))
        {
            return false;
        }

        // Build key schema
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement(partitionKeyName, KeyType.HASH)
        };

        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition(partitionKeyName, ScalarAttributeType.S)
        };

        if (!string.IsNullOrEmpty(sortKeyName))
        {
            keySchema.Add(new KeySchemaElement(sortKeyName, KeyType.RANGE));
            attributeDefinitions.Add(new AttributeDefinition(sortKeyName, ScalarAttributeType.S));
        }

        // Add GSI attribute definitions
        if (gsis != null)
        {
            foreach (var gsi in gsis)
            {
                foreach (var key in gsi.KeySchema)
                {
                    if (!attributeDefinitions.Any(a => a.AttributeName == key.AttributeName))
                    {
                        attributeDefinitions.Add(new AttributeDefinition(key.AttributeName, ScalarAttributeType.S));
                    }
                }
            }
        }

        var request = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST
        };

        if (gsis != null && gsis.Count > 0)
        {
            request.GlobalSecondaryIndexes = gsis;
        }

        try
        {
            await client.CreateTableAsync(request);
            
            // Wait for table to become active
            await WaitForTableActiveAsync(client, tableName);
            
            return true;
        }
        catch (ResourceInUseException)
        {
            // Table already exists (race condition) - this is fine
            return false;
        }
    }

    /// <summary>
    /// Checks if a table exists.
    /// </summary>
    private static async Task<bool> TableExistsAsync(IAmazonDynamoDB client, string tableName)
    {
        try
        {
            var response = await client.DescribeTableAsync(tableName);
            return response.Table != null;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits for a table to become active.
    /// </summary>
    private static async Task WaitForTableActiveAsync(IAmazonDynamoDB client, string tableName, int maxWaitSeconds = 30)
    {
        var startTime = DateTime.UtcNow;
        
        while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitSeconds)
        {
            try
            {
                var response = await client.DescribeTableAsync(tableName);
                if (response.Table.TableStatus == TableStatus.ACTIVE)
                {
                    return;
                }
            }
            catch (ResourceNotFoundException)
            {
                // Table not yet created, keep waiting
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Table {tableName} did not become active within {maxWaitSeconds} seconds.");
    }
}
