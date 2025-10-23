using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Examples demonstrating the new format string functionality in condition and filter expressions.
/// Format strings are supported in Where() methods for Query, Update, Delete, and Put operations,
/// and in WithFilter() methods for Query and Scan operations.
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
        var oldResult = await _table.Query()
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .ExecuteAsync();

        // NEW APPROACH - Format strings
        var newResult = await _table.Query()
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

        var result = await _table.Query()
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

        var result = await _table.Query()
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
        await _table.Query()
            .Where("pk = {0} AND begins_with(sk, {1})", userId, "ORDER#")
            .ExecuteAsync();

        // Update with conditional format strings
        await _table.Update()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #status = :newStatus")  // Set still uses traditional parameters
            .Where("attribute_exists(pk) AND version = {0}", expectedVersion)
            .WithValue(":newStatus", "COMPLETED")
            .ExecuteAsync();

        // Delete with conditional format strings
        await _table.Delete()
            .WithKey("pk", userId, "sk", orderId)
            .Where("version = {0}", expectedVersion)
            .ExecuteAsync();

        // Put with conditional format strings
        await _table.Put()
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

        var result = await _table.Query()
            .Where("pk = {0} AND sk BETWEEN :startSk AND :endSk AND created > {1:o}",
                   userId, recentDate)
            .WithValue(":startSk", "ORDER#2024-01")
            .WithValue(":endSk", "ORDER#2024-12")
            .ExecuteAsync();
    }

    /// <summary>
    /// Filter expression format string examples.
    /// Filter expressions are applied after items are retrieved, reducing data transfer but not consumed capacity.
    /// </summary>
    public async Task FilterExpressionFormatStringExamples()
    {
        var userId = "USER#123";
        var status = OrderStatus.Processing;
        var minAmount = 50.0m;
        var maxAmount = 500.0m;
        var createdAfter = DateTime.UtcNow.AddDays(-30);
        var tag = "important";

        // Basic filter with format strings
        var result1 = await _table.Query()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #amount > {1:F2}", status, minAmount)
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .ExecuteAsync();

        // Complex filter with multiple conditions and types
        var result2 = await _table.Query()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #amount BETWEEN {1:F2} AND {2:F2} AND #created > {3:o}",
                       status, minAmount, maxAmount, createdAfter)
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .WithAttribute("#created", "created_date")
            .ExecuteAsync();

        // Filter with DynamoDB functions
        var result3 = await _table.Query()
            .Where("pk = {0}", userId)
            .WithFilter("contains(#tags, {0}) AND size(#items) > {1} AND attribute_exists(#optional)",
                       tag, 5)
            .WithAttribute("#tags", "tags")
            .WithAttribute("#items", "items")
            .WithAttribute("#optional", "optional_field")
            .ExecuteAsync();

        // Filter with IN operator
        var result4 = await _table.Query()
            .Where("pk = {0}", userId)
            .WithFilter("#status IN ({0}, {1}, {2})",
                       OrderStatus.Processing, OrderStatus.Completed, OrderStatus.Pending)
            .WithAttribute("#status", "status")
            .ExecuteAsync();

        // Mixed filter styles - format strings with traditional parameters
        var result5 = await _table.Query()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #customField = :customValue", status)
            .WithAttribute("#status", "status")
            .WithAttribute("#customField", "custom_field")
            .WithValue(":customValue", "custom data")
            .ExecuteAsync();
    }

    /// <summary>
    /// Scan operations with filter expressions (use sparingly - scans are expensive).
    /// Note: Requires casting to DynamoDbTableBase to access AsScannable() method.
    /// </summary>
    public async Task ScanWithFilterExpressionExamples()
    {
        // Cast to DynamoDbTableBase to access AsScannable method
        if (_table is not DynamoDbTableBase tableBase)
            throw new InvalidOperationException("Table must inherit from DynamoDbTableBase to access scan operations");

        var status = OrderStatus.Processing;
        var minAmount = 100.0m;
        var createdAfter = DateTime.UtcNow.AddDays(-7);

        // Basic scan with filter
        var result1 = await tableBase.AsScannable().Scan()
            .WithFilter("#status = {0} AND #amount > {1:F2}", status, minAmount)
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .Take(100)  // Limit items examined
            .ExecuteAsync();

        // Complex scan filter with multiple conditions
        var result2 = await tableBase.AsScannable().Scan()
            .WithFilter("(#status = {0} OR #status = {1}) AND #created > {2:o} AND attribute_exists(#tags)",
                       OrderStatus.Processing, OrderStatus.Completed, createdAfter)
            .WithAttribute("#status", "status")
            .WithAttribute("#created", "created_date")
            .WithAttribute("#tags", "tags")
            .Take(50)
            .ExecuteAsync();

        // Scan with projection and filter
        var result3 = await tableBase.AsScannable().Scan()
            .WithProjection("#id, #name, #status, #amount")
            .WithFilter("#status = {0} AND #amount BETWEEN {1:F2} AND {2:F2}",
                       status, 50.0m, 1000.0m)
            .WithAttribute("#id", "id")
            .WithAttribute("#name", "name")
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
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
        await _table.Update()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o}", newName, updatedTime)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .ExecuteAsync();

        // ADD operation with numeric formatting
        await _table.Update()
            .WithKey("pk", userId, "sk", orderId)
            .Set("ADD #count {0}, #amount {1:F2}", incrementValue, newAmount)
            .WithAttribute("#count", "count")
            .WithAttribute("#amount", "amount")
            .ExecuteAsync();

        // Complex update with multiple operations
        await _table.Update()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField",
                newName, updatedTime, incrementValue)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .WithAttribute("#count", "count")
            .WithAttribute("#oldField", "old_field")
            .ExecuteAsync();

        // Mixed format strings and traditional parameters
        await _table.Update()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #customField = :customValue", newName)
            .WithAttribute("#name", "name")
            .WithAttribute("#customField", "custom_field")
            .WithValue(":customValue", "custom data")
            .ExecuteAsync();
    }
}