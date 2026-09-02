using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface compile tests for Put key prefix application feature.
/// These tests verify that:
/// - Existing Put API patterns still compile (no signature breakage)
/// - WithKeyMode method is available and chainable on PutItemRequestBuilder
/// - Both ToDynamoDb overloads are accessible
/// - PutAsync(entity) and PutAsync(entity, KeyCondition) convenience methods still work
/// Requirements: 8.3
/// </summary>
public class PutKeyPrefixApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task ExistingPutPatterns_PrefixedKeyEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);
        var entity = new PrefixedKeyEntity { Pk = "ORDER#12345", Sk = "sortKey", Amount = 99.99m, Status = "active" };

        // === Existing builder pattern: Put(entity).PutAsync() ===
        await table.PrefixedKeyEntitys.Put(entity).PutAsync();

        // === Table-level Put(entity).PutAsync() ===
        await table.Put(entity).PutAsync();

        // === Existing Put builder without entity ===
        PutItemRequestBuilder<PrefixedKeyEntity> builder = table.PrefixedKeyEntitys.Put();

        // === Put with condition expression (existing pattern) ===
        await table.PrefixedKeyEntitys.Put(entity)
            .Where(x => x.Pk.AttributeNotExists())
            .PutAsync();

        // === Put with IfNotExists (existing pattern) ===
        await table.PrefixedKeyEntitys.Put(entity).IfNotExists().PutAsync();

        // === Put with IfExists (existing pattern) ===
        await table.PrefixedKeyEntitys.Put(entity).IfExists().PutAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task WithKeyMode_PrefixedKeyEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "sortKey", Amount = 99.99m, Status = "active" };

        // === WithKeyMode is available and returns PutItemRequestBuilder for fluent chaining ===
        PutItemRequestBuilder<PrefixedKeyEntity> builder = table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Auto);

        // === WithKeyMode with Raw mode ===
        builder = table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Raw);

        // === WithKeyMode with Value mode ===
        builder = table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Value);

        // === WithKeyMode with Default mode (no-op, resolves from options) ===
        builder = table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Default);

        // === WithKeyMode is chainable with other builder methods ===
        await table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Raw)
            .PutAsync();

        // === WithKeyMode combined with condition expression ===
        await table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Auto)
            .Where(x => x.Pk.AttributeNotExists())
            .PutAsync();

        // === WithKeyMode combined with IfNotExists ===
        await table.PrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Value)
            .IfNotExists()
            .PutAsync();

        // === Table-level Put with WithKeyMode ===
        await table.Put(entity)
            .WithKeyMode(KeyInputMode.Raw)
            .PutAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public void ToDynamoDb_BothOverloads_ShouldCompile()
    {
        var entity = new PrefixedKeyEntity { Pk = "ORDER#12345", Sk = "sortKey", Amount = 99.99m, Status = "active" };
        var options = new FluentDynamoDbOptions();

        // === Existing overload: ToDynamoDb(entity, options) ===
        var dict1 = PrefixedKeyEntity.ToDynamoDb(entity, options);

        // === Existing overload with null options ===
        var dict2 = PrefixedKeyEntity.ToDynamoDb(entity, null);

        // === New overload: ToDynamoDb(entity, options, keyInputMode) ===
        var dict3 = PrefixedKeyEntity.ToDynamoDb(entity, options, KeyInputMode.Auto);
        var dict4 = PrefixedKeyEntity.ToDynamoDb(entity, options, KeyInputMode.Raw);
        var dict5 = PrefixedKeyEntity.ToDynamoDb(entity, options, KeyInputMode.Value);
        var dict6 = PrefixedKeyEntity.ToDynamoDb(entity, options, KeyInputMode.Default);

        // === New overload with null options ===
        var dict7 = PrefixedKeyEntity.ToDynamoDb(entity, null, KeyInputMode.Auto);

        // === Return type is Dictionary<string, AttributeValue> ===
        Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> result = PrefixedKeyEntity.ToDynamoDb(entity, options, KeyInputMode.Auto);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task PutAsyncConvenienceMethods_PrefixedKeyEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);
        var entity = new PrefixedKeyEntity { Pk = "12345", Sk = "sortKey", Amount = 99.99m, Status = "active" };

        // === PutAsync(entity) convenience method — entity accessor ===
        await table.PrefixedKeyEntitys.PutAsync(entity);

        // === PutAsync(entity, KeyCondition) convenience method — MustNotExist ===
        await table.PrefixedKeyEntitys.PutAsync(entity, KeyCondition.MustNotExist);

        // === PutAsync(entity, KeyCondition) convenience method — MustExist ===
        await table.PrefixedKeyEntitys.PutAsync(entity, KeyCondition.MustExist);

        // === PutAsync(entity, KeyCondition.None) — no condition ===
        await table.PrefixedKeyEntitys.PutAsync(entity, KeyCondition.None);

        // === Table-level PutAsync(entity) ===
        await table.PutAsync(entity);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task WithKeyMode_CompositePrefixedKeyEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new CompositePrefixedKeyTableTable(client, "compositePrefixedKeyTable", options: null);
        var entity = new CompositePrefixedKeyEntity { Pk = "custId", Sk = "invoiceId", Total = 250m, Description = "Test" };

        // === Put with WithKeyMode on entity with both PK and SK prefixes ===
        await table.CompositePrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Auto)
            .PutAsync();

        // === Raw mode bypasses both key prefixes ===
        await table.CompositePrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Raw)
            .PutAsync();

        // === Value mode always prepends both key prefixes ===
        await table.CompositePrefixedKeyEntitys.Put(entity)
            .WithKeyMode(KeyInputMode.Value)
            .PutAsync();

        // === PutAsync convenience still works ===
        await table.CompositePrefixedKeyEntitys.PutAsync(entity);
        await table.CompositePrefixedKeyEntitys.PutAsync(entity, KeyCondition.MustNotExist);
    }
}
