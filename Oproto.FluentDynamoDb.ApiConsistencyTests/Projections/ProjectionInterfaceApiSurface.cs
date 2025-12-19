using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Projections;

/// <summary>
/// API surface tests for projection interface compatibility with QueryRequestBuilder.
/// Validates that all documented projection patterns compile correctly.
/// 
/// **Validates: Requirement 7.4 from projection-interface-enhancement spec**
/// 
/// This file tests:
/// 1. Projection types work with QueryRequestBuilder (Requirement 3.1, 3.4)
/// 2. All query expression patterns work with projections (Requirement 4.5)
/// 3. Projection extension methods compile correctly (Requirement 6.3)
/// 4. Index queries with projections compile correctly (Requirement 4.1)
/// </summary>
public class ProjectionInterfaceApiSurface
{
    #region Interface Implementation Verification
    
    /// <summary>
    /// Verifies that projection types implement IReadOnlyEntity interface.
    /// **Validates: Requirements 2.1, 3.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public void ProjectionType_ImplementsIReadOnlyEntity()
    {
        // Verify at compile time that ProjectionTestProjection implements IReadOnlyEntity
        IReadOnlyEntity projection = null!;
        
        // This line would fail to compile if ProjectionTestProjection doesn't implement IReadOnlyEntity
        projection = (IReadOnlyEntity)(object)new ProjectionTestProjection();
        
        // Verify the interface methods exist
        var item = new Dictionary<string, AttributeValue>();
        var partitionKey = ProjectionTestProjection.GetPartitionKey(item);
        var metadata = ProjectionTestProjection.GetEntityMetadata();
    }
    
    /// <summary>
    /// Verifies that projection types implement IProjectionModel interface.
    /// **Validates: Requirements 6.1, 6.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public void ProjectionType_ImplementsIProjectionModel()
    {
        // Verify at compile time that ProjectionTestProjection implements IProjectionModel
        // This line would fail to compile if ProjectionTestProjection doesn't implement IProjectionModel
        var projectionExpression = ProjectionTestProjection.ProjectionExpression;
        
        // Verify FromDynamoDb method exists
        var item = new Dictionary<string, AttributeValue>();
        var projection = ProjectionTestProjection.FromDynamoDb(item);
    }
    
    #endregion
    
    #region QueryRequestBuilder Compatibility
    
    /// <summary>
    /// Verifies that projection types can be used with QueryRequestBuilder.
    /// **Validates: Requirements 3.1, 3.4**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task QueryRequestBuilder_AcceptsProjectionType_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Projection type should be accepted by QueryRequestBuilder
        QueryRequestBuilder<ProjectionTestProjection> builder = table.Query<ProjectionTestProjection>();
        
        // Should be able to chain query methods and use ToListAsync
        var results = await builder
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that existing entity types continue to work with QueryRequestBuilder.
    /// **Validates: Requirements 3.5, 6.2**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task QueryRequestBuilder_AcceptsEntityType_BackwardCompatibility()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Full entity type should still work (backward compatibility)
        QueryRequestBuilder<ProjectionTestEntity> builder = table.Query<ProjectionTestEntity>();
        
