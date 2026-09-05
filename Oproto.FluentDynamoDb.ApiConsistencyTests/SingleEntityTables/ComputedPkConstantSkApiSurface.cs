using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface compile tests for typed parameter convenience overloads when
/// an entity has a computed PK and a constant SK (expression-body).
/// The constant SK should be OMITTED from all typed overloads — the raw methods
/// auto-fill it and emit single-parameter signatures (PK only).
///
/// Bug condition exploration test: These tests encode the expected (correct) behavior.
/// On UNFIXED code, these tests will FAIL TO COMPILE because the generator incorrectly
/// includes sK in typed overload parameters and delegation calls.
///
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5
/// </summary>
public class ComputedPkConstantSkApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedPkConstantSk_Get_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedPkConstantSkTable(client, "computedPkConstantSkTable", options: null);

        // === Typed overload Get with only PK source params (no sK — it's constant) ===
        GetItemRequestBuilder<ComputedPkConstantSkEntity> getBuilder =
            table.ComputedPkConstantSkEntitys.Get(Guid.NewGuid(), Guid.NewGuid());

        // Execute via builder
        ComputedPkConstantSkEntity? result = await table.ComputedPkConstantSkEntitys
            .Get(Guid.NewGuid(), Guid.NewGuid())
            .GetItemAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedPkConstantSk_Delete_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedPkConstantSkTable(client, "computedPkConstantSkTable", options: null);

        // === Typed overload Delete with only PK source params (no sK — it's constant) ===
        DeleteItemRequestBuilder<ComputedPkConstantSkEntity> deleteBuilder =
            table.ComputedPkConstantSkEntitys.Delete(Guid.NewGuid(), Guid.NewGuid());

        // Execute via builder
        await table.ComputedPkConstantSkEntitys
            .Delete(Guid.NewGuid(), Guid.NewGuid())
            .DeleteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedPkConstantSk_Update_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedPkConstantSkTable(client, "computedPkConstantSkTable", options: null);

        // === Typed overload Update with only PK source params (no sK — it's constant) ===
        ComputedPkConstantSkEntityUpdateBuilder updateBuilder =
            table.ComputedPkConstantSkEntitys.Update(Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact(Skip = "API Surface Validation")]
    public void TypedOverloads_ComputedPkConstantSk_ConditionCheck_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedPkConstantSkTable(client, "computedPkConstantSkTable", options: null);

        // === Typed overload ConditionCheck with only PK source params (no sK — it's constant) ===
        ConditionCheckBuilder<ComputedPkConstantSkEntity> conditionBuilder =
            table.ComputedPkConstantSkEntitys.ConditionCheck(Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact(Skip = "API Surface Validation")]
    public void StandardOverloads_ComputedPkConstantSk_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedPkConstantSkTable(client, "computedPkConstantSkTable", options: null);

        // === Standard string overloads — single PK string, no SK (raw methods omit constant SK) ===
        GetItemRequestBuilder<ComputedPkConstantSkEntity> getBuilder =
            table.ComputedPkConstantSkEntitys.Get("pk_value");

        DeleteItemRequestBuilder<ComputedPkConstantSkEntity> deleteBuilder =
            table.ComputedPkConstantSkEntitys.Delete("pk_value");

        ComputedPkConstantSkEntityUpdateBuilder updateBuilder =
            table.ComputedPkConstantSkEntitys.Update("pk_value");

        ConditionCheckBuilder<ComputedPkConstantSkEntity> conditionCheckBuilder =
            table.ComputedPkConstantSkEntitys.ConditionCheck("pk_value");
    }
}
