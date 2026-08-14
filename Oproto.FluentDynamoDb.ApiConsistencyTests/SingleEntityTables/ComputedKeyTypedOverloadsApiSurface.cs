using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface compile tests for typed parameter convenience overloads (computed key entities).
/// These tests verify that typed overload method signatures compile correctly for
/// Get, Delete, Update, ConditionCheck, GetAsync, and DeleteAsync operations.
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.6, 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2, 6.1
/// </summary>
public class ComputedKeyTypedOverloadsApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_Get_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload Get ===
        // ComputedKeyEntity has computed PK (Year, Month, Day) + simple string SK
        GetItemRequestBuilder<ComputedKeyEntity> getBuilder =
            table.ComputedKeyEntitys.Get(2024, 12, 25, "sortKeyValue");

        // Execute via builder
        ComputedKeyEntity? result = await table.ComputedKeyEntitys
            .Get(2024, 12, 25, "sortKeyValue")
            .GetItemAsync();

        // === Standard string overload still exists (no KeyInputMode since typed overload exists) ===
        getBuilder = table.ComputedKeyEntitys.Get("2024#12#25", "sortKeyValue");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_Delete_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload Delete ===
        DeleteItemRequestBuilder<ComputedKeyEntity> deleteBuilder =
            table.ComputedKeyEntitys.Delete(2024, 12, 25, "sortKeyValue");

        // Execute via builder
        await table.ComputedKeyEntitys
            .Delete(2024, 12, 25, "sortKeyValue")
            .DeleteAsync();

        // === Standard string overload still exists ===
        deleteBuilder = table.ComputedKeyEntitys.Delete("2024#12#25", "sortKeyValue");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_Update_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload Update ===
        ComputedKeyEntityUpdateBuilder updateBuilder =
            table.ComputedKeyEntitys.Update(2024, 12, 25, "sortKeyValue");

        // Chain a Set and execute
        await table.ComputedKeyEntitys
            .Update(2024, 12, 25, "sortKeyValue")
            .Set(x => new ComputedKeyEntityUpdateModel { Title = "Holiday" })
            .UpdateAsync();

        // === Standard string overload still exists ===
        updateBuilder = table.ComputedKeyEntitys.Update("2024#12#25", "sortKeyValue");
    }

    [Fact(Skip = "API Surface Validation")]
    public void TypedOverloads_ComputedKeyEntity_ConditionCheck_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload ConditionCheck ===
        ConditionCheckBuilder<ComputedKeyEntity> conditionBuilder =
            table.ComputedKeyEntitys.ConditionCheck(2024, 12, 25, "sortKeyValue");

        // Chain a Where condition
        conditionBuilder = table.ComputedKeyEntitys
            .ConditionCheck(2024, 12, 25, "sortKeyValue")
            .Where("attribute_exists(title)");

        // === Standard string overload still exists ===
        conditionBuilder = table.ComputedKeyEntitys.ConditionCheck("2024#12#25", "sortKeyValue");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Table-level typed overloads (delegate to entity accessor) ===
        // Table-level Get typed overload
        GetItemRequestBuilder<ComputedKeyEntity> getBuilder = table.Get(2024, 12, 25, "sortKeyValue");

        // Table-level Delete typed overload
        DeleteItemRequestBuilder<ComputedKeyEntity> deleteBuilder = table.Delete(2024, 12, 25, "sortKeyValue");

        // Table-level Update typed overload
        ComputedKeyEntityUpdateBuilder updateBuilder = table.Update(2024, 12, 25, "sortKeyValue");

        // Table-level ConditionCheck typed overload
        ConditionCheckBuilder<ComputedKeyEntity> conditionBuilder = table.ConditionCheck(2024, 12, 25, "sortKeyValue");

        // === Table-level standard string overloads still exist ===
        getBuilder = table.Get("2024#12#25", "sortKeyValue");
        deleteBuilder = table.Delete("2024#12#25", "sortKeyValue");
        updateBuilder = table.Update("2024#12#25", "sortKeyValue");
        conditionBuilder = table.ConditionCheck("2024#12#25", "sortKeyValue");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_GetAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload GetAsync ===
        // ComputedKeyEntity has computed PK (Year, Month, Day) + simple string SK
        // GetAsync accepts the typed source property parameters and returns Task<ComputedKeyEntity?>
        Task<ComputedKeyEntity?> getTask = table.ComputedKeyEntitys.GetAsync(2024, 12, 25, "sk");

        // Execute and verify return type
        ComputedKeyEntity? result = await table.ComputedKeyEntitys.GetAsync(2024, 12, 25, "sk");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_DeleteAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Entity Accessor: Typed overload DeleteAsync ===
        // DeleteAsync accepts typed source property parameters + KeyCondition and returns Task
        Task deleteTask = table.ComputedKeyEntitys.DeleteAsync(2024, 12, 25, "sk", KeyCondition.None);

        // Execute with explicit KeyCondition
        await table.ComputedKeyEntitys.DeleteAsync(2024, 12, 25, "sk", KeyCondition.MustExist);

        // Execute with default KeyCondition (omitted)
        await table.ComputedKeyEntitys.DeleteAsync(2024, 12, 25, "sk");
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TypedOverloads_ComputedKeyEntity_TableLevel_Async_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new ComputedKeyTableTable(client, "computedKeyTable", options: null);

        // === Table-level typed GetAsync ===
        // Delegates to entity accessor's typed GetAsync, returns Task<ComputedKeyEntity?>
        Task<ComputedKeyEntity?> getTask = table.GetAsync(2024, 12, 25, "sk");
        ComputedKeyEntity? getResult = await table.GetAsync(2024, 12, 25, "sk");

        // === Table-level typed DeleteAsync ===
        // Delegates to entity accessor's typed DeleteAsync, returns Task
        Task deleteTask = table.DeleteAsync(2024, 12, 25, "sk");
        await table.DeleteAsync(2024, 12, 25, "sk", KeyCondition.MustExist);
        await table.DeleteAsync(2024, 12, 25, "sk", KeyCondition.None);
    }
}
