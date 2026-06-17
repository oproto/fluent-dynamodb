using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Tests that the generated multi-entity CreateTableAsync correctly provisions
/// GSIs from non-default entities with proper AttributeDefinitions.
/// This validates the Bug 2 fix end-to-end through the actual generated code.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "GsiProvisioning")]
public class GeneratedMultiEntityGsiTests : IntegrationTestBase
{
    private readonly List<string> _additionalTablesToCleanup = new();

    public GeneratedMultiEntityGsiTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task DisposeAsync()
    {
        foreach (var tableName in _additionalTablesToCleanup)
        {
            try { await DynamoDb.DeleteTableAsync(tableName); }
            catch (ResourceNotFoundException) { }
        }
        await base.DisposeAsync();
    }

    /// <summary>
    /// Calls the actual generated TestMultiEntityTable.CreateTableAsync and verifies
    /// that non-default entity GSIs are provisioned with correct AttributeDefinitions.
    /// </summary>
    [Fact]
    public async Task GeneratedCreateTableAsync_MultiEntity_ProvisionesNonDefaultEntityGsis()
    {
        // Arrange
        var tableName = $"test_gen_multi_gsi_{Guid.NewGuid():N}";
        _additionalTablesToCleanup.Add(tableName);

        // Act - Call the ACTUAL generated CreateTableAsync on the multi-entity table class
        var result = await TestMultiEntityTable.CreateTableAsync(DynamoDb, tableName,
            new TableCreationOptions { WaitForActive = true });

        // Assert - Table should be active
        result.TableStatus.Should().Be(TableStatus.ACTIVE);

        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;

        // Verify InventoryEntity's "StatusIndex" GSI is provisioned
        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Generated multi-entity CreateTableAsync should provision GSIs from non-default entities");

        var statusIndex = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        statusIndex.Should().NotBeNull("StatusIndex from InventoryEntity should be provisioned");

        // Verify GSI key schema
        statusIndex!.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_pk" && k.KeyType == KeyType.HASH);
        statusIndex.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_sk" && k.KeyType == KeyType.RANGE);

        // CRITICAL: Verify AttributeDefinitions include the GSI key attributes
        table.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_pk",
            "AttributeDefinitions must include GSI partition key attribute 'gsi1_pk'");
        table.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_sk",
            "AttributeDefinitions must include GSI sort key attribute 'gsi1_sk'");
    }

    /// <summary>
    /// Tests single-entity path: default entity with GSIs, created via TableCreator.
    /// This reproduces the exact scenario of the user's consuming project where
    /// EmployeeEntity (default, has gsi1/gsi3/gsi4) is passed to TableCreator.
    /// Verifies items are properly indexed and queryable on GSIs.
    /// </summary>
    [Fact]
    public async Task SingleEntityPath_DefaultEntityWithGsis_GsiQueriesReturnResults()
    {
        // Arrange - Simulate EmployeeEntity scenario: default entity with multiple GSIs
        // Using InventoryEntity which has a GSI (StatusIndex: gsi1_pk, gsi1_sk)
        var tableName = $"test_single_entity_gsi_{Guid.NewGuid():N}";
        _additionalTablesToCleanup.Add(tableName);

        var metadata = InventoryEntity.GetEntityMetadata();
        var creator = new TableCreator();

        // Act - This is exactly what the generated single-entity CreateTableAsync does
        await creator.CreateAsync(DynamoDb, tableName, metadata,
            new TableCreationOptions { WaitForActive = true });

        // Verify AttributeDefinitions
        var desc = await DynamoDb.DescribeTableAsync(tableName);
        desc.Table.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_pk",
            "AttributeDefinitions must include GSI PK");
        desc.Table.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_sk",
            "AttributeDefinitions must include GSI SK");

        // Put an item WITH GSI key values populated
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "WAREHOUSE#1" },
            ["sk"] = new AttributeValue { S = "ITEM#001" },
            ["entity_type"] = new AttributeValue { S = "INVENTORY" },
            ["gsi1_pk"] = new AttributeValue { S = "IN_STOCK" },
            ["gsi1_sk"] = new AttributeValue { S = "INVENTORY#001" },
            ["item_name"] = new AttributeValue { S = "Widget" }
        };
        await DynamoDb.PutItemAsync(tableName, item);

        // Query the GSI
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = tableName,
            IndexName = "StatusIndex",
            KeyConditionExpression = "#pk = :pk",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = "gsi1_pk" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = "IN_STOCK" }
            }
        });

        // Assert - Item must appear in GSI query
        queryResponse.Items.Should().NotBeEmpty(
            "Item with gsi1_pk='IN_STOCK' should appear in StatusIndex GSI query");
    }

    /// <summary>
    /// Verifies that items put into the table actually appear in GSI queries.
    /// This is the end-to-end test that catches the user's reported issue.
    /// </summary>
    [Fact]
    public async Task GeneratedCreateTableAsync_MultiEntity_GsiQueriesReturnResults()
    {
        // Arrange
        var tableName = $"test_gen_multi_gsi_query_{Guid.NewGuid():N}";
        _additionalTablesToCleanup.Add(tableName);

        await TestMultiEntityTable.CreateTableAsync(DynamoDb, tableName,
            new TableCreationOptions { WaitForActive = true });

        // Put an InventoryEntity item with GSI key values
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "WAREHOUSE#1" },
            ["sk"] = new AttributeValue { S = "ITEM#001" },
            ["entity_type"] = new AttributeValue { S = "INVENTORY" },
            ["gsi1_pk"] = new AttributeValue { S = "IN_STOCK" },
            ["gsi1_sk"] = new AttributeValue { S = "INVENTORY#001" },
            ["item_name"] = new AttributeValue { S = "Widget" },
            ["quantity"] = new AttributeValue { N = "100" }
        };

        await DynamoDb.PutItemAsync(tableName, item);

        // Act - Query the GSI
        var queryResponse = await DynamoDb.QueryAsync(new QueryRequest
        {
            TableName = tableName,
            IndexName = "StatusIndex",
            KeyConditionExpression = "gsi1_pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = "IN_STOCK" }
            }
        });

        // Assert - GSI query should return the item
        queryResponse.Items.Should().NotBeEmpty(
            "GSI query on 'StatusIndex' should return items that have gsi1_pk set. " +
            "If empty, AttributeDefinitions likely missing gsi1_pk/gsi1_sk.");
        queryResponse.Items[0]["item_name"].S.Should().Be("Widget");
    }
}
