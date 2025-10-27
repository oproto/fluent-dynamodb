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
    private readonly DynamoDbTableBase _table;

    public FormatStringExamples(DynamoDbTableBase table)
    {
        _table = table;
    }
    
    // Placeholder entity for examples
    public class ExampleEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Amount { get; set; }
    }

    public enum OrderStatus { Pending, Processing, Completed, Cancelled }

    /// <summary>
    /// Basic format string usage in Query operations.
    /// </summary>
    public async Task BasicQueryExample()
    {
        // OLD APPROACH (still supported)
        var oldResult = await _table.Query<ExampleEntity>()
            .Where("pk = :pk AND begins_with(sk, :prefix)")
            .WithValue(":pk", "USER#123")
            .WithValue(":prefix", "ORDER#")
            .ToDynamoDbResponseAsync();

        // NEW APPROACH - Format strings
        var newResult = await _table.Query<ExampleEntity>()
            .Where("pk = {0} AND begins_with(sk, {1})", "USER#123", "ORDER#")
            .ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// DateTime formatting in conditions.
    /// </summary>
    public async Task DateTimeFormattingExample()
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = DateTime.UtcNow;

        var result = await _table.Query<ExampleEntity>()
            .Where("pk = {0} AND created BETWEEN {1:o} AND {2:o}",
                   "USER#123", startDate, endDate)
            .ToDynamoDbResponseAsync();
        // Results in: ":p1" = "2024-01-01T00:00:00.000Z", ":p2" = "2024-01-15T10:30:00.000Z"
    }

    /// <summary>
    /// Enum handling and reserved word mapping.
    /// </summary>
    public async Task EnumAndReservedWordExample()
    {
        var status = OrderStatus.Processing;

        var result = await _table.Query<ExampleEntity>()
            .Where("pk = {0} AND #status = {1}", "ORDER#123", status)
            .WithAttribute("#status", "status")  // Maps #status to actual "status" attribute
            .ToDynamoDbResponseAsync();
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
        await _table.Query<ExampleEntity>()
            .Where("pk = {0} AND begins_with(sk, {1})", userId, "ORDER#")
            .ToDynamoDbResponseAsync();

        // Update with conditional format strings
        await _table.Update<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #status = :newStatus")  // Set still uses traditional parameters
            .Where("attribute_exists(pk) AND version = {0}", expectedVersion)
            .WithValue(":newStatus", "COMPLETED")
            .ToDynamoDbResponseAsync();

        // Delete with conditional format strings
        await _table.Delete<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Where("version = {0}", expectedVersion)
            .ToDynamoDbResponseAsync();

        // Put with conditional format strings
        await _table.Put<PlaceholderEntity>()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = orderId }
            })
            .Where("attribute_not_exists(pk)")
            .ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// Mixed usage of format strings and traditional parameters.
    /// </summary>
    public async Task MixedParameterStylesExample()
    {
        var userId = "USER#123";
        var recentDate = DateTime.UtcNow.AddDays(-7);

        var result = await _table.Query<ExampleEntity>()
            .Where("pk = {0} AND sk BETWEEN :startSk AND :endSk AND created > {1:o}",
                   userId, recentDate)
            .WithValue(":startSk", "ORDER#2024-01")
            .WithValue(":endSk", "ORDER#2024-12")
            .ToDynamoDbResponseAsync();
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
        var result1 = await _table.Query<ExampleEntity>()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #amount > {1:F2}", status, minAmount)
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .ToDynamoDbResponseAsync();

        // Complex filter with multiple conditions and types
        var result2 = await _table.Query<ExampleEntity>()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #amount BETWEEN {1:F2} AND {2:F2} AND #created > {3:o}",
                       status, minAmount, maxAmount, createdAfter)
            .WithAttribute("#status", "status")
            .WithAttribute("#amount", "amount")
            .WithAttribute("#created", "created_date")
            .ToDynamoDbResponseAsync();

        // Filter with DynamoDB functions
        var result3 = await _table.Query<ExampleEntity>()
            .Where("pk = {0}", userId)
            .WithFilter("contains(#tags, {0}) AND size(#items) > {1} AND attribute_exists(#optional)",
                       tag, 5)
            .WithAttribute("#tags", "tags")
            .WithAttribute("#items", "items")
            .WithAttribute("#optional", "optional_field")
            .ToDynamoDbResponseAsync();

        // Filter with IN operator
        var result4 = await _table.Query<ExampleEntity>()
            .Where("pk = {0}", userId)
            .WithFilter("#status IN ({0}, {1}, {2})",
                       OrderStatus.Processing, OrderStatus.Completed, OrderStatus.Pending)
            .WithAttribute("#status", "status")
            .ToDynamoDbResponseAsync();

        // Mixed filter styles - format strings with traditional parameters
        var result5 = await _table.Query<ExampleEntity>()
            .Where("pk = {0}", userId)
            .WithFilter("#status = {0} AND #customField = :customValue", status)
            .WithAttribute("#status", "status")
            .WithAttribute("#customField", "custom_field")
            .WithValue(":customValue", "custom data")
            .ToDynamoDbResponseAsync();
    }

    /// <summary>
    /// Scan operations with filter expressions (use sparingly - scans are expensive).
    /// Note: Scan operations now require the [Scannable] attribute on table classes.
    /// The AsScannable() method has been removed in favor of attribute-based source generation.
    /// </summary>
    /// <remarks>
    /// To enable scan operations on a table:
    /// 1. Add [Scannable] attribute to your table class
    /// 2. Use the generated Scan() methods directly on the table instance
    /// 
    /// Example:
    /// [DynamoDbTable("Orders")]
    /// [Scannable]
    /// public partial class OrdersTable : DynamoDbTableBase { }
    /// 
    /// var result = await table.Scan()
    ///     .WithFilter("#status = {0} AND #amount > {1:F2}", status, minAmount)
    ///     .WithAttribute("#status", "status")
    ///     .WithAttribute("#amount", "amount")
    ///     .Take(100)
    ///     .ToDynamoDbResponseAsync();
    /// </remarks>
    public async Task ScanWithFilterExpressionExamples()
    {
        // This method is kept for documentation purposes but is not functional
        // without a table class marked with [Scannable] attribute.
        // See the remarks above for the new usage pattern.
        await Task.CompletedTask;
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
        await _table.Update<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o}", newName, updatedTime)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .ToDynamoDbResponseAsync();

        // ADD operation with numeric formatting
        await _table.Update<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Set("ADD #count {0}, #amount {1:F2}", incrementValue, newAmount)
            .WithAttribute("#count", "count")
            .WithAttribute("#amount", "amount")
            .ToDynamoDbResponseAsync();

        // Complex update with multiple operations
        await _table.Update<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #updated = {1:o} ADD #count {2} REMOVE #oldField",
                newName, updatedTime, incrementValue)
            .WithAttribute("#name", "name")
            .WithAttribute("#updated", "updated_time")
            .WithAttribute("#count", "count")
            .WithAttribute("#oldField", "old_field")
            .ToDynamoDbResponseAsync();

        // Mixed format strings and traditional parameters
        await _table.Update<ExampleEntity>()
            .WithKey("pk", userId, "sk", orderId)
            .Set("SET #name = {0}, #customField = :customValue", newName)
            .WithAttribute("#name", "name")
            .WithAttribute("#customField", "custom_field")
            .WithValue(":customValue", "custom data")
            .ToDynamoDbResponseAsync();
    }
}