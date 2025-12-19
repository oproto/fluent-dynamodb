using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Indexes;

/// <summary>
/// API surface tests for generated typed index classes with projection types.
/// 
/// **Validates: Requirement 2.6 from enhanced-index-table-generation spec**
/// 
/// Note: The non-generic Query methods (Query(), Query(Expression), Query(Expression, Expression))
/// are only generated when a projection type is defined via [UseProjection] attribute on the index.
/// 
/// These methods are validated through property-based tests in the source generator unit tests:
/// - IndexProjectionQueryMethodsPropertyTests.cs (Property 8)
/// 
/// The property-based tests verify that:
/// 1. When a projection type is defined, non-generic Query() method is generated
/// 2. When a projection type is defined, non-generic Query(Expression) method is generated
/// 3. When a projection type is defined, non-generic Query(Expression, Expression) method is generated
/// 4. When NO projection type is defined, non-generic Query methods are NOT generated
/// 
/// This file serves as documentation of the expected API surface for projection types.
/// </summary>
/// <remarks>
/// Example usage when projection type is defined:
/// <code>
/// // Entity with [UseProjection(typeof(ProductProjection))] on GSI
/// [DynamoDbTable("products")]
/// public partial class Product
/// {
///     [GlobalSecondaryIndex("gsi1", IsPartitionKey = true, Name = "CategoryIndex")]
///     [UseProjection(typeof(ProductProjection))]
///     [DynamoDbAttribute("gsi1pk")]
///     public string CategoryId { get; set; }
/// }
/// 
/// // Generated non-generic Query methods on CategoryIndex:
/// // - Query() returns QueryRequestBuilder&lt;ProductProjection&gt;
/// // - Query(Expression&lt;Func&lt;ProductProjection, bool&gt;&gt;) returns QueryRequestBuilder&lt;ProductProjection&gt;
/// // - Query(Expression, Expression) returns QueryRequestBuilder&lt;ProductProjection&gt;
/// 
/// // Usage:
/// var results = await table.CategoryIndex.Query()
///     .Where(x => x.CategoryId == "electronics")
///     .ToListAsync();
/// </code>
/// </remarks>
public class IndexProjectionApiSurface
{
    /// <summary>
    /// Documents that non-generic Query() method exists when projection type is defined.
    /// This is validated by property-based tests in IndexProjectionQueryMethodsPropertyTests.
    /// **Validates: Requirement 2.6**
    /// </summary>
    [Fact(Skip = "Documentation only - validated by property-based tests")]
    public void IndexWithProjection_NonGenericQuery_ExistsWhenProjectionTypeDefined()
    {
        // This test documents the expected API surface.
        // Actual validation is done in IndexProjectionQueryMethodsPropertyTests.cs
        // 
        // When [UseProjection(typeof(TProjection))] is applied to a GSI property,
        // the generated index class will have:
        //   public QueryRequestBuilder<TProjection> Query()
        //
        // This allows querying without specifying the type parameter:
        //   var results = await table.CategoryIndex.Query().Where(...).ToListAsync();
    }

    /// <summary>
    /// Documents that non-generic Query(Expression) method exists when projection type is defined.
    /// This is validated by property-based tests in IndexProjectionQueryMethodsPropertyTests.
    /// **Validates: Requirement 2.6**
    /// </summary>
    [Fact(Skip = "Documentation only - validated by property-based tests")]
    public void IndexWithProjection_NonGenericQueryWithExpression_ExistsWhenProjectionTypeDefined()
    {
        // This test documents the expected API surface.
        // Actual validation is done in IndexProjectionQueryMethodsPropertyTests.cs
        //
        // When [UseProjection(typeof(TProjection))] is applied to a GSI property,
        // the generated index class will have:
        //   public QueryRequestBuilder<TProjection> Query(Expression<Func<TProjection, bool>> keyCondition)
        //
        // This allows querying with a lambda expression without specifying the type parameter:
        //   var results = await table.CategoryIndex.Query(x => x.CategoryId == "electronics").ToListAsync();
    }

    /// <summary>
    /// Documents that non-generic Query(Expression, Expression) method exists when projection type is defined.
    /// This is validated by property-based tests in IndexProjectionQueryMethodsPropertyTests.
    /// **Validates: Requirement 2.6**
    /// </summary>
    [Fact(Skip = "Documentation only - validated by property-based tests")]
    public void IndexWithProjection_NonGenericQueryWithTwoExpressions_ExistsWhenProjectionTypeDefined()
    {
        // This test documents the expected API surface.
        // Actual validation is done in IndexProjectionQueryMethodsPropertyTests.cs
        //
        // When [UseProjection(typeof(TProjection))] is applied to a GSI property,
        // the generated index class will have:
        //   public QueryRequestBuilder<TProjection> Query(
        //       Expression<Func<TProjection, bool>> keyCondition,
        //       Expression<Func<TProjection, bool>> filterCondition)
        //
        // This allows querying with key and filter expressions without specifying the type parameter:
        //   var results = await table.CategoryIndex.Query(
        //       x => x.CategoryId == "electronics",
        //       x => x.ProductName.Contains("Pro")
        //   ).ToListAsync();
    }
}
