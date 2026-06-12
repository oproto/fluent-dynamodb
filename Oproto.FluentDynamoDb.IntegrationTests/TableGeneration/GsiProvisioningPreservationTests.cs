using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.IntegrationTests.TableGeneration;

/// <summary>
/// Preservation property tests for GSI/LSI table provisioning.
/// These tests establish a baseline of CORRECT behavior on UNFIXED code.
/// They must ALL PASS now and STILL pass after the fix is applied (confirming no regressions).
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Category", "PropertyTest")]
[Trait("Feature", "GsiProvisioningPreservation")]
public class GsiProvisioningPreservationTests : IntegrationTestBase
{
    private readonly List<string> _additionalTablesToCleanup = new();

    public GsiProvisioningPreservationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task DisposeAsync()
    {
        foreach (var tableName in _additionalTablesToCleanup)
        {
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
            }
            catch (ResourceNotFoundException)
            {
                // Already deleted
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Warning: Failed to delete table {tableName}: {ex.Message}");
            }
        }

        await base.DisposeAsync();
    }

    private string GenerateTableName(string suffix)
    {
        var tableName = $"test_preservation_{suffix}_{Guid.NewGuid():N}";
        _additionalTablesToCleanup.Add(tableName);
        return tableName;
    }

    #region Preservation Test 1: No-index entity via IntegrationTestBase

    /// <summary>
    /// Preservation: IntegrationTestBase.CreateTableAsync&lt;TEntity&gt;() for an entity with
    /// NO GSIs/LSIs creates a table with only partition key + sort key, PAY_PER_REQUEST billing,
    /// and no secondary indexes.
    ///
    /// This behavior must remain unchanged after the fix.
    ///
    /// **Validates: Requirements 3.1, 3.5**
    /// </summary>
    [Fact]
    public async Task Preservation_CreateTableAsync_NoIndexEntity_CreatesTableWithOnlyKeys()
    {
        // Arrange - BasicTestEntity has no GSI/LSI declarations
        var metadata = BasicTestEntity.GetEntityMetadata();

        // Verify precondition: entity has NO indexes
        metadata.Indexes.Should().BeEmpty(
            "BasicTestEntity should not declare any secondary indexes");

        // Act - Create table using IntegrationTestBase.CreateTableAsync<TEntity>()
        await CreateTableAsync<BasicTestEntity>();

        // Assert - Table should have only PK + SK, no secondary indexes
        var describeResponse = await DynamoDb.DescribeTableAsync(TableName);
        var table = describeResponse.Table;

        // Verify key schema: partition key + sort key
        table.KeySchema.Should().Contain(k =>
            k.AttributeName == "pk" && k.KeyType == KeyType.HASH,
            "Table should have 'pk' as partition key");
        table.KeySchema.Should().Contain(k =>
            k.AttributeName == "sk" && k.KeyType == KeyType.RANGE,
            "Table should have 'sk' as sort key");

        // Verify NO GSIs
        table.GlobalSecondaryIndexes.Should().BeNullOrEmpty(
            "Table for entity with no indexes should have no GSIs");

        // Verify NO LSIs
        table.LocalSecondaryIndexes.Should().BeNullOrEmpty(
            "Table for entity with no indexes should have no LSIs");

        // Verify PAY_PER_REQUEST billing mode
        table.BillingModeSummary?.BillingMode.Should().Be(BillingMode.PAY_PER_REQUEST,
            "Table should use PAY_PER_REQUEST billing mode");
    }

    #endregion

    #region Preservation Test 2: TableCreator.BuildCreateTableRequest with no indexes

    /// <summary>
    /// Preservation: TableCreator.BuildCreateTableRequest() with metadata that has an empty
    /// Indexes array produces a request with only partition key and optional sort key,
    /// no GlobalSecondaryIndexes, and no LocalSecondaryIndexes.
    ///
    /// **Validates: Requirements 3.1, 3.3**
    /// </summary>
    [Property(MaxTest = 20)]
    public Property Preservation_BuildCreateTableRequest_NoIndexes_ProducesNoSecondaryIndexes()
    {
        // Generate various entity metadata configurations with NO indexes
        var metadataGen = from pkName in Gen.Elements("pk", "partition_key", "id", "hash_key")
                          from pkType in Gen.Elements("S", "N")
                          from hasSortKey in Arb.Generate<bool>()
                          from skName in Gen.Elements("sk", "sort_key", "range_key")
                          from skType in Gen.Elements("S", "N")
                          select new EntityMetadata
                          {
                              TableName = "test-table",
                              PartitionKeyAttributeName = pkName,
                              PartitionKeyAttributeType = pkType,
                              SortKeyAttributeName = hasSortKey ? skName : null,
                              SortKeyAttributeType = hasSortKey ? skType : null,
                              Indexes = Array.Empty<IndexMetadata>(),
                              Properties = Array.Empty<PropertyMetadata>()
                          };

        return Prop.ForAll(metadataGen.ToArbitrary(), metadata =>
        {
            var creator = new TableCreator();
            var request = creator.BuildCreateTableRequest("test-table", metadata);

            // No GSIs should be present
            var noGsis = request.GlobalSecondaryIndexes == null || request.GlobalSecondaryIndexes.Count == 0;

            // No LSIs should be present
            var noLsis = request.LocalSecondaryIndexes == null || request.LocalSecondaryIndexes.Count == 0;

            // Key schema should match metadata
            var hasPk = request.KeySchema.Any(k =>
                k.AttributeName == metadata.PartitionKeyAttributeName && k.KeyType == KeyType.HASH);

            var hasSk = !string.IsNullOrEmpty(metadata.SortKeyAttributeName)
                ? request.KeySchema.Any(k =>
                    k.AttributeName == metadata.SortKeyAttributeName && k.KeyType == KeyType.RANGE)
                : request.KeySchema.Count == 1;

            // Billing mode should be PAY_PER_REQUEST by default
            var correctBilling = request.BillingMode == BillingMode.PAY_PER_REQUEST;

            return (noGsis && noLsis && hasPk && hasSk && correctBilling).ToProperty()
                .Label($"No-index metadata with PK={metadata.PartitionKeyAttributeName} " +
                       $"SK={metadata.SortKeyAttributeName ?? "(none)"}: " +
                       $"noGsis={noGsis}, noLsis={noLsis}, hasPk={hasPk}, hasSk={hasSk}, billing={correctBilling}");
        });
    }

    #endregion

    #region Preservation Test 3: TableCreator.BuildCreateTableRequest with GSI

    /// <summary>
    /// Preservation: TableCreator.BuildCreateTableRequest() with metadata containing a GSI
    /// produces a request that includes the GSI with correct key schema and attribute definitions.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void Preservation_BuildCreateTableRequest_WithGsi_IncludesGsiInRequest()
    {
        // Arrange - Use InventoryEntity's metadata which has "StatusIndex" GSI
        var metadata = InventoryEntity.GetEntityMetadata();
        var creator = new TableCreator();

        // Act
        var request = creator.BuildCreateTableRequest("test-table", metadata);

        // Assert - GSI should be present
        request.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Request built from metadata with GSI should include GlobalSecondaryIndexes");

        var gsi = request.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        gsi.Should().NotBeNull("StatusIndex GSI should be present in the request");

        // Verify GSI key schema
        gsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_pk" && k.KeyType == KeyType.HASH,
            "StatusIndex should have 'gsi1_pk' as partition key");
        gsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_sk" && k.KeyType == KeyType.RANGE,
            "StatusIndex should have 'gsi1_sk' as sort key");

        // Verify attribute definitions include GSI keys
        request.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_pk",
            "Attribute definitions should include GSI partition key");
        request.AttributeDefinitions.Should().Contain(a => a.AttributeName == "gsi1_sk",
            "Attribute definitions should include GSI sort key");
    }

    /// <summary>
    /// Preservation: TableCreator.BuildCreateTableRequest() with metadata containing an LSI
    /// produces a request that includes the LSI with correct key schema.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void Preservation_BuildCreateTableRequest_WithLsi_IncludesLsiInRequest()
    {
        // Arrange - Use LsiTestEntity's metadata which has "OrderDateIndex" LSI
        var metadata = LsiTestEntity.GetEntityMetadata();
        var creator = new TableCreator();

        // Act
        var request = creator.BuildCreateTableRequest("test-table", metadata);

        // Assert - LSI should be present
        request.LocalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Request built from metadata with LSI should include LocalSecondaryIndexes");

        var lsi = request.LocalSecondaryIndexes.FirstOrDefault(l => l.IndexName == "OrderDateIndex");
        lsi.Should().NotBeNull("OrderDateIndex LSI should be present in the request");

        // Verify LSI uses table's partition key
        lsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "pk" && k.KeyType == KeyType.HASH,
            "LSI should use table's partition key 'pk'");
        lsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "order_date" && k.KeyType == KeyType.RANGE,
            "LSI should have 'order_date' as sort key");
    }

    #endregion

    #region Preservation Test 4: TableCreator.CreateAsync with GSI entity

    /// <summary>
    /// Preservation: TableCreator.CreateAsync() called directly with entity metadata containing
    /// GSIs produces a table with all GSIs correctly provisioned.
    /// This behavior already works correctly and must remain unchanged.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public async Task Preservation_TableCreatorCreateAsync_WithGsiEntity_ProvisionesGsisCorrectly()
    {
        // Arrange
        var tableName = GenerateTableName("tablecreator_gsi");
        var creator = new TableCreator();
        var metadata = InventoryEntity.GetEntityMetadata();

        // Verify precondition
        metadata.Indexes.Should().NotBeEmpty(
            "InventoryEntity should declare indexes");

        // Act - Call TableCreator directly (this already works correctly)
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata,
            new TableCreationOptions { WaitForActive = true });

        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);

        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;

        // Verify GSI is present
        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "TableCreator.CreateAsync should provision GSIs from metadata");

        var gsi = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "StatusIndex");
        gsi.Should().NotBeNull("StatusIndex GSI should be provisioned");

        gsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_pk" && k.KeyType == KeyType.HASH);
        gsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_sk" && k.KeyType == KeyType.RANGE);
    }

    /// <summary>
    /// Preservation: TableCreator.CreateAsync() called directly with entity metadata containing
    /// LSIs produces a table with all LSIs correctly provisioned.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public async Task Preservation_TableCreatorCreateAsync_WithLsiEntity_ProvisionesLsisCorrectly()
    {
        // Arrange
        var tableName = GenerateTableName("tablecreator_lsi");
        var creator = new TableCreator();
        var metadata = LsiTestEntity.GetEntityMetadata();

        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata,
            new TableCreationOptions { WaitForActive = true });

        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);

        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;

        // Verify LSI is present
        table.LocalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "TableCreator.CreateAsync should provision LSIs from metadata");

        var lsi = table.LocalSecondaryIndexes.FirstOrDefault(l => l.IndexName == "OrderDateIndex");
        lsi.Should().NotBeNull("OrderDateIndex LSI should be provisioned");

        lsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "pk" && k.KeyType == KeyType.HASH);
        lsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "order_date" && k.KeyType == KeyType.RANGE);
    }

    #endregion

    #region Preservation Test 5: CreateTableWithGsiAsync separate code path

    /// <summary>
    /// Preservation: CreateTableWithGsiAsync&lt;TEntity&gt;() in IntegrationTestBase creates
    /// tables with GSIs correctly. This is a separate code path that must remain unaffected.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Fact]
    public async Task Preservation_CreateTableWithGsiAsync_ProvisionesGsiCorrectly()
    {
        // Arrange & Act - Use the separate CreateTableWithGsiAsync path
        await CreateTableWithGsiAsync<InventoryEntity>(
            gsiName: "ManualStatusIndex",
            gsiPartitionKeyAttribute: "gsi1_pk",
            gsiSortKeyAttribute: "gsi1_sk");

        // Assert - Verify the table has the GSI
        var describeResponse = await DynamoDb.DescribeTableAsync(TableName);
        var table = describeResponse.Table;

        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "CreateTableWithGsiAsync should provision GSIs");

        var gsi = table.GlobalSecondaryIndexes.FirstOrDefault(g => g.IndexName == "ManualStatusIndex");
        gsi.Should().NotBeNull("ManualStatusIndex GSI should be provisioned");

        gsi!.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_pk" && k.KeyType == KeyType.HASH);
        gsi.KeySchema.Should().Contain(k =>
            k.AttributeName == "gsi1_sk" && k.KeyType == KeyType.RANGE);

        // Verify table also has correct primary key
        table.KeySchema.Should().Contain(k =>
            k.AttributeName == "pk" && k.KeyType == KeyType.HASH);
        table.KeySchema.Should().Contain(k =>
            k.AttributeName == "sk" && k.KeyType == KeyType.RANGE);
    }

    #endregion

    #region Preservation Test 6: Multi-entity where only default entity has GSIs

    /// <summary>
    /// Preservation: Multi-entity table where only the default entity declares GSIs
    /// produces a table with those GSIs. This already works correctly because the
    /// generated CreateTableAsync uses defaultEntity.GetEntityMetadata() which includes
    /// the default entity's indexes.
    ///
    /// For this test, we simulate by calling TableCreator.CreateAsync() with the default
    /// entity's metadata (which has GSIs), verifying the table includes those GSIs.
    /// This is exactly what the generated code does today.
    ///
    /// **Validates: Requirements 3.2, 3.4**
    /// </summary>
    [Fact]
    public async Task Preservation_MultiEntity_DefaultEntityWithGsi_ProvisionesDefaultGsis()
    {
        // Arrange
        // Simulate: default entity has GSIs, non-default entities have no GSIs
        // This scenario already works because generated code passes default entity's metadata
        var tableName = GenerateTableName("multi_default_gsi");
        var creator = new TableCreator();

        // InventoryEntity has "StatusIndex" GSI - simulate it being the default entity
        var defaultEntityMetadata = InventoryEntity.GetEntityMetadata();

        // Act - This is what the generated multi-entity CreateTableAsync does today
        // when only the default entity has GSIs
        var result = await creator.CreateAsync(DynamoDb, tableName, defaultEntityMetadata,
            new TableCreationOptions { WaitForActive = true });

        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);

        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;

        // GSIs from the default entity should be present
        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty(
            "Multi-entity table where default entity has GSIs should include those GSIs");

        table.GlobalSecondaryIndexes.Should().Contain(g => g.IndexName == "StatusIndex",
            "Default entity's 'StatusIndex' GSI should be provisioned");
    }

    #endregion

    #region Preservation Test 7: Property-based test for BuildCreateTableRequest correctness

    /// <summary>
    /// Preservation: For various entity metadata configurations with GSIs,
    /// TableCreator.BuildCreateTableRequest() always includes all declared indexes
    /// in the output request.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property Preservation_BuildCreateTableRequest_AlwaysIncludesAllDeclaredIndexes()
    {
        // Generate metadata with varying numbers of GSIs
        var gsiGen = from indexName in Gen.Elements("gsi1", "gsi2", "gsi3", "EmailIndex", "StatusIndex")
                     from pkName in Gen.Elements("gsi_pk", "email", "status", "category")
                     from pkType in Gen.Constant("S")
                     from hasSk in Arb.Generate<bool>()
                     from skName in Gen.Elements("gsi_sk", "created_at", "updated_at")
                     from skType in Gen.Constant("S")
                     select new IndexMetadata
                     {
                         IndexName = indexName,
                         IndexType = IndexType.GlobalSecondaryIndex,
                         PartitionKeyAttributeName = pkName,
                         PartitionKeyAttributeType = pkType,
                         SortKeyAttributeName = hasSk ? skName : null,
                         SortKeyAttributeType = hasSk ? skType : null,
                         ProjectionType = Metadata.ProjectionType.All,
                         ProjectedProperties = Array.Empty<string>()
                     };

        var metadataGen = from gsiCount in Gen.Choose(1, 3)
                          from gsis in Gen.ListOf(gsiCount, gsiGen)
                          let uniqueGsis = gsis.GroupBy(g => g.IndexName).Select(g => g.First()).ToArray()
                          select new EntityMetadata
                          {
                              TableName = "test-table",
                              PartitionKeyAttributeName = "pk",
                              PartitionKeyAttributeType = "S",
                              SortKeyAttributeName = "sk",
                              SortKeyAttributeType = "S",
                              Indexes = uniqueGsis,
                              Properties = Array.Empty<PropertyMetadata>()
                          };

        return Prop.ForAll(metadataGen.ToArbitrary(), metadata =>
        {
            var creator = new TableCreator();
            var request = creator.BuildCreateTableRequest("test-table", metadata);

            // Every declared index should appear in the request
            var allGsisPresent = metadata.Indexes
                .Where(i => i.IndexType == IndexType.GlobalSecondaryIndex)
                .All(expectedGsi =>
                    request.GlobalSecondaryIndexes != null &&
                    request.GlobalSecondaryIndexes.Any(g => g.IndexName == expectedGsi.IndexName));

            // All GSI key attributes should be in attribute definitions
            var allAttrDefsPresent = metadata.Indexes
                .Where(i => i.IndexType == IndexType.GlobalSecondaryIndex)
                .All(gsi =>
                    request.AttributeDefinitions.Any(a => a.AttributeName == gsi.PartitionKeyAttributeName) &&
                    (string.IsNullOrEmpty(gsi.SortKeyAttributeName) ||
                     request.AttributeDefinitions.Any(a => a.AttributeName == gsi.SortKeyAttributeName)));

            return (allGsisPresent && allAttrDefsPresent).ToProperty()
                .Label($"Metadata with {metadata.Indexes.Length} GSI(s): " +
                       $"allGsisPresent={allGsisPresent}, allAttrDefsPresent={allAttrDefsPresent}");
        });
    }

    #endregion

    #region Preservation Test 8: TableCreator with comprehensive metadata (GSI + LSI + TTL)

    /// <summary>
    /// Preservation: TableCreator.CreateAsync() with comprehensive metadata (GSI, LSI, TTL)
    /// produces tables with all features correctly provisioned.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public async Task Preservation_TableCreatorCreateAsync_ComprehensiveMetadata_AllFeaturesProvisioned()
    {
        // Arrange - SchemaValidationTestEntity has GSI, LSI, and TTL
        var tableName = GenerateTableName("comprehensive");
        var creator = new TableCreator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();

        // Verify preconditions
        metadata.Indexes.Should().NotBeEmpty("SchemaValidationTestEntity should have indexes");
        metadata.TtlAttributeName.Should().NotBeNullOrEmpty(
            "SchemaValidationTestEntity should have TTL configured");

        // Act
        var result = await creator.CreateAsync(DynamoDb, tableName, metadata,
            new TableCreationOptions { WaitForActive = true, EnableTtl = true });

        // Assert
        result.TableStatus.Should().Be(TableStatus.ACTIVE);
        result.TtlEnabled.Should().BeTrue();

        var describeResponse = await DynamoDb.DescribeTableAsync(tableName);
        var table = describeResponse.Table;

        // Verify GSI
        table.GlobalSecondaryIndexes.Should().NotBeNullOrEmpty();
        table.GlobalSecondaryIndexes.Should().Contain(g => g.IndexName == "StatusIndex");

        // Verify LSI
        table.LocalSecondaryIndexes.Should().NotBeNullOrEmpty();
        table.LocalSecondaryIndexes.Should().Contain(l => l.IndexName == "CategoryIndex");

        // Verify TTL
        var ttlResponse = await DynamoDb.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
        {
            TableName = tableName
        });
        ttlResponse.TimeToLiveDescription.TimeToLiveStatus.Should().Be(TimeToLiveStatus.ENABLED);
        ttlResponse.TimeToLiveDescription.AttributeName.Should().Be("ttl");
    }

    #endregion
}
