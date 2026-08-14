using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults typed async methods on computed key entities.
/// Verifies that GetAsyncResult and DeleteAsyncResult are generated with correct typed parameters
/// for entities with [UseFluentResults] and computed keys.
/// Also verifies HideGeneratedAsyncMethods behavior for standard GetAsync/DeleteAsync suppression.
/// Requirements: 5.1, 5.3, 6.1, 6.4, 7.1, 7.2, 7.3, 7.4
/// </summary>
public class ComputedKeyFluentResultsApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task GetAsyncResult_EntityAccessor_TypedParams_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Entity accessor: Typed GetAsyncResult with computed PK params (Year, Month, Day) + SK ===
        var result = await table.ComputedKeyFluentResultsEntitys.GetAsyncResult(2024, 12, 25, "sortKeyValue");

        // === Verify return type is Task<Result<T?>> ===
        global::FluentResults.Result<ComputedKeyFluentResultsEntity?> typedResult = result;

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            ComputedKeyFluentResultsEntity? entity = result.Value;
        }

        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task GetAsyncResult_EntityAccessor_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);
        var cancellationToken = new CancellationToken();

        // === Entity accessor: Typed GetAsyncResult with CancellationToken ===
        var result = await table.ComputedKeyFluentResultsEntitys.GetAsyncResult(2024, 12, 25, "sk", cancellationToken);

        global::FluentResults.Result<ComputedKeyFluentResultsEntity?> typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_EntityAccessor_TypedParams_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Entity accessor: Typed DeleteAsyncResult with computed PK params + SK ===
        var result = await table.ComputedKeyFluentResultsEntitys.DeleteAsyncResult(2024, 12, 25, "sortKeyValue");

        // === Verify return type is Task<Result> ===
        global::FluentResults.Result typedResult = result;

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Delete succeeded
        }

        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_EntityAccessor_WithKeyCondition_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Entity accessor: Typed DeleteAsyncResult with KeyCondition ===
        var result = await table.ComputedKeyFluentResultsEntitys.DeleteAsyncResult(2024, 12, 25, "sk", KeyCondition.MustExist);

        global::FluentResults.Result typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_EntityAccessor_WithKeyConditionAndCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);
        var cancellationToken = new CancellationToken();

        // === Entity accessor: Typed DeleteAsyncResult with KeyCondition and CancellationToken ===
        var result = await table.ComputedKeyFluentResultsEntitys.DeleteAsyncResult(2024, 12, 25, "sk", KeyCondition.MustExist, cancellationToken);

        global::FluentResults.Result typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task GetAsyncResult_TableLevel_TypedParams_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Table-level: Typed GetAsyncResult delegates to entity accessor ===
        var result = await table.GetAsyncResult(2024, 12, 25, "sortKeyValue");

        // === Verify return type ===
        global::FluentResults.Result<ComputedKeyFluentResultsEntity?> typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_TableLevel_TypedParams_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Table-level: Typed DeleteAsyncResult delegates to entity accessor ===
        var result = await table.DeleteAsyncResult(2024, 12, 25, "sortKeyValue");

        // === Verify return type ===
        global::FluentResults.Result typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_TableLevel_WithKeyCondition_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === Table-level: Typed DeleteAsyncResult with KeyCondition ===
        var result = await table.DeleteAsyncResult(2024, 12, 25, "sk", KeyCondition.MustExist);

        global::FluentResults.Result typedResult = result;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task HideGeneratedAsyncMethods_True_SuppressesStandardTypedAsync()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsTableTable(client, "computedKeyFluentResultsTable", options: null);

        // === With HideGeneratedAsyncMethods = true (default for ComputedKeyFluentResultsEntity): ===
        // Standard typed GetAsync/DeleteAsync should NOT be generated.
        // The following would NOT compile if uncommented:
        // await table.ComputedKeyFluentResultsEntitys.GetAsync(2024, 12, 25, "sk");  // Should NOT compile
        // await table.ComputedKeyFluentResultsEntitys.DeleteAsync(2024, 12, 25, "sk");  // Should NOT compile
        // await table.GetAsync(2024, 12, 25, "sk");  // Should NOT compile
        // await table.DeleteAsync(2024, 12, 25, "sk");  // Should NOT compile

        // Only Result-returning methods should be available:
        var getResult = await table.ComputedKeyFluentResultsEntitys.GetAsyncResult(2024, 12, 25, "sk");
        var deleteResult = await table.ComputedKeyFluentResultsEntitys.DeleteAsyncResult(2024, 12, 25, "sk");

        // Table-level Result methods should also be available:
        var tableLevelGetResult = await table.GetAsyncResult(2024, 12, 25, "sk");
        var tableLevelDeleteResult = await table.DeleteAsyncResult(2024, 12, 25, "sk");

        // Verify types
        global::FluentResults.Result<ComputedKeyFluentResultsEntity?> typedGetResult = getResult;
        global::FluentResults.Result typedDeleteResult = deleteResult;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task HideGeneratedAsyncMethods_False_GeneratesBothStandardAndResultVariants()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyFluentResultsBothTableTable(client, "computedKeyFluentResultsBothTable", options: null);

        // === With HideGeneratedAsyncMethods = false (ComputedKeyFluentResultsBothEntity): ===
        // Both standard typed async methods AND Result-returning methods should be generated.

        // Standard typed async methods should exist:
        ComputedKeyFluentResultsBothEntity? getEntity = await table.ComputedKeyFluentResultsBothEntitys.GetAsync(2024, 12, 25, "sk");
        await table.ComputedKeyFluentResultsBothEntitys.DeleteAsync(2024, 12, 25, "sk");

        // Result-returning methods should also exist:
        var getResult = await table.ComputedKeyFluentResultsBothEntitys.GetAsyncResult(2024, 12, 25, "sk");
        var deleteResult = await table.ComputedKeyFluentResultsBothEntitys.DeleteAsyncResult(2024, 12, 25, "sk");

        // Table-level standard async methods:
        getEntity = await table.GetAsync(2024, 12, 25, "sk");
        await table.DeleteAsync(2024, 12, 25, "sk");

        // Table-level Result methods:
        getResult = await table.GetAsyncResult(2024, 12, 25, "sk");
        deleteResult = await table.DeleteAsyncResult(2024, 12, 25, "sk");

        // Verify types
        global::FluentResults.Result<ComputedKeyFluentResultsBothEntity?> typedGetResult = getResult;
        global::FluentResults.Result typedDeleteResult = deleteResult;
    }
}
