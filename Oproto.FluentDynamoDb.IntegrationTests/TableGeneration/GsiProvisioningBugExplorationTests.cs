using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Bug condition exploration tests for missing GSI/LSI table provisioning.
/// These tests demonstrate that:
/// - Bug 1: IntegrationTestBase.CreateTableAsync&lt;TEntity&gt;() does NOT provision GSIs/LSIs
/// - Bug 2: Multi-entity generated CreateTableAsync only uses default entity metadata,
///           omitting indexes from non-default entities
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "BugExploration")]
public class GsiProvisioningBugExplorationTests : IntegrationTestBase
{
    public GsiProvisioningBugExplorationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// Bug 1: IntegrationTestBase.CreateTableAsync&lt;TEntity&gt;() called with an entity
    /// that has GSI(s) declared via [GsiPartitionKey] — the created table should have GSIs.
    ///
    /// EXPECTED: This test FAILS on unfixed code because CreateTableAsync&lt;TEntity&gt;()
    /// manually builds CreateTableRequest from metadata.Properties only and never reads
    /// metadata.Indexes.
    ///
    /// Counterexample: DescribeTable returns null/empty GlobalSecondaryIndexes despite
    /// InventoryEntity declaring "StatusIndex" GSI with partition key "gsi1_pk" and sort key "gsi1_sk".
    /// </summary>
    [Fact]
    public async Task Bug1_CreateTableAsync_WithGsiEntity_ShouldProvisionGsi()
    {
        // Arrange - InventoryEntity declares GSI "StatusIndex" with [GsiPartitionKey("StatusIndex")]
        // and [GsiSortKey("StatusIndex")] on gsi1_pk and gsi1_sk attributes
        var metadata = InventoryEntity.GetEntityMetadata();

        // Verify precondition: entity metadata declares indexes
        metadata.Indexes.Should().NotBeEmpty(
            "InventoryEntity should declare at least one GSI in its metadata");
        
        var statusIndex = metadata.Indexes.FirstOrDefault(i => i.IndexName == "StatusIndex");
        statusIndex.Should().NotBeNull(
            "InventoryEntity should declare a 'StatusIndex' GSI");

        // Act - Create table using IntegrationTestBase.CreateTableAsync<TEntity>()
        // This is the buggy path that only reads metadata.Properties for key schema
        await CreateTableAsync<InventoryEntity>();

        // Assert - Verify the table has GSIs provisioned
        var describeResponse = await DynamoDb.DescribeTableAsync(TableName);
        var table = describeResponse.Table;

        // This assertion should FAIL on unfixed code:
        // The table is created without any GSIs because CreateTableAsync<TEntity>()
        // never consults metadata.Indexes
        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Table created for InventoryEntity should include 'StatusIndex' GSI, " +
            "but IntegrationTestBase.CreateTableAsync<TEntity>() never reads metadata.Indexes");

        var gsi = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        gsi.Should().NotBeNull(
            "Table should have 'StatusIndex' GSI provisioned");

