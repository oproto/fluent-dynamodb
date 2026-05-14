using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Indexes;

/// <summary>
/// API surface tests for generated typed index classes.
/// Validates that all documented index class methods compile correctly.
/// 
/// **Validates: Requirements 2.2, 2.4 from enhanced-index-table-generation spec**
/// 
/// Note: Requirements 2.3 (Query with Expression), 2.5 (Query with two Expressions), and 2.6 (projection type methods)
/// are validated through property-based tests in the source generator unit tests:
/// - IndexQueryMethodsPropertyTests.cs (Properties 7)
/// - IndexProjectionQueryMethodsPropertyTests.cs (Property 8)
/// 
/// The API surface tests here focus on the patterns that work with the base DynamoDbIndex class
/// which is used when no custom Name property is specified on the index attributes.
/// </summary>
public class TypedIndexClassApiSurface
{
    /// <summary>
    /// Test that Query&lt;T&gt;() method exists on generated typed index class.
    /// **Validates: Requirement 2.2**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedIndexClass_QueryGeneric_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // Query<T>() - returns QueryRequestBuilder<T>
        var results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .ToListAsync();
    }

    /// <summary>
    /// Test that Query&lt;T&gt;(string, params object[]) method exists on generated typed index class.
    /// **Validates: Requirement 2.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedIndexClass_QueryGenericWithString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // Query<T>(string, params object[]) - calls base.Query<T>()
        var results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0}", "category1")
            .ToListAsync();
        
        // With multiple parameters
        results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0} AND begins_with(gsi1sk, {1})", "category1", "2024")
            .ToListAsync();
    }

    /// <summary>
    /// Test that all generic Query methods can be chained with builder methods.
    /// **Validates: Requirements 2.2, 2.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedIndexClass_QueryGenericMethods_CanBeChained()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // Query<T>() can be chained with all builder methods
        var query1 = table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .WithFilter(x => x.Status == "active")
            .WithProjection("pk, sk, gsi1pk, status")
            .ScanIndexForward(false)
            .Take(25);
        var results1 = await query1.ToListAsync();
        
        // Query<T>(string, params) can be chained
        var query3 = table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0}", "category1")
            .WithFilter(x => x.Status == "active")
            .Take(10);
        var results3 = await query3.ToListAsync();
    }

    /// <summary>
    /// Test that LSI typed index class also has all generic Query methods.
    /// **Validates: Requirements 2.2, 2.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedIndexClass_LsiQueryMethods_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);
        var startDate = DateTime.Parse("2024-01-01");

        // Query<T>()
        var results1 = await table.Lsi1.Query<GsiLsiEntity>()
            .Where(x => x.PartitionKey == "pk1" && x.CreatedAt > startDate)
            .ToListAsync();
        
        // Query<T>(string, params)
        var results3 = await table.Lsi1.Query<GsiLsiEntity>("pk = {0} AND createdAt > {1:o}", "pk1", startDate)
            .ToListAsync();
    }

    /// <summary>
    /// Test that typed index class inherits from DynamoDbIndex and is partial.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public void TypedIndexClass_InheritsFromDynamoDbIndex()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // The generated index class should inherit from DynamoDbIndex
        // This is validated at compile time by the fact that we can access base class methods
        var gsi1Index = table.Gsi1;
        var lsi1Index = table.Lsi1;
        
        // Both should be accessible and usable
        Assert.NotNull(gsi1Index);
        Assert.NotNull(lsi1Index);
    }
}
