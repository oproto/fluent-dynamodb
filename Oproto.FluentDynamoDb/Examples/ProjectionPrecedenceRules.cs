using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Examples;

/// <summary>
/// Comprehensive documentation and examples for projection precedence rules.
/// This guide explains how different projection configurations interact and which takes precedence.
/// </summary>
/// <remarks>
/// <para><b>Projection Precedence Order (Highest to Lowest):</b></para>
/// <list type="number">
/// <item>
/// <term>Manual .WithProjection() call</term>
/// <description>Explicit projection in the query builder always takes precedence</description>
/// </item>
/// <item>
/// <term>Index constructor projection</term>
/// <description>Projection configured when creating the DynamoDbIndex instance</description>
/// </item>
/// <item>
/// <term>Generated projection from source generator</term>
/// <description>Automatic projection based on projection model type (when using ToListAsync&lt;TProjection&gt;)</description>
/// </item>
/// <item>
/// <term>No projection</term>
/// <description>All attributes are fetched (default DynamoDB behavior)</description>
/// </item>
/// </list>
/// 
/// <para><b>Key Principles:</b></para>
/// <list type="bullet">
/// <item>More explicit configurations override less explicit ones</item>
/// <item>Manual configurations override automatic ones</item>
/// <item>Query-level configurations override index-level ones</item>
/// <item>Developer-defined configurations override generated ones</item>
/// </list>
/// </remarks>
public class ProjectionPrecedenceRulesExamples
{
    public class Transaction
    {
        public string Id { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class TransactionSummary
    {
        public string Id { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Simulating generated projection metadata
        public static string ProjectionExpression => "id, amount, status";
    }

    public class TransactionsTable : DynamoDbTableBase
    {
        public TransactionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
        {
        }

        // Index with manual projection configured
        public DynamoDbIndex StatusIndexWithProjection => new DynamoDbIndex(
            this,
            "StatusIndex",
            "id, amount, status, created_date");

        // Index without projection
        public DynamoDbIndex StatusIndexNoProjection => new DynamoDbIndex(
            this,
            "StatusIndex");

        // Generic index with projection
        public DynamoDbIndex<TransactionSummary> StatusIndexTyped => 
            new DynamoDbIndex<TransactionSummary>(
                this,
                "StatusIndex",
                "id, amount, status, created_date");
    }

    #region Rule 1: Manual .WithProjection() Has Highest Precedence

    /// <summary>
    /// Rule 1: Manual .WithProjection() call overrides all other projection configurations.
    /// This is the most explicit form of projection and always takes precedence.
    /// </summary>
    /// <remarks>
    /// <b>Precedence:</b> Manual .WithProjection() > Index projection > Generated projection > No projection
    /// </remarks>
    public static async Task Rule1_ManualProjectionOverridesAll(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Case 1: Manual projection overrides index projection
        var response1 = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id") // Overrides index projection "id, amount, status, created_date"
            .ToDynamoDbResponseAsync();
        // Result: Only "id" is fetched

        // Case 2: Manual projection overrides generated projection (when using ToListAsync<T>)
        // Note: This would work with actual generated projection models
        var response2 = await table.StatusIndexNoProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id, amount") // Overrides TransactionSummary's projection "id, amount, status"
            .ToDynamoDbResponseAsync();
        // Result: Only "id, amount" are fetched

        // Case 3: Manual projection can expand or reduce the projection
        var response3 = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id, amount, status, created_date, description, metadata") // Expands projection
            .ToDynamoDbResponseAsync();
        // Result: All specified fields are fetched, including those not in index projection
    }

    #endregion

    #region Rule 2: Index Constructor Projection Has Second Precedence

    /// <summary>
    /// Rule 2: Projection configured in the DynamoDbIndex constructor is automatically applied
    /// to all queries through that index, unless overridden by manual .WithProjection().
    /// </summary>
    /// <remarks>
    /// <b>Precedence:</b> Index projection > Generated projection > No projection
    /// <para>
    /// This is useful for:
    /// - Enforcing consistent projections across all queries to an index
    /// - Optimizing cost by limiting data transfer at the index level
    /// - Providing sensible defaults that can be overridden when needed
    /// </para>
    /// </remarks>
    public static async Task Rule2_IndexProjectionAutoApplied(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Case 1: Index projection is automatically applied
        var response1 = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: "id, amount, status, created_date" are fetched (from index projection)

        // Case 2: Index without projection fetches all fields
        var response2 = await table.StatusIndexNoProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: All fields are fetched (no projection configured)

        // Case 3: Generic index projection is also automatically applied
        var response3 = await table.StatusIndexTyped.Query<TransactionSummary>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: "id, amount, status, created_date" are fetched (from index projection)
    }

    #endregion

    #region Rule 3: Generated Projection Has Third Precedence

    /// <summary>
    /// Rule 3: When using ToListAsync&lt;TProjection&gt; where TProjection is a projection model
    /// generated by the source generator, the projection is automatically applied if no
    /// manual projection or index projection is configured.
    /// </summary>
    /// <remarks>
    /// <b>Precedence:</b> Generated projection > No projection
    /// <para>
    /// This provides automatic projection based on the result type, making it easy to
    /// fetch only the data you need without explicit projection configuration.
    /// </para>
    /// <para>
    /// Note: This requires using the source generator and projection model attributes.
    /// See ProjectionModelExamples.cs for details on defining projection models.
    /// </para>
    /// </remarks>
    public static async Task Rule3_GeneratedProjectionAutoApplied(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Case 1: Generated projection is applied when using projection model type
        // (This would work with actual generated projection models)
        // var summaries = await table.StatusIndexNoProjection.Query<Transaction>()
        //     .Where("status = :status")
        //     .WithValue(":status", "ACTIVE")
        //     .ToListAsync<TransactionSummary>();
        // Result: "id, amount, status" are fetched (from TransactionSummary.ProjectionExpression)

        // Case 2: Generated projection is NOT applied when using full entity type
        // var transactions = await table.StatusIndexNoProjection.Query<Transaction>()
        //     .Where("status = :status")
        //     .WithValue(":status", "ACTIVE")
        //     .ToListAsync<Transaction>();
        // Result: All fields are fetched (Transaction is not a projection model)
    }

    #endregion

    #region Rule 4: Manual Index Configuration Overrides Generated

    /// <summary>
    /// Rule 4: Manually defined index properties in your table class override
    /// any generated index properties with the same name.
    /// </summary>
    /// <remarks>
    /// <b>Precedence:</b> Manual index definition > Generated index definition
    /// <para>
    /// This allows you to:
    /// - Override generated index configurations
    /// - Provide custom projection expressions
    /// - Maintain backward compatibility with existing code
    /// - Have fine-grained control over specific indexes
    /// </para>
    /// </remarks>
    public class TableWithManualOverride : DynamoDbTableBase
    {
        public TableWithManualOverride(IAmazonDynamoDB client) : base(client, "Transactions")
        {
        }

        // Manual index definition overrides any generated one
        // If the source generator would create a StatusIndex property,
        // this manual definition takes precedence
        public DynamoDbIndex StatusIndex => new DynamoDbIndex(
            this,
            "StatusIndex",
            "id, amount"); // Custom projection

        // You can also use partial classes to extend generated code
        // while keeping manual overrides in a separate file
    }

    public static async Task Rule4_ManualIndexOverridesGenerated(IAmazonDynamoDB client)
    {
        var table = new TableWithManualOverride(client);

        // Uses the manually defined index with custom projection
        var response = await table.StatusIndex.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Result: "id, amount" are fetched (from manual index definition)
    }

    #endregion

    #region Complete Precedence Example

    /// <summary>
    /// Complete example showing all precedence levels in action.
    /// Demonstrates how different configurations interact.
    /// </summary>
    public static async Task CompleteExample_AllPrecedenceLevels(IAmazonDynamoDB client)
    {
        var table = new TransactionsTable(client);

        // Level 1: Manual .WithProjection() - Highest precedence
        var manual = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .WithProjection("id") // Overrides everything
            .ToDynamoDbResponseAsync();
        // Fetches: id

        // Level 2: Index constructor projection - Second precedence
        var indexProjection = await table.StatusIndexWithProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Fetches: id, amount, status, created_date

        // Level 3: Generated projection - Third precedence
        // (Would work with actual generated projection models)
        // var generated = await table.StatusIndexNoProjection.Query<Transaction>()
        //     .Where("status = :status")
        //     .WithValue(":status", "ACTIVE")
        //     .ToListAsync<TransactionSummary>();
        // Fetches: id, amount, status

        // Level 4: No projection - Lowest precedence (default)
        var noProjection = await table.StatusIndexNoProjection.Query<Transaction>()
            .Where("status = :status")
            .WithValue(":status", "ACTIVE")
            .ToDynamoDbResponseAsync();
        // Fetches: All fields
    }

    #endregion

    #region Best Practices

    /// <summary>
    /// Best practices for using projection precedence rules effectively.
    /// </summary>
    public static class BestPractices
    {
        /// <summary>
        /// Practice 1: Use index-level projections for consistent defaults.
        /// This ensures all queries through an index use the same projection
        /// unless explicitly overridden.
        /// </summary>
        public class ConsistentDefaultsTable : DynamoDbTableBase
        {
            public ConsistentDefaultsTable(IAmazonDynamoDB client) : base(client, "Transactions")
            {
            }

            // Set a sensible default projection at the index level
            public DynamoDbIndex StatusIndex => new DynamoDbIndex(
                this,
                "StatusIndex",
                "id, amount, status, created_date"); // Default for most queries

            // Provide alternative indexes for different use cases
            public DynamoDbIndex StatusIndexMinimal => new DynamoDbIndex(
                this,
                "StatusIndex",
                "id, status"); // For list views

            public DynamoDbIndex StatusIndexFull => new DynamoDbIndex(
                this,
                "StatusIndex"); // For detail views
        }

        /// <summary>
        /// Practice 2: Use manual .WithProjection() for exceptional cases.
        /// Reserve manual projections for queries that need different data
        /// than the index default.
        /// </summary>
        public static async Task UseManualForExceptions(IAmazonDynamoDB client)
        {
            var table = new ConsistentDefaultsTable(client);

            // Most queries use the index default
            var standard = await table.StatusIndex.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .ToDynamoDbResponseAsync();

            // Exceptional case: need additional fields
            var detailed = await table.StatusIndex.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .WithProjection("id, amount, status, created_date, description, metadata")
                .ToDynamoDbResponseAsync();

            // Exceptional case: need fewer fields
            var minimal = await table.StatusIndex.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .WithProjection("id")
                .ToDynamoDbResponseAsync();
        }

        /// <summary>
        /// Practice 3: Use generated projections for type-safe queries.
        /// When using source generation, let the projection model type
        /// determine the projection automatically.
        /// </summary>
        public static async Task UseGeneratedForTypeSafety(IAmazonDynamoDB client)
        {
            var table = new ConsistentDefaultsTable(client);

            // Type-safe query with automatic projection
            // (Would work with actual generated projection models)
            // var summaries = await table.StatusIndex.Query<Transaction>()
            //     .Where("status = :status")
            //     .WithValue(":status", "ACTIVE")
            //     .ToListAsync<TransactionSummary>();
            // Projection is automatically applied based on TransactionSummary definition
        }

        /// <summary>
        /// Practice 4: Document precedence in your table class.
        /// Make it clear which projections are defaults and which can be overridden.
        /// </summary>
        public class WellDocumentedTable : DynamoDbTableBase
        {
            public WellDocumentedTable(IAmazonDynamoDB client) : base(client, "Transactions")
            {
            }

            /// <summary>
            /// StatusIndex with standard projection for most queries.
            /// Fetches: id, amount, status, created_date
            /// Override with .WithProjection() if you need different fields.
            /// </summary>
            public DynamoDbIndex StatusIndex => new DynamoDbIndex(
                this,
                "StatusIndex",
                "id, amount, status, created_date");
        }
    }

    #endregion

    #region Common Scenarios

    /// <summary>
    /// Common scenarios and how precedence rules apply.
    /// </summary>
    public static class CommonScenarios
    {
        /// <summary>
        /// Scenario 1: Migrating from manual to generated projections.
        /// Shows how to gradually adopt source generation while maintaining compatibility.
        /// </summary>
        public static async Task Scenario1_MigrationPath(IAmazonDynamoDB client)
        {
            var table = new TransactionsTable(client);

            // Phase 1: Start with manual index projections
            var phase1 = await table.StatusIndexWithProjection.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .ToDynamoDbResponseAsync();

            // Phase 2: Add projection models and use ToListAsync<T>
            // Manual index projection still works as fallback
            // var phase2 = await table.StatusIndexWithProjection.Query<Transaction>()
            //     .Where("status = :status")
            //     .WithValue(":status", "ACTIVE")
            //     .ToListAsync<TransactionSummary>();

            // Phase 3: Remove manual index projection, rely on generated
            // var phase3 = await table.StatusIndexNoProjection.Query<Transaction>()
            //     .Where("status = :status")
            //     .WithValue(":status", "ACTIVE")
            //     .ToListAsync<TransactionSummary>();
        }

        /// <summary>
        /// Scenario 2: Different projections for different query types.
        /// Shows how to use precedence to optimize different query patterns.
        /// </summary>
        public static async Task Scenario2_OptimizedQueryPatterns(IAmazonDynamoDB client)
        {
            var table = new TransactionsTable(client);

            // List query: minimal projection for performance
            var listItems = await table.StatusIndexWithProjection.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .WithProjection("id, status") // Override for minimal data
                .Take(100)
                .ToDynamoDbResponseAsync();

            // Detail query: use index default projection
            var detailItem = await table.StatusIndexWithProjection.Query<Transaction>()
                .Where("status = :status AND id = :id")
                .WithValue(":status", "ACTIVE")
                .WithValue(":id", "TXN123")
                .ToDynamoDbResponseAsync();

            // Export query: fetch all fields
            var exportItems = await table.StatusIndexWithProjection.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE")
                .WithProjection("id, amount, status, created_date, description, metadata") // Override for all data
                .ToDynamoDbResponseAsync();
        }

        /// <summary>
        /// Scenario 3: Conditional projections based on runtime logic.
        /// Shows how to apply different projections based on conditions.
        /// </summary>
        public static async Task Scenario3_ConditionalProjections(
            IAmazonDynamoDB client,
            bool includeMetadata)
        {
            var table = new TransactionsTable(client);

            var query = table.StatusIndexWithProjection.Query<Transaction>()
                .Where("status = :status")
                .WithValue(":status", "ACTIVE");

            // Conditionally override projection
            if (includeMetadata)
            {
                query = query.WithProjection("id, amount, status, created_date, metadata");
            }
            // Otherwise, use index default projection

            var response = await query.ToDynamoDbResponseAsync();
        }
    }

    #endregion
}
