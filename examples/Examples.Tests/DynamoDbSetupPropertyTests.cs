using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Examples.Shared;

namespace Examples.Tests;

/// <summary>
/// Property-based tests for DynamoDbSetup.
/// These tests require DynamoDB Local to be running on port 8000.
/// </summary>
public class DynamoDbSetupPropertyTests
{
    /// <summary>
    /// **Feature: example-applications, Property 1: Idempotent Table Creation**
    /// **Validates: Requirements 1.2**
    /// 
    /// For any table name and schema, calling EnsureTableExistsAsync twice in succession
    /// should result in the table existing and no errors being thrown.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IdempotentTableCreation_CallingTwice_DoesNotThrow()
    {
        return Prop.ForAll(
            GenerateValidTableName(),
            tableName =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    
                    // Clean up any existing table from previous test runs
                    DeleteTableIfExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    // First call - should create the table
                    var firstResult = DynamoDbSetup.EnsureTableExistsAsync(
                        client, tableName, "pk").GetAwaiter().GetResult();
                    
                    // Second call - should not throw and should return false (already exists)
                    var secondResult = DynamoDbSetup.EnsureTableExistsAsync(
                        client, tableName, "pk").GetAwaiter().GetResult();
                    
                    // Verify table exists
                    var tableExists = TableExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    // Clean up
                    DeleteTableIfExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    // First call should return true (created), second should return false (already existed)
                    var firstCallCreated = firstResult == true;
                    var secondCallDidNotCreate = secondResult == false;
                    
                    return (firstCallCreated && secondCallDidNotCreate && tableExists).ToProperty()
                        .Label($"Idempotent table creation. FirstCallCreated: {firstCallCreated}, " +
                               $"SecondCallDidNotCreate: {secondCallDidNotCreate}, TableExists: {tableExists}");
                }
                catch (AmazonDynamoDBException ex) when (ex.Message.Contains("Unable to connect") || 
                                                          ex.Message.Contains("Connection refused"))
                {
                    // DynamoDB Local not running - skip test
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 1: Idempotent Table Creation (with sort key)**
    /// **Validates: Requirements 1.2**
    /// 
    /// For any table name and schema with sort key, calling EnsureTableExistsAsync twice
    /// should result in the table existing and no errors being thrown.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property IdempotentTableCreation_WithSortKey_CallingTwice_DoesNotThrow()
    {
        return Prop.ForAll(
            GenerateValidTableName(),
            tableName =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    
                    // Clean up any existing table from previous test runs
                    DeleteTableIfExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    // First call - should create the table with sort key
                    var firstResult = DynamoDbSetup.EnsureTableExistsAsync(
                        client, tableName, "pk", "sk").GetAwaiter().GetResult();
                    
                    // Second call - should not throw and should return false
                    var secondResult = DynamoDbSetup.EnsureTableExistsAsync(
                        client, tableName, "pk", "sk").GetAwaiter().GetResult();
                    
                    // Verify table exists
                    var tableExists = TableExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    // Clean up
                    DeleteTableIfExistsAsync(client, tableName).GetAwaiter().GetResult();
                    
                    var firstCallCreated = firstResult == true;
                    var secondCallDidNotCreate = secondResult == false;
                    
                    return (firstCallCreated && secondCallDidNotCreate && tableExists).ToProperty()
                        .Label($"Idempotent table creation with sort key. FirstCallCreated: {firstCallCreated}, " +
                               $"SecondCallDidNotCreate: {secondCallDidNotCreate}, TableExists: {tableExists}");
                }
                catch (AmazonDynamoDBException ex) when (ex.Message.Contains("Unable to connect") || 
                                                          ex.Message.Contains("Connection refused"))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// Generates valid DynamoDB table names.
    /// Table names must be 3-255 characters, alphanumeric with hyphens, underscores, and dots.
    /// </summary>
    private static Arbitrary<string> GenerateValidTableName()
    {
        return Arb.From(
            from prefix in Gen.Elements("test", "example", "prop")
            from suffix in Gen.Choose(1000, 9999)
            from timestamp in Gen.Constant(DateTime.UtcNow.Ticks % 1000000)
            select $"{prefix}-table-{suffix}-{timestamp}"
        );
    }

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

    private static async Task DeleteTableIfExistsAsync(IAmazonDynamoDB client, string tableName)
    {
        try
        {
            await client.DeleteTableAsync(tableName);
            // Wait for deletion to complete
            await Task.Delay(100);
        }
        catch (ResourceNotFoundException)
        {
            // Table doesn't exist, nothing to delete
        }
    }
}
