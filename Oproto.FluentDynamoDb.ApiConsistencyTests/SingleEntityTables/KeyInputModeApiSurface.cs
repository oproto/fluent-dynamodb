using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface compile tests for KeyInputMode parameter on standard accessor methods.
/// These tests verify that Get, Delete, Update, ConditionCheck, GetAsync, and DeleteAsync
/// accept explicit KeyInputMode.Auto, KeyInputMode.Raw, and KeyInputMode.Value parameters
/// on entities with string keys that have prefixes (and no typed overloads).
/// Requirements: 4.1, 7.1, 7.3
/// </summary>
public class KeyInputModeApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_PrefixedKeyEntity_Get_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === Get with explicit KeyInputMode values ===
        // Default (omitted — standard behavior)
        var builder = table.PrefixedKeyEntitys.Get("ORDER#12345", "sortKey");

        // Explicit Auto
        builder = table.PrefixedKeyEntitys.Get("12345", "sortKey", KeyInputMode.Auto);

        // Explicit Raw
        builder = table.PrefixedKeyEntitys.Get("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Explicit Value
        builder = table.PrefixedKeyEntitys.Get("12345", "sortKey", KeyInputMode.Value);

        // Execute with KeyInputMode
        PrefixedKeyEntity? result = await table.PrefixedKeyEntitys
            .Get("12345", "sortKey", KeyInputMode.Auto)
            .GetItemAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_PrefixedKeyEntity_Delete_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === Delete with explicit KeyInputMode values ===
        // Default (omitted)
        var builder = table.PrefixedKeyEntitys.Delete("ORDER#12345", "sortKey");

        // Explicit Auto
        builder = table.PrefixedKeyEntitys.Delete("12345", "sortKey", KeyInputMode.Auto);

        // Explicit Raw
        builder = table.PrefixedKeyEntitys.Delete("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Explicit Value
        builder = table.PrefixedKeyEntitys.Delete("12345", "sortKey", KeyInputMode.Value);

        // Execute via DeleteAsync
        await table.PrefixedKeyEntitys
            .Delete("12345", "sortKey", KeyInputMode.Value)
            .DeleteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_PrefixedKeyEntity_Update_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === Update with explicit KeyInputMode values ===
        // Default (omitted)
        var builder = table.PrefixedKeyEntitys.Update("ORDER#12345", "sortKey");

        // Explicit Auto
        builder = table.PrefixedKeyEntitys.Update("12345", "sortKey", KeyInputMode.Auto);

        // Explicit Raw
        builder = table.PrefixedKeyEntitys.Update("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Explicit Value
        builder = table.PrefixedKeyEntitys.Update("12345", "sortKey", KeyInputMode.Value);

        // Chain and execute
        await table.PrefixedKeyEntitys
            .Update("12345", "sortKey", KeyInputMode.Auto)
            .Set(x => new PrefixedKeyEntityUpdateModel { Amount = 100m })
            .UpdateAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public void KeyInputMode_PrefixedKeyEntity_ConditionCheck_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === ConditionCheck with explicit KeyInputMode values ===
        // Default (omitted)
        var builder = table.PrefixedKeyEntitys.ConditionCheck("ORDER#12345", "sortKey");

        // Explicit Auto
        builder = table.PrefixedKeyEntitys.ConditionCheck("12345", "sortKey", KeyInputMode.Auto);

        // Explicit Raw
        builder = table.PrefixedKeyEntitys.ConditionCheck("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Explicit Value
        builder = table.PrefixedKeyEntitys.ConditionCheck("12345", "sortKey", KeyInputMode.Value);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_PrefixedKeyEntity_GetAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === GetAsync convenience method with KeyInputMode ===
        // Default (omitted)
        PrefixedKeyEntity? result = await table.PrefixedKeyEntitys.GetAsync("ORDER#12345", "sortKey");

        // Explicit Auto
        result = await table.PrefixedKeyEntitys.GetAsync("12345", "sortKey", KeyInputMode.Auto);

        // Explicit Raw
        result = await table.PrefixedKeyEntitys.GetAsync("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Explicit Value
        result = await table.PrefixedKeyEntitys.GetAsync("12345", "sortKey", KeyInputMode.Value);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_PrefixedKeyEntity_DeleteAsync_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === DeleteAsync convenience method with KeyInputMode ===
        // Default (omitted)
        await table.PrefixedKeyEntitys.DeleteAsync("ORDER#12345", "sortKey");

        // Explicit Auto
        await table.PrefixedKeyEntitys.DeleteAsync("12345", "sortKey", KeyCondition.None, KeyInputMode.Auto);

        // Explicit Raw
        await table.PrefixedKeyEntitys.DeleteAsync("ORDER#12345", "sortKey", KeyCondition.None, KeyInputMode.Raw);

        // Explicit Value
        await table.PrefixedKeyEntitys.DeleteAsync("12345", "sortKey", KeyCondition.None, KeyInputMode.Value);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_CompositePrefixedKeyEntity_AllOperations_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new CompositePrefixedKeyTableTable(client, "compositePrefixedKeyTable", options: null);

        // === CompositePrefixedKeyEntity has both PK prefix (CUSTOMER) and SK prefix (INVOICE) ===
        // Get with KeyInputMode
        var getBuilder = table.CompositePrefixedKeyEntitys.Get("custId", "invoiceId", KeyInputMode.Auto);
        getBuilder = table.CompositePrefixedKeyEntitys.Get("CUSTOMER#custId", "INVOICE#invoiceId", KeyInputMode.Raw);
        getBuilder = table.CompositePrefixedKeyEntitys.Get("custId", "invoiceId", KeyInputMode.Value);

        // Delete with KeyInputMode
        var deleteBuilder = table.CompositePrefixedKeyEntitys.Delete("custId", "invoiceId", KeyInputMode.Auto);
        deleteBuilder = table.CompositePrefixedKeyEntitys.Delete("CUSTOMER#custId", "INVOICE#invoiceId", KeyInputMode.Raw);

        // Update with KeyInputMode
        var updateBuilder = table.CompositePrefixedKeyEntitys.Update("custId", "invoiceId", KeyInputMode.Auto);
        updateBuilder = table.CompositePrefixedKeyEntitys.Update("CUSTOMER#custId", "INVOICE#invoiceId", KeyInputMode.Raw);

        // ConditionCheck with KeyInputMode
        var condBuilder = table.CompositePrefixedKeyEntitys.ConditionCheck("custId", "invoiceId", KeyInputMode.Value);
        condBuilder = table.CompositePrefixedKeyEntitys.ConditionCheck("CUSTOMER#custId", "INVOICE#invoiceId", KeyInputMode.Raw);

        // GetAsync with KeyInputMode
        CompositePrefixedKeyEntity? result = await table.CompositePrefixedKeyEntitys
            .GetAsync("custId", "invoiceId", KeyInputMode.Auto);

        // DeleteAsync with KeyInputMode
        await table.CompositePrefixedKeyEntitys.DeleteAsync("custId", "invoiceId", KeyCondition.None, KeyInputMode.Value);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task KeyInputMode_TableLevel_PrefixedKeyEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === Table-level operations with KeyInputMode ===
        // Table-level Get
        var getBuilder = table.Get("12345", "sortKey", KeyInputMode.Auto);
        getBuilder = table.Get("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Table-level GetAsync
        PrefixedKeyEntity? result = await table.GetAsync("12345", "sortKey", KeyInputMode.Value);

        // Table-level Delete
        var deleteBuilder = table.Delete("12345", "sortKey", KeyInputMode.Auto);
        deleteBuilder = table.Delete("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Table-level DeleteAsync
        await table.DeleteAsync("12345", "sortKey", KeyCondition.None, KeyInputMode.Value);

        // Table-level Update
        var updateBuilder = table.Update("12345", "sortKey", KeyInputMode.Auto);
        updateBuilder = table.Update("ORDER#12345", "sortKey", KeyInputMode.Raw);

        // Table-level ConditionCheck
        var condBuilder = table.ConditionCheck("12345", "sortKey", KeyInputMode.Value);
        condBuilder = table.ConditionCheck("ORDER#12345", "sortKey", KeyInputMode.Raw);
    }
}
