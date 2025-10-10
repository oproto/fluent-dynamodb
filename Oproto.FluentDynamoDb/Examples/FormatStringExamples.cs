using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Examples demonstrating the new format string functionality in condition expressions.
/// Format strings are supported in Where() methods for Query, Update, Delete, and Put operations.
/// </summary>
public class FormatStringExamples
{
    private readonly IDynamoDbTable _table;

    public FormatStringExamples(IDynamoDbTable table)
    {
        _table = table;
    }

    public enum OrderStatus { Pending, Processing, Completed, Cancelled }

    /// <summary>
    /// Basic format string usage in Query operations.
    /// </summary>
    public async Task BasicQueryExample()
    {
        // OLD APPROACH (still supported)
        var oldResult = await _table.Query
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .ExecuteAsync();

        // NEW APPROACH - Format strings
        var newResult = await _table.Query
            .Where("pk = {0} AND begins_with(sk, {1})", "USER#123", "ORDER#")
            .ExecuteAsync();
    }

    /// <summary>
    /// DateTime formatting in conditions.
    /// </summary>
    public async Task DateTimeFormattingExample()
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = DateTime.UtcNow;

        var result = await _table.Query
            .Where("pk = {0} AND created BETWEEN {1:o} AND {2:o}", 
                   "USER#123", startDate, endDate)
            .ExecuteAsync();
        // Results in: ":p1" = "2024-01-01T00:00:00.000Z", ":p2" = "2024-01-15T10:30:00.000Z"
    }

    /// <summary>
    /// Enum handling and reserved word mapping.
    /// </summary>
    public async Task EnumAndReservedWordExample()
    {
        var status = OrderStatus.Processing;

        var result = await _table.Query
            .Where("pk = {0} AND #status = {1}", "ORDER#123", status)
            .WithAttribute("#status", "status")  // Maps #status to actual "status" attribute
            .ExecuteAsync();
        // Results in: ":p1" = "Processing"
    }

    /// <summary>
    /// All operations that support format strings in conditions.
    /// </summary>
    public async Task AllSupportedOperationsExample()
    {
        var userId = "USER#123";
        var orderId = "ORDER#456";
        var expectedVersion = 5;

        // Query with format strings
        await _table.Query
            .Where("pk = {0} AND begins_with(sk, {1})", userId, "ORDER#")
            .ExecuteAsync();

        // Update with conditional format strings
        await _table.Update
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #status = :newStatus")  // Set still uses traditional parameters
            .Where("attribute_exists(pk) AND version = {0}", expectedVersion)
            .WithValue(":newStatus", "COMPLETED")
            .ExecuteAsync();

        // Delete with conditional format strings
        await _table.Delete
            .WithKey("pk", userId, "sk", orderId)
            .Where("version = {0}", expectedVersion)
            .ExecuteAsync();

        // Put with conditional format strings
        await _table.Put
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = orderId }
            })
            .Where("attribute_not_exists(pk)")
            .ExecuteAsync();
    }

    /// <summary>
    /// Mixed usage of format strings and traditional parameters.
    /// </summary>
    public async Task MixedParameterStylesExample()
    {
        var userId = "USER#123";
        var recentDate = DateTime.UtcNow.AddDays(-7);

        var result = await _table.Query
            .Where("pk = {0} AND sk BETWEEN :startSk AND :endSk AND created > {1:o}", 
                   userId, recentDate)
            .WithValue(":startSk", "ORDER#2024-01")
            .WithValue(":endSk", "ORDER#2024-12")
            .ExecuteAsync();
    }

    /// <summary>
    /// Update expression format string examples.
    /// </summary>
    public async Task UpdateExpressionFormatStringExamples()
    {
        var userId = "USER#123";
        var orderId = "ORDER#456";
        var newName = "John Doe";
        var updatedTime = DateTime.UtcNow;
        var incrementValue = 1;
        var newAmount = 99.99m;

        // Simple SET operation with format strings
        await _table.Update
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o}", newName, updatedTime)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .ExecuteAsync();

        // ADD operation with numeric formatting
        await _table.Update
            .WithKey("pk", userId, "sk", orderId)
            .Set("ADD #count {0}, #amount {1:F2}", incrementValue, newAmount)
            .WithAttribute("#count", "count")
            .WithAttribute("#amount", "amount")
            .ExecuteAsync();

        // Complex update with multiple operations
        await _table.Update
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField", 
                newName, updatedTime, incrementValue)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .WithAttribute("#count", "count")
            .WithAttribute("#oldField", "old_field")
            .ExecuteAsync();

        // Mixed format strings and traditional parameters
        await _table.Update
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #customField = :customValue", newName)
            .WithAttribute("#name", "name")
            .WithAttribute("#customField", "custom_field")
            .WithValue(":customValue", "custom data")
            .ExecuteAsync();
    }
}