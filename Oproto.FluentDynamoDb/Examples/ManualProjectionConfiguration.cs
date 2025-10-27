using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Examples demonstrating manual projection configuration for users not using source generation.
/// These patterns allow you to configure projections without generated code.
/// </summary>
public class ManualProjectionConfigurationExamples
{
    /// <summary>
    /// Example table class with manually configured index projections.
    /// This approach is useful when not using source generation or when you need
    /// runtime control over projection expressions.
    /// </summary>
    public class TransactionsTable : DynamoDbTableBase
    {
        public TransactionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
        {
        }

        // Example 1: Non-generic index with manual projection
        // The projection expression is automatically applied to all queries through this index
        public DynamoDbIndex StatusIndexWithProjection => new DynamoDbIndex(
            this,
            "StatusIndex",
            "id, amount, status, created_date, entity_type");

        // Example 2: Non-generic index without projection (defaults to all fields)
        public DynamoDbIndex StatusIndexFull => new DynamoDbIndex(
            this,
            "StatusIndex");

        // Example 3: Generic index with type-safe projection
        // Provides type safety for query results while applying projection
        public DynamoDbIndex<TransactionSummary> StatusIndexTyped => 
            new DynamoDbIndex<TransactionSummary>(
                this,
                "StatusIndex",
                "id, amount, status, created_date, entity_type");

        // Example 4: Generic index without projection (defaults to all fields)
        // Still provides type safety but fetches all attributes
        public DynamoDbIndex<Transaction> Gsi1Full => 
            new DynamoDbIndex<Transaction>(this, "Gsi1");
    }

    /// <summary>
    /// Example entity class.
    /// </summary>
    public class Transaction
    {
        public string Id { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Example projection class (manually defined, not generated).
    /// </summary>
    public class TransactionSummary
    {
        public string Id { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string EntityType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Example: Using non-generic index with manual projection.
    /// The projection is automatically applied to the query.
    /// </summary>
    public static async Task Example1_NonGenericWithProjection(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Projection is automatically applied
        var response = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();

        // Response contains only projected attributes: id, amount, status, created_date, entity_type
        // Description and Metadata are not fetched, reducing data transfer and cost
    }

    /// <summary>
    /// Example: Using generic index with type-safe projection.
    /// The projection is automatically applied and results are type-safe.
    /// </summary>
    public static async Task Example2_GenericWithProjection(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Projection is automatically applied, results are type-safe
        var response = await table.StatusIndexTyped.Query<TransactionSummary>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();

        // response.Items contains only projected properties populated
    }

    /// <summary>
    /// Example: Overriding automatic projection with manual projection.
    /// Manual .WithProjection() takes precedence over automatic projection.
    /// </summary>
    public static async Task Example3_ManualOverride(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Manual projection overrides the automatic one
        var response = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id, amount") // Overrides automatic projection
            .ToDynamoDbResponseAsync();

        // Response contains only: id, amount (manual projection takes precedence)
    }

    /// <summary>
    /// Example: Using generic index with type override.
    /// You can override the default type while still benefiting from projection.
    /// </summary>
    public static async Task Example4_TypeOverride(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Override the default type (TransactionSummary) with a different type
        var response = await table.StatusIndexTyped.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();

        // response.Items is List<Dictionary<string, AttributeValue>>
        // Note: If Transaction has a FromDynamoDb method, it will be used for hydration
    }

    /// <summary>
    /// Example: Precedence rules demonstration.
    /// Shows the order of precedence for projection configuration.
    /// </summary>
    public static async Task Example5_PrecedenceRules(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Precedence order (highest to lowest):
        // 1. Manual .WithProjection() call
        // 2. Index constructor projection expression
        // 3. No projection (all fields)

        // Case 1: Manual projection (highest precedence)
        var response1 = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id") // Takes precedence over index projection
            .ToDynamoDbResponseAsync();
        // Result: Only "id" is fetched

        // Case 2: Index projection (medium precedence)
        var response2 = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: "id, amount, status, created_date, entity_type" are fetched

        // Case 3: No projection (lowest precedence)
        var response3 = await table.StatusIndexFull.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: All fields are fetched
    }

    /// <summary>
    /// Example: Runtime projection configuration.
    /// Shows how to configure projections at runtime based on conditions.
    /// </summary>
    public static DynamoDbIndex GetConfiguredIndex(
        TransactionsTable table,
        bool useProjection)
    {
        // Configure projection at runtime
        if (useProjection)
        {
            return new DynamoDbIndex(
                table,
                "StatusIndex",
                "id, amount, status");
        }
        else
        {
            return new DynamoDbIndex(
                table,
                "StatusIndex");
        }
    }

    /// <summary>
    /// Example: Multiple projections for the same index.
    /// Shows how to define different projection levels for different use cases.
    /// </summary>
    public class FlexibleProjectionsTable : DynamoDbTableBase
    {
        public FlexibleProjectionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
        {
        }

        // Minimal projection for list views
        public DynamoDbIndex StatusIndexMinimal => new DynamoDbIndex(
            this,
            "StatusIndex",
            "id, status");

        // Standard projection for most queries
        public DynamoDbIndex StatusIndexStandard => new DynamoDbIndex(
            this,
            "StatusIndex",
            "id, amount, status, created_date");

        // Full projection (all fields)
        public DynamoDbIndex StatusIndexFull => new DynamoDbIndex(
            this,
            "StatusIndex");
    }

    /// <summary>
    /// Example: Using different projection levels based on use case.
    /// </summary>
    public static async Task Example6_MultipleProjectionLevels(IAmazonDynamoDB client)
    {
        var table = new FlexibleProjectionsTable(client);

        // Use minimal projection for list view (fast, low cost)
        var listItems = await table.StatusIndexMinimal.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .Take(100)
            .ToDynamoDbResponseAsync();

        // Use standard projection for detail view (balanced)
        var detailItems = await table.StatusIndexStandard.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .Take(10)
            .ToDynamoDbResponseAsync();

        // Use full projection when all data is needed (slower, higher cost)
        var fullItems = await table.StatusIndexFull.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .Take(1)
            .ToDynamoDbResponseAsync();
    }
}
