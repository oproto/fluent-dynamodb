using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

/// <summary>
/// API surface compile tests verifying that KeyCondition can be passed positionally
/// as the 3rd argument to Update/Delete on entities that also qualify for KeyInputMode.
/// 
/// These tests guard against a backwards-compatibility regression where KeyInputMode
/// was accidentally placed before KeyCondition in the generated parameter list.
/// If the parameter order is ever reversed, these tests will fail to compile.
///
/// See: ISSUE_update_keyinputmode_parameter_ordering.md
/// </summary>
public class KeyConditionWithKeyInputModeApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task Update_KeyCondition_PositionalThirdArg_CompositeKey_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === KeyCondition as 3rd positional argument (backwards-compatible usage) ===
        // This was the pre-KeyInputMode calling convention: Update(pk, sk, KeyCondition)
        // If KeyInputMode is ever placed before KeyCondition, this will fail to compile.
        var builder = table.PrefixedKeyEntitys.Update("ORDER#12345", "sortKey", KeyCondition.MustExist);

        await table.PrefixedKeyEntitys
            .Update("ORDER#12345", "sortKey", KeyCondition.MustExist)
            .Set(x => new PrefixedKeyEntityUpdateModel { Amount = 100m })
            .UpdateAsync();

        // === KeyCondition.MustNotExist positionally ===
        builder = table.PrefixedKeyEntitys.Update("ORDER#12345", "sortKey", KeyCondition.MustNotExist);

        // === Both KeyCondition and KeyInputMode (named) ===
        builder = table.PrefixedKeyEntitys.Update("12345", "sortKey", KeyCondition.MustExist, mode: KeyInputMode.Auto);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task Update_KeyCondition_PositionalThirdArg_TableLevel_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new PrefixedKeyTableTable(client, "prefixedKeyTable", options: null);

        // === Table-level Update with KeyCondition positionally ===
        var builder = table.Update("ORDER#12345", "sortKey", KeyCondition.MustExist);

        await table.Update("ORDER#12345", "sortKey", KeyCondition.MustExist)
            .Set(x => new PrefixedKeyEntityUpdateModel { Status = "active" })
            .UpdateAsync();

        // === Table-level with both KeyCondition and KeyInputMode ===
        builder = table.Update("12345", "sortKey", KeyCondition.MustExist, mode: KeyInputMode.Value);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task Update_KeyCondition_PositionalThirdArg_CompositePrefixed_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var table = new CompositePrefixedKeyTableTable(client, "compositePrefixedKeyTable", options: null);

        // === Both PK and SK have prefixes — KeyCondition still works positionally ===
        var builder = table.CompositePrefixedKeyEntitys.Update("CUSTOMER#custId", "INVOICE#invoiceId", KeyCondition.MustExist);
        builder = table.CompositePrefixedKeyEntitys.Update("custId", "invoiceId", KeyCondition.MustNotExist);

        // === With explicit mode ===
        builder = table.CompositePrefixedKeyEntitys.Update("custId", "invoiceId", KeyCondition.MustExist, mode: KeyInputMode.Auto);
    }
}