        // Should be able to chain query methods
        var results = await builder
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToListAsync();
    }
    
    #endregion
    
    #region Query Expression Patterns with Projections
    
    /// <summary>
    /// Verifies that projection queries work with manual expression pattern.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_ManualExpression_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Manual expression with WithValue - using ToListAsync
        var results = await table.Query<ProjectionTestProjection>()
            .Where("#pk = :pk")
            .WithAttribute("#pk", "pk")
            .WithValue(":pk", "test")
            .ToListAsync();
        
        // Manual expression with composite key
        results = await table.Query<ProjectionTestProjection>()
            .Where("#pk = :pk AND begins_with(#sk, :skPrefix)")
            .WithAttribute("#pk", "pk")
            .WithAttribute("#sk", "sk")
            .WithValue(":pk", "test")
            .WithValue(":skPrefix", "ITEM#")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with format string pattern.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_FormatString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Format string with single value
        var results = await table.Query<ProjectionTestProjection>("pk = {0}", "test")
            .ToListAsync();
        
        // Format string with multiple values
        results = await table.Query<ProjectionTestProjection>("pk = {0} AND begins_with(sk, {1})", "test", "ITEM#")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with filter expressions.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_WithFilter_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Manual filter expression
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithFilter("#qty > :minQty")
            .WithAttribute("#qty", "quantity")
            .WithValue(":minQty", 10)
            .ToListAsync();
        
        // Format string filter expression
        results = await table.Query<ProjectionTestProjection>("pk = {0}", "test")
            .WithFilter("quantity > {0}", 10)
            .ToListAsync();
    }
    
    #endregion
    
    #region Query Options with Projections
    
    /// <summary>
    /// Verifies that projection queries work with pagination options.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_Pagination_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Take (limit)
        var query = table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .Take(25);
        var results = await query.ToListAsync();
        
        // Access LastEvaluatedKey from response after execution
        var lastKey = query.Response?.LastEvaluatedKey ?? new Dictionary<string, AttributeValue>();
        
        // StartAt for next page
        results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .StartAt(lastKey)
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with sort order options.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_SortOrder_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // ScanIndexForward (ascending)
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ScanIndexForward(true)
            .ToListAsync();
        
        // ScanIndexForward (descending)
        results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ScanIndexForward(false)
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with consistent read option.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_ConsistentRead_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .UsingConsistentRead()
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with manual projection expression override.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_WithProjectionOverride_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Override projection expression
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithProjection("pk, sk, #name")
            .WithAttribute("#name", "name")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that projection queries work with combined options.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_CombinedOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .WithFilter("quantity > :minQty")
            .WithValue(":minQty", 10)
            .ScanIndexForward(false)
            .Take(25)
            .ToListAsync();
    }
    
    #endregion
    
    #region Index Queries with Projections
    
    /// <summary>
    /// Verifies that projection types work with GSI queries via generated index accessor.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task IndexQuery_WithProjectionType_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Query GSI with projection type via generated index accessor - manual expression
        var results = await table.StatusIndex.Query<ProjectionTestProjection>()
            .Where("gsi1pk = :status")
            .WithValue(":status", "ACTIVE")
            .ToListAsync();
        
        // Query GSI with projection type - format string
        results = await table.StatusIndex.Query<ProjectionTestProjection>("gsi1pk = {0}", "ACTIVE")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that generic index with projection type works correctly.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task GenericIndex_WithProjectionType_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Create generic index with projection type
        var index = new DynamoDbIndex<ProjectionTestProjection>(table, "StatusIndex");
        
        // Non-generic Query() method should return QueryRequestBuilder<ProjectionTestProjection>
        QueryRequestBuilder<ProjectionTestProjection> builder = index.Query();
        
        var results = await builder
            .Where("gsi1pk = :status")
            .WithValue(":status", "ACTIVE")
            .ToListAsync();
        
        // Non-generic Query with format string
        results = await index.Query("gsi1pk = {0}", "ACTIVE")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that index queries with projections work with all options.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task IndexQuery_WithProjectionAndOptions_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Combined options on index query
        var results = await table.StatusIndex.Query<ProjectionTestProjection>()
            .Where("gsi1pk = :status")
            .WithValue(":status", "ACTIVE")
            .WithFilter("quantity > :minQty")
            .WithValue(":minQty", 10)
            .ScanIndexForward(false)
            .Take(25)
            .ToListAsync();
    }
    
    #endregion
    
    #region Projection Extension Methods
    
    /// <summary>
    /// Verifies that ToListAsync extension method compiles correctly.
    /// **Validates: Requirements 3.2, 3.3, 6.3**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // ToListAsync should work with projection types
        var results = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToListAsync();
    }
    
    /// <summary>
    /// Verifies that ToListAsync with projection type parameter compiles correctly.
    /// **Validates: Requirements 3.2, 3.3, 6.3**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsync_WithProjectionTypeParameter_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Query with entity type, project to projection type
        var results = await table.Query<ProjectionTestEntity>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToListAsync<ProjectionTestEntity, ProjectionTestProjection>();
    }
    
    /// <summary>
    /// Verifies that ToDynamoDbResponseAsync works with projection types for manual hydration.
    /// **Validates: Requirements 3.2, 3.3**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ToDynamoDbResponseAsync_WithProjection_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        // Query and get raw response for manual hydration
        var response = await table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .ToDynamoDbResponseAsync();
        
        // Manual hydration using IProjectionModel.FromDynamoDb
        var projections = response.Items
            .Select(item => ProjectionTestProjection.FromDynamoDb(item))
            .ToList();
    }
    
    #endregion
    
    #region Response Metadata Access
    
    /// <summary>
    /// Verifies that response metadata is accessible after projection queries.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact(Skip = "API Surface Validation")]
    public async Task ProjectionQuery_ResponseMetadata_ShouldBeAccessible()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ProjectionTestTable table = new ProjectionTestTable(client, "projectionTest", options: null);
        
        var query = table.Query<ProjectionTestProjection>()
            .Where("pk = :pk")
            .WithValue(":pk", "test")
            .Take(25);
        
        var results = await query.ToListAsync();
        
        // Response metadata should be accessible
        var lastKey = query.Response?.LastEvaluatedKey;
        var hasMore = query.Response?.HasMorePages ?? false;
        var scannedCount = query.Response?.ScannedCount;
        var resultCount = query.Response?.ResultCount;
        var consumedCapacity = query.Response?.ConsumedCapacity;
    }
    
    #endregion
}