        // Verify GSI key schema matches expected
        gsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_pk" && k.KeyType == KeyType.HASH,
            "StatusIndex should have 'gsi1_pk' as partition key");
        gsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_sk" && k.KeyType == KeyType.RANGE,
            "StatusIndex should have 'gsi1_sk' as sort key");
    }

    /// <summary>
    /// Bug 1 (LSI variant): IntegrationTestBase.CreateTableAsync&lt;TEntity&gt;() called with
    /// an entity that has LSI(s) declared via [LsiSortKey] — the created table should have LSIs.
    ///
    /// EXPECTED: This test FAILS on unfixed code because CreateTableAsync&lt;TEntity&gt;()
    /// never reads metadata.Indexes for LSI definitions either.
    ///
    /// Counterexample: DescribeTable returns null/empty LocalSecondaryIndexes despite
    /// LsiTestEntity declaring "OrderDateIndex" LSI with sort key "order_date".
    /// </summary>
    [Fact]
    public async Task Bug1_CreateTableAsync_WithLsiEntity_ShouldProvisionLsi()
    {
        // Arrange - LsiTestEntity declares LSI "OrderDateIndex" with [LsiSortKey("OrderDateIndex")]
        var metadata = LsiTestEntity.GetEntityMetadata();

        // Verify precondition: entity metadata declares LSI indexes
        metadata.Indexes.Should().NotBeEmpty(
            "LsiTestEntity should declare at least one LSI in its metadata");

        var lsiIndex = metadata.Indexes.FirstOrDefault(i =>
            i.IndexName == "OrderDateIndex" && i.IndexType == Oproto.FluentDynamoDb.Metadata.IndexType.LocalSecondaryIndex);
        lsiIndex.Should().NotBeNull(
            "LsiTestEntity should declare an 'OrderDateIndex' LSI");

        // Act - Create table using IntegrationTestBase.CreateTableAsync<TEntity>()
        await CreateTableAsync<LsiTestEntity>();

        // Assert - Verify the table has LSIs provisioned
        var describeResponse = await DynamoDb.DescribeTableAsync(TableName);
        var table = describeResponse.Table;

        // This assertion should FAIL on unfixed code:
        // The table is created without any LSIs because CreateTableAsync<TEntity>()
        // never consults metadata.Indexes
        table.LocalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Table created for LsiTestEntity should include 'OrderDateIndex' LSI, " +
            "but IntegrationTestBase.CreateTableAsync<TEntity>() never reads metadata.Indexes");

        var lsi = table.LocalSecondaryIndexes.FirstOrDefault(l => l.IndexName == "OrderDateIndex");
        lsi.Should().NotBeNull(
            "Table should have 'OrderDateIndex' LSI provisioned");

        // Verify LSI key schema: LSIs share the table's partition key
        lsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "pk" && k.KeyType == KeyType.HASH,
            "OrderDateIndex LSI should use table's partition key 'pk'");
        lsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "order_date" && k.KeyType == KeyType.RANGE,
            "OrderDateIndex LSI should have 'order_date' as sort key");
    }

    /// <summary>
    /// Bug 2: Multi-entity generated CreateTableAsync called when non-default entities
    /// declare GSIs — the created table only includes the default entity's GSIs.
    ///
    /// This test simulates the bug condition by calling TableCreator.CreateAsync() with
    /// only the default entity's metadata (which is what the generated
    /// GenerateCreateTableAsyncMethodForMultiEntity does), then asserting that indexes
    /// from the non-default entity are present. This demonstrates the bug because the
    /// generated code only passes defaultEntity.GetEntityMetadata() to TableCreator,
    /// omitting non-default entity indexes.
    ///
    /// EXPECTED: This test FAILS on unfixed code because the multi-entity CreateTableAsync
    /// only delegates to the single-entity method with the default entity.
    ///
    /// Counterexample: DescribeTable returns only GSIs from the default entity, missing
    /// "StatusIndex" from InventoryEntity (the non-default entity).
    /// </summary>
    [Fact]
    public async Task Bug2_MultiEntityCreateTable_WithNonDefaultEntityGsi_ShouldProvisionAllGsis()
    {
        // Arrange
        // Simulate the multi-entity table scenario:
        // - MultiEntityOrderTestEntity is the default entity (has NO GSIs)
        // - InventoryEntity is a non-default entity (has "StatusIndex" GSI)
        //
        // The generated GenerateCreateTableAsyncMethodForMultiEntity simply calls
        // GenerateCreateTableAsyncMethod(sb, defaultEntity) which generates code passing
        // only defaultEntity.GetEntityMetadata() to TableCreator.CreateAsync().
        // This means non-default entity GSIs are never provisioned.

        var defaultEntityMetadata = MultiEntityOrderTestEntity.GetEntityMetadata();
        var nonDefaultEntityMetadata = InventoryEntity.GetEntityMetadata();

        // Verify preconditions
        nonDefaultEntityMetadata.Indexes.Should().NotBeEmpty(
            "Non-default entity (InventoryEntity) should declare GSIs");
        
        // The generated multi-entity CreateTableAsync effectively does this:
        // TableCreator.CreateAsync(client, tableName, defaultEntity.GetEntityMetadata(), options)
        // This ONLY passes the default entity's metadata, omitting non-default entity indexes.
        var creator = new TableCreator();
        var tableName = $"test_multiEntityBug2_{Guid.NewGuid():N}";

        // Act - Simulate what the generated multi-entity CreateTableAsync does:
        // It only passes the default entity's metadata to TableCreator
        var result = await creator.CreateAsync(DynamoDb, tableName, defaultEntityMetadata,
            new TableCreationOptions { WaitForActive = true });

        try
        {
            // Assert - The table should contain GSIs from ALL entities (including non-default)
            var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
            var table = describeResponse.Table;

            // Collect all expected GSI names from all entities in the multi-entity table
            var expectedGsiNames = nonDefaultEntityMetadata.Indexes
                .Where(i => i.IndexType == Oproto.FluentDynamoDb.Metadata.IndexType.GlobalSecondaryIndex)
                .Select(i => i.IndexName)
                .ToList();

            // This assertion FAILS on unfixed code because the default entity
            // (MultiEntityOrderTestEntity) has no GSIs, so the table has no GSIs.
            // After the fix, the generated code should aggregate indexes from all entities.
            table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
                "Multi-entity table should include GSIs from non-default entities " +
                "(InventoryEntity declares 'StatusIndex'), but generated CreateTableAsync " +
                "only passes default entity metadata to TableCreator");

            foreach (var expectedGsiName in expectedGsiNames)
            {
                table.GlobalSecondaryIndexes.Should().Contain(g => g.IndexName == expectedGsiName,
                    $"Table should include GSI '{expectedGsiName}' from non-default entity");
            }
        }
        finally
        {
            // Cleanup
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
            }
            catch (ResourceNotFoundException)
            {
                // Already deleted
            }
        }
    }
}
