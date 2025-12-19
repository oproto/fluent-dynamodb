using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Indexes;

/// <summary>
/// API surface tests for Global Secondary Index (GSI) and Local Secondary Index (LSI) query operations.
/// Validates that all documented index query patterns compile correctly.
/// </summary>
public class IndexQueryApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task GsiQueryPatterns_LambdaExpression_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Lambda Expression Style (Preferred) ===
        // GSI query with partition key only
        var results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .ToListAsync();
        
        // GSI query with partition key and sort key
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1" && x.Gsi1Sk.StartsWith("2024"))
            .ToListAsync();
        
        // GSI query with range condition on sort key (using CompareTo for string comparison)
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1" && x.Gsi1Sk.CompareTo("2024-01-01") >= 0)
            .ToListAsync();
        
        // GSI query with Between on sort key
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1" && x.Gsi1Sk.Between("2024-01-01", "2024-12-31"))
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task GsiQueryPatterns_FormatString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Format String Style ===
        // GSI query with partition key only
        var results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0}", "category1")
            .ToListAsync();
        
        // GSI query with partition key and sort key
        results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0} AND begins_with(gsi1sk, {1})", "category1", "2024")
            .ToListAsync();
        
        // GSI query with range condition
        results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0} AND gsi1sk >= {1}", "category1", "2024-01-01")
            .ToListAsync();
        
        // GSI query with BETWEEN
        results = await table.Gsi1.Query<GsiLsiEntity>("gsi1pk = {0} AND gsi1sk BETWEEN {1} AND {2}", "category1", "2024-01-01", "2024-12-31")
            .ToListAsync();
    }

    
    [Fact(Skip = "API Surface Validation")]
    public async Task GsiQueryPatterns_ManualWithValue_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Manual WithValue Style ===
        // GSI query with partition key only
        var results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where("#gsi1pk = :gsi1pk")
            .WithAttribute("#gsi1pk", "gsi1pk")
            .WithValue(":gsi1pk", "category1")
            .ToListAsync();
        
        // GSI query with partition key and sort key
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where("#gsi1pk = :gsi1pk AND begins_with(#gsi1sk, :prefix)")
            .WithAttribute("#gsi1pk", "gsi1pk")
            .WithAttribute("#gsi1sk", "gsi1sk")
            .WithValue(":gsi1pk", "category1")
            .WithValue(":prefix", "2024")
            .ToListAsync();
        
        // GSI query with range condition
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where("#gsi1pk = :gsi1pk AND #gsi1sk >= :startDate")
            .WithAttribute("#gsi1pk", "gsi1pk")
            .WithAttribute("#gsi1sk", "gsi1sk")
            .WithValue(":gsi1pk", "category1")
            .WithValue(":startDate", "2024-01-01")
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task LsiQueryPatterns_LambdaExpression_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);
        var startDate = DateTime.Parse("2024-01-01");

        // === Lambda Expression Style (Preferred) ===
        // LSI query - uses main table's partition key with alternate sort key
        var results = await table.Lsi1.Query<GsiLsiEntity>()
            .Where(x => x.PartitionKey == "pk1" && x.CreatedAt > startDate)
            .ToListAsync();
        
        // LSI query with range condition
        results = await table.Lsi1.Query<GsiLsiEntity>()
            .Where(x => x.PartitionKey == "pk1" && x.CreatedAt >= startDate)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task LsiQueryPatterns_FormatString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Format String Style ===
        // LSI query with partition key and sort key
        var results = await table.Lsi1.Query<GsiLsiEntity>("pk = {0} AND createdAt > {1:o}", "pk1", DateTime.Parse("2024-01-01"))
            .ToListAsync();
        
        // LSI query with range condition
        results = await table.Lsi1.Query<GsiLsiEntity>("pk = {0} AND createdAt >= {1:o}", "pk1", DateTime.Parse("2024-01-01"))
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task IndexQueryOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Projection ===
        var results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .WithProjection("pk, sk, name, status")
            .ToListAsync();
        
        // === Filter Expression ===
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .WithFilter(x => x.Status == "active")
            .ToListAsync();
        
        // === Pagination ===
        var query = table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .Take(25);
        results = await query.ToListAsync();
        
        // Access LastEvaluatedKey from response after execution
        var lastKey = query.Response?.LastEvaluatedKey ?? new Dictionary<string, AttributeValue>();
        
        // StartAt for next page
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .StartAt(lastKey)
            .ToListAsync();
        
        // === ScanIndexForward (sort order) ===
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .ScanIndexForward(false)
            .ToListAsync();
        
        // === Combined options ===
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .WithFilter(x => x.Status == "active")
            .WithProjection("pk, sk, name, status")
            .ScanIndexForward(false)
            .Take(25)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task IndexWithProjection_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GsiLsiTable table = new GsiLsiTable(client, "gsiLsi", options: null);

        // === Index with projection expression ===
        // When an index is defined with a projection expression, it's automatically applied
        var results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .ToListAsync();
        
        // Override projection on index query
        results = await table.Gsi1.Query<GsiLsiEntity>()
            .Where(x => x.Gsi1Pk == "category1")
            .WithProjection("pk, sk, gsi1pk, gsi1sk")
            .ToListAsync();
    }
}
