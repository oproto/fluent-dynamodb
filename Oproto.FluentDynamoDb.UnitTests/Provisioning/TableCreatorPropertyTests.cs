using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Provisioning;

namespace Oproto.FluentDynamoDb.UnitTests.Provisioning;

/// <summary>
/// Property-based tests for TableCreator.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class TableCreatorPropertyTests
{
    private static readonly string[] ValidAttributeTypes = { "S", "N", "B" };
    private readonly TableCreator _tableCreator = new();

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 1: Primary key schema round-trip**
    /// 
    /// For any valid EntityMetadata with partition key (and optional sort key), 
    /// the generated CreateTableRequest SHALL have a KeySchema that matches 
    /// the metadata's key configuration exactly.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryKeySchema_MatchesMetadata()
    {
        return Prop.ForAll(
            PrimaryKeyMetadataArb(),
            input =>
            {
                // Arrange
                var metadata = new EntityMetadata
                {
                    TableName = input.TableName,
                    PartitionKeyAttributeName = input.PkName,
                    PartitionKeyAttributeType = input.PkType,
                    SortKeyAttributeName = input.SkName,
                    SortKeyAttributeType = input.SkName != null ? input.SkType : null
                };

                // Act
                var request = _tableCreator.BuildCreateTableRequest(input.TableName, metadata);

                // Assert - Partition key
                var hashKey = request.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
                var pkNameMatches = hashKey?.AttributeName == input.PkName;

                // Assert - Sort key
                var rangeKey = request.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);
                var skMatches = input.SkName == null
                    ? rangeKey == null
                    : rangeKey?.AttributeName == input.SkName;

                // Assert - Attribute definitions contain correct types
                var pkAttrDef = request.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == input.PkName);
                var pkTypeMatches = pkAttrDef?.AttributeType.Value == input.PkType;

                var skTypeMatches = input.SkName == null || 
                    request.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == input.SkName)?.AttributeType.Value == input.SkType;

                return (pkNameMatches && skMatches && pkTypeMatches && skTypeMatches).ToProperty()
                    .Label($"PK: {input.PkName}={hashKey?.AttributeName}, " +
                           $"SK: {input.SkName ?? "null"}={rangeKey?.AttributeName ?? "null"}, " +
                           $"PKType: {input.PkType}={pkAttrDef?.AttributeType.Value}, " +
                           $"SKType: {input.SkType ?? "null"}");
            });
    }

    /// <summary>
    /// Input record for primary key metadata tests.
    /// </summary>
    private record PrimaryKeyMetadataInput(
        string TableName, 
        string PkName, 
        string PkType, 
        string? SkName, 
        string? SkType);

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 2: GSI configuration preservation**
    /// 
    /// For any EntityMetadata with GSI definitions, the generated CreateTableRequest 
    /// SHALL contain GlobalSecondaryIndexes with matching index names, key schemas, 
    /// and projection configurations.
    /// 
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiConfiguration_IsPreserved()
    {
        return Prop.ForAll(
            GsiMetadataArb(),
            input =>
            {
                // Arrange
                var metadata = new EntityMetadata
                {
                    TableName = input.TableName,
                    PartitionKeyAttributeName = "pk",
                    PartitionKeyAttributeType = "S",
                    Indexes = input.Gsis.Select(g => new IndexMetadata
                    {
                        IndexName = g.IndexName,
                        IndexType = IndexType.GlobalSecondaryIndex,
                        PartitionKeyAttributeName = g.PkName,
                        PartitionKeyAttributeType = g.PkType,
                        SortKeyAttributeName = g.SkName,
                        SortKeyAttributeType = g.SkType,
                        ProjectionType = g.ProjectionType,
                        ProjectedProperties = g.ProjectedProperties
                    }).ToArray()
                };

                // Act
                var request = _tableCreator.BuildCreateTableRequest(input.TableName, metadata);

                // Assert - All GSIs are present
                var allGsisPresent = input.Gsis.All(expectedGsi =>
                    request.GlobalSecondaryIndexes?.Any(actualGsi => 
                        actualGsi.IndexName == expectedGsi.IndexName) ?? false);

                // Assert - GSI key schemas match
                var allKeySchemaMatch = input.Gsis.All(expectedGsi =>
                {
                    var actualGsi = request.GlobalSecondaryIndexes?.FirstOrDefault(g => g.IndexName == expectedGsi.IndexName);
                    if (actualGsi == null) return false;

                    var pkMatches = actualGsi.KeySchema.Any(k => 
                        k.KeyType == KeyType.HASH && k.AttributeName == expectedGsi.PkName);
                    
                    var skMatches = expectedGsi.SkName == null
                        ? !actualGsi.KeySchema.Any(k => k.KeyType == KeyType.RANGE)
                        : actualGsi.KeySchema.Any(k => k.KeyType == KeyType.RANGE && k.AttributeName == expectedGsi.SkName);

                    return pkMatches && skMatches;
                });

                // Assert - Projection types match
                var allProjectionsMatch = input.Gsis.All(expectedGsi =>
                {
                    var actualGsi = request.GlobalSecondaryIndexes?.FirstOrDefault(g => g.IndexName == expectedGsi.IndexName);
                    if (actualGsi == null) return false;

                    var expectedProjectionType = expectedGsi.ProjectionType switch
                    {
                        Metadata.ProjectionType.All => Amazon.DynamoDBv2.ProjectionType.ALL,
                        Metadata.ProjectionType.KeysOnly => Amazon.DynamoDBv2.ProjectionType.KEYS_ONLY,
                        Metadata.ProjectionType.Include => Amazon.DynamoDBv2.ProjectionType.INCLUDE,
                        _ => Amazon.DynamoDBv2.ProjectionType.ALL
                    };

                    return actualGsi.Projection.ProjectionType == expectedProjectionType;
                });

                return (allGsisPresent && allKeySchemaMatch && allProjectionsMatch).ToProperty()
                    .Label($"GSIs: {input.Gsis.Length}, AllPresent: {allGsisPresent}, " +
                           $"KeySchemaMatch: {allKeySchemaMatch}, ProjectionsMatch: {allProjectionsMatch}");
            });
    }

    /// <summary>
    /// Input record for GSI metadata tests.
    /// </summary>
    private record GsiMetadataInput(string TableName, GsiInput[] Gsis);
    
    private record GsiInput(
        string IndexName, 
        string PkName, 
        string PkType, 
        string? SkName, 
        string? SkType,
        Metadata.ProjectionType ProjectionType,
        string[] ProjectedProperties);

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 3: LSI configuration preservation**
    /// 
    /// For any EntityMetadata with LSI definitions, the generated CreateTableRequest 
    /// SHALL contain LocalSecondaryIndexes with the table's partition key, the LSI's 
    /// sort key, and matching projection configurations.
    /// 
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiConfiguration_IsPreserved()
    {
        return Prop.ForAll(
            LsiMetadataArb(),
            input =>
            {
                // Arrange
                var metadata = new EntityMetadata
                {
                    TableName = input.TableName,
                    PartitionKeyAttributeName = input.TablePkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = "table_sk",
                    SortKeyAttributeType = "S",
                    Indexes = input.Lsis.Select(l => new IndexMetadata
                    {
                        IndexName = l.IndexName,
                        IndexType = IndexType.LocalSecondaryIndex,
                        PartitionKeyAttributeName = input.TablePkName, // LSI uses table's PK
                        PartitionKeyAttributeType = "S",
                        SortKeyAttributeName = l.SkName,
                        SortKeyAttributeType = l.SkType,
                        ProjectionType = l.ProjectionType,
                        ProjectedProperties = l.ProjectedProperties
                    }).ToArray()
                };

                // Act
                var request = _tableCreator.BuildCreateTableRequest(input.TableName, metadata);

                // Assert - All LSIs are present
                var allLsisPresent = input.Lsis.All(expectedLsi =>
                    request.LocalSecondaryIndexes?.Any(actualLsi => 
                        actualLsi.IndexName == expectedLsi.IndexName) ?? false);

                // Assert - LSI key schemas use table's partition key
                var allUseTablePk = input.Lsis.All(expectedLsi =>
                {
                    var actualLsi = request.LocalSecondaryIndexes?.FirstOrDefault(l => l.IndexName == expectedLsi.IndexName);
                    if (actualLsi == null) return false;

                    return actualLsi.KeySchema.Any(k => 
                        k.KeyType == KeyType.HASH && k.AttributeName == input.TablePkName);
                });

                // Assert - LSI sort keys match
                var allSkMatch = input.Lsis.All(expectedLsi =>
                {
                    var actualLsi = request.LocalSecondaryIndexes?.FirstOrDefault(l => l.IndexName == expectedLsi.IndexName);
                    if (actualLsi == null) return false;

                    return actualLsi.KeySchema.Any(k => 
                        k.KeyType == KeyType.RANGE && k.AttributeName == expectedLsi.SkName);
                });

                // Assert - Projection types match
                var allProjectionsMatch = input.Lsis.All(expectedLsi =>
                {
                    var actualLsi = request.LocalSecondaryIndexes?.FirstOrDefault(l => l.IndexName == expectedLsi.IndexName);
                    if (actualLsi == null) return false;

                    var expectedProjectionType = expectedLsi.ProjectionType switch
                    {
                        Metadata.ProjectionType.All => Amazon.DynamoDBv2.ProjectionType.ALL,
                        Metadata.ProjectionType.KeysOnly => Amazon.DynamoDBv2.ProjectionType.KEYS_ONLY,
                        Metadata.ProjectionType.Include => Amazon.DynamoDBv2.ProjectionType.INCLUDE,
                        _ => Amazon.DynamoDBv2.ProjectionType.ALL
                    };

                    return actualLsi.Projection.ProjectionType == expectedProjectionType;
                });

                return (allLsisPresent && allUseTablePk && allSkMatch && allProjectionsMatch).ToProperty()
                    .Label($"LSIs: {input.Lsis.Length}, AllPresent: {allLsisPresent}, " +
                           $"UseTablePK: {allUseTablePk}, SKMatch: {allSkMatch}, ProjectionsMatch: {allProjectionsMatch}");
            });
    }

    /// <summary>
    /// Input record for LSI metadata tests.
    /// </summary>
    private record LsiMetadataInput(string TableName, string TablePkName, LsiInput[] Lsis);
    
    private record LsiInput(
        string IndexName, 
        string SkName, 
        string SkType,
        Metadata.ProjectionType ProjectionType,
        string[] ProjectedProperties);

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 4: Attribute definitions completeness**
    /// 
    /// For any EntityMetadata, the generated CreateTableRequest SHALL have AttributeDefinitions 
    /// containing all unique key attributes from the table and all indexes with correct types.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 2.2, 2.3, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AttributeDefinitions_ContainsAllKeyAttributes()
    {
        return Prop.ForAll(
            CompleteMetadataArb(),
            input =>
            {
                // Arrange
                var metadata = input.Metadata;

                // Act
                var request = _tableCreator.BuildCreateTableRequest(input.TableName, metadata);

                // Collect all expected key attributes
                var expectedAttributes = new Dictionary<string, string>();
                
                // Table keys
                expectedAttributes[metadata.PartitionKeyAttributeName] = metadata.PartitionKeyAttributeType;
                if (!string.IsNullOrEmpty(metadata.SortKeyAttributeName))
                {
                    expectedAttributes[metadata.SortKeyAttributeName] = metadata.SortKeyAttributeType!;
                }
                
                // Index keys
                foreach (var index in metadata.Indexes)
                {
                    if (index.IndexType == IndexType.GlobalSecondaryIndex)
                    {
                        expectedAttributes[index.PartitionKeyAttributeName] = index.PartitionKeyAttributeType;
                    }
                    if (!string.IsNullOrEmpty(index.SortKeyAttributeName))
                    {
                        expectedAttributes[index.SortKeyAttributeName] = index.SortKeyAttributeType!;
                    }
                }

                // Assert - All expected attributes are present with correct types
                var allAttributesPresent = expectedAttributes.All(expected =>
                {
                    var actual = request.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == expected.Key);
                    return actual != null && actual.AttributeType.Value == expected.Value;
                });

                // Assert - No extra attributes (only key attributes should be defined)
                var noExtraAttributes = request.AttributeDefinitions.All(actual =>
                    expectedAttributes.ContainsKey(actual.AttributeName));

                return (allAttributesPresent && noExtraAttributes).ToProperty()
                    .Label($"Expected: {expectedAttributes.Count} attrs, " +
                           $"Actual: {request.AttributeDefinitions.Count} attrs, " +
                           $"AllPresent: {allAttributesPresent}, NoExtra: {noExtraAttributes}");
            });
    }

    /// <summary>
    /// Input record for complete metadata tests.
    /// </summary>
    private record CompleteMetadataInput(string TableName, EntityMetadata Metadata);

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 5: Provisioned throughput configuration**
    /// 
    /// For any TableCreationOptions with PROVISIONED billing mode and specified throughput values, 
    /// the generated CreateTableRequest SHALL have matching ProvisionedThroughput values.
    /// 
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProvisionedThroughput_IsConfiguredCorrectly()
    {
        return Prop.ForAll(
            ProvisionedThroughputArb(),
            input =>
            {
                // Arrange
                var metadata = new EntityMetadata
                {
                    TableName = input.TableName,
                    PartitionKeyAttributeName = "pk",
                    PartitionKeyAttributeType = "S",
                    Indexes = input.GsiCount > 0 
                        ? Enumerable.Range(0, input.GsiCount).Select(i => new IndexMetadata
                        {
                            IndexName = $"gsi{i}",
                            IndexType = IndexType.GlobalSecondaryIndex,
                            PartitionKeyAttributeName = $"gsi{i}_pk",
                            PartitionKeyAttributeType = "S"
                        }).ToArray()
                        : Array.Empty<IndexMetadata>()
                };

                var options = new TableCreationOptions
                {
                    BillingMode = BillingMode.PROVISIONED,
                    ProvisionedThroughput = new ProvisionedThroughputConfig
                    {
                        ReadCapacityUnits = input.ReadCapacity,
                        WriteCapacityUnits = input.WriteCapacity
                    }
                };

                // Act
                var request = _tableCreator.BuildCreateTableRequest(input.TableName, metadata, options);

                // Assert - Table throughput matches
                var tableRcuMatches = request.ProvisionedThroughput?.ReadCapacityUnits == input.ReadCapacity;
                var tableWcuMatches = request.ProvisionedThroughput?.WriteCapacityUnits == input.WriteCapacity;

                // Assert - GSI throughput matches (uses table throughput by default)
                var gsiThroughputMatches = request.GlobalSecondaryIndexes?.All(gsi =>
                    gsi.ProvisionedThroughput?.ReadCapacityUnits == input.ReadCapacity &&
                    gsi.ProvisionedThroughput?.WriteCapacityUnits == input.WriteCapacity) ?? true;

                return (tableRcuMatches && tableWcuMatches && gsiThroughputMatches).ToProperty()
                    .Label($"RCU: {input.ReadCapacity}, WCU: {input.WriteCapacity}, " +
                           $"TableRCU: {tableRcuMatches}, TableWCU: {tableWcuMatches}, " +
                           $"GSIThroughput: {gsiThroughputMatches}");
            });
    }

    /// <summary>
    /// Input record for provisioned throughput tests.
    /// </summary>
    private record ProvisionedThroughputInput(string TableName, long ReadCapacity, long WriteCapacity, int GsiCount);

    /// <summary>
    /// **Feature: table-creation-from-metadata, Property 6: Table name is used in request**
    /// 
    /// For any table name provided to BuildCreateTableRequest, the generated CreateTableRequest 
    /// SHALL use that exact table name.
    /// 
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TableName_IsUsedInRequest()
    {
        return Prop.ForAll(
            TableNameArb(),
            tableName =>
            {
                // Arrange
                var metadata = new EntityMetadata
                {
                    TableName = "different-table-name", // Intentionally different to verify tableName parameter is used
                    PartitionKeyAttributeName = "pk",
                    PartitionKeyAttributeType = "S"
                };

                // Act
                var request = _tableCreator.BuildCreateTableRequest(tableName, metadata);

                // Assert - The request uses the provided table name, not the metadata's table name
                var tableNameMatches = request.TableName == tableName;

                return tableNameMatches.ToProperty()
                    .Label($"Expected: {tableName}, Actual: {request.TableName}");
            });
    }

    #region Arbitraries

    private static Arbitrary<PrimaryKeyMetadataInput> PrimaryKeyMetadataArb()
    {
        var tableNameGen = Gen.Elements("TestTable", "MyTable", "Users", "Orders", "Products")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..8]);
        
        var attrNameGen = Gen.Elements("pk", "sk", "id", "userId", "orderId", "timestamp", "data")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var attrTypeGen = Gen.Elements(ValidAttributeTypes);
        
        var optionalSkGen = Gen.OneOf(
            Gen.Constant<(string?, string?)>((null, null)),
            attrNameGen.SelectMany(name => attrTypeGen.Select(type => ((string?)name, (string?)type)))
        );
        
        return (from tableName in tableNameGen
                from pkName in attrNameGen
                from pkType in attrTypeGen
                from sk in optionalSkGen
                select new PrimaryKeyMetadataInput(tableName, pkName, pkType, sk.Item1, sk.Item2))
            .ToArbitrary();
    }

    private static Arbitrary<GsiMetadataInput> GsiMetadataArb()
    {
        var tableNameGen = Gen.Elements("TestTable", "MyTable", "Users", "Orders", "Products")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..8]);
        
        var gsiGen = GsiInputGen();
        
        // Generate 0-5 GSIs
        var gsisGen = Gen.Choose(0, 5).SelectMany(count => 
            Gen.ArrayOf(count, gsiGen));
        
        return (from tableName in tableNameGen
                from gsis in gsisGen
                select new GsiMetadataInput(tableName, gsis))
            .ToArbitrary();
    }

    private static Gen<GsiInput> GsiInputGen()
    {
        var indexNameGen = Gen.Elements("gsi1", "gsi2", "gsi3", "gsi4", "gsi5")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var attrNameGen = Gen.Elements("gsi_pk", "gsi_sk", "category", "status", "date", "type")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var attrTypeGen = Gen.Elements(ValidAttributeTypes);
        
        var optionalSkGen = Gen.OneOf(
            Gen.Constant<(string?, string?)>((null, null)),
            attrNameGen.SelectMany(name => attrTypeGen.Select(type => ((string?)name, (string?)type)))
        );
        
        var projectionTypeGen = Gen.Elements(
            Metadata.ProjectionType.All, 
            Metadata.ProjectionType.KeysOnly, 
            Metadata.ProjectionType.Include);
        
        var projectedPropsGen = Gen.Choose(0, 3).SelectMany(count =>
            Gen.ArrayOf(count, Gen.Elements("attr1", "attr2", "attr3", "attr4")));
        
        return from indexName in indexNameGen
               from pkName in attrNameGen
               from pkType in attrTypeGen
               from sk in optionalSkGen
               from projType in projectionTypeGen
               from projProps in projectedPropsGen
               select new GsiInput(
                   indexName, 
                   pkName, 
                   pkType, 
                   sk.Item1, 
                   sk.Item2, 
                   projType,
                   projType == Metadata.ProjectionType.Include ? projProps : Array.Empty<string>());
    }

    private static Arbitrary<ProvisionedThroughputInput> ProvisionedThroughputArb()
    {
        var tableNameGen = Gen.Elements("TestTable", "MyTable", "Users", "Orders", "Products")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..8]);
        
        // DynamoDB allows 1-40000 for provisioned capacity
        var capacityGen = Gen.Choose(1, 1000).Select(i => (long)i);
        var gsiCountGen = Gen.Choose(0, 3);
        
        return (from tableName in tableNameGen
                from readCapacity in capacityGen
                from writeCapacity in capacityGen
                from gsiCount in gsiCountGen
                select new ProvisionedThroughputInput(tableName, readCapacity, writeCapacity, gsiCount))
            .ToArbitrary();
    }

    private static Arbitrary<CompleteMetadataInput> CompleteMetadataArb()
    {
        var tableNameGen = Gen.Elements("TestTable", "MyTable", "Users", "Orders", "Products")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..8]);
        
        var attrNameGen = Gen.Elements("pk", "sk", "id", "userId", "orderId")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var attrTypeGen = Gen.Elements(ValidAttributeTypes);
        
        var optionalSkGen = Gen.OneOf(
            Gen.Constant<(string?, string?)>((null, null)),
            attrNameGen.SelectMany(name => attrTypeGen.Select(type => ((string?)name, (string?)type)))
        );
        
        var gsiGen = GsiInputGen();
        var lsiGen = LsiInputGen();
        
        var gsisGen = Gen.Choose(0, 3).SelectMany(count => Gen.ArrayOf(count, gsiGen));
        var lsisGen = Gen.Choose(0, 3).SelectMany(count => Gen.ArrayOf(count, lsiGen));
        
        return (from tableName in tableNameGen
                from pkName in attrNameGen
                from pkType in attrTypeGen
                from sk in optionalSkGen
                from gsis in gsisGen
                from lsis in lsisGen
                let indexes = gsis.Select(g => new IndexMetadata
                {
                    IndexName = g.IndexName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyAttributeName = g.PkName,
                    PartitionKeyAttributeType = g.PkType,
                    SortKeyAttributeName = g.SkName,
                    SortKeyAttributeType = g.SkType,
                    ProjectionType = g.ProjectionType,
                    ProjectedProperties = g.ProjectedProperties
                }).Concat(lsis.Select(l => new IndexMetadata
                {
                    IndexName = l.IndexName,
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = pkType,
                    SortKeyAttributeName = l.SkName,
                    SortKeyAttributeType = l.SkType,
                    ProjectionType = l.ProjectionType,
                    ProjectedProperties = l.ProjectedProperties
                })).ToArray()
                select new CompleteMetadataInput(tableName, new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = pkType,
                    SortKeyAttributeName = sk.Item1,
                    SortKeyAttributeType = sk.Item2,
                    Indexes = indexes
                }))
            .ToArbitrary();
    }

    private static Arbitrary<LsiMetadataInput> LsiMetadataArb()
    {
        var tableNameGen = Gen.Elements("TestTable", "MyTable", "Users", "Orders", "Products")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..8]);
        
        var tablePkNameGen = Gen.Elements("pk", "partition_key", "id")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var lsiGen = LsiInputGen();
        
        // Generate 0-5 LSIs
        var lsisGen = Gen.Choose(0, 5).SelectMany(count => 
            Gen.ArrayOf(count, lsiGen));
        
        return (from tableName in tableNameGen
                from tablePkName in tablePkNameGen
                from lsis in lsisGen
                select new LsiMetadataInput(tableName, tablePkName, lsis))
            .ToArbitrary();
    }

    private static Gen<LsiInput> LsiInputGen()
    {
        var indexNameGen = Gen.Elements("lsi1", "lsi2", "lsi3", "lsi4", "lsi5")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var skNameGen = Gen.Elements("lsi_sk", "created_at", "updated_at", "status", "type")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
        
        var attrTypeGen = Gen.Elements(ValidAttributeTypes);
        
        var projectionTypeGen = Gen.Elements(
            Metadata.ProjectionType.All, 
            Metadata.ProjectionType.KeysOnly, 
            Metadata.ProjectionType.Include);
        
        var projectedPropsGen = Gen.Choose(0, 3).SelectMany(count =>
            Gen.ArrayOf(count, Gen.Elements("attr1", "attr2", "attr3", "attr4")));
        
        return from indexName in indexNameGen
               from skName in skNameGen
               from skType in attrTypeGen
               from projType in projectionTypeGen
               from projProps in projectedPropsGen
               select new LsiInput(
                   indexName, 
                   skName, 
                   skType, 
                   projType,
                   projType == Metadata.ProjectionType.Include ? projProps : Array.Empty<string>());
    }

    private static Gen<string> ValidAttributeNameGen()
    {
        return Gen.Elements("pk", "sk", "gsi1pk", "gsi1sk", "lsi1sk", "id", "userId", "orderId", "timestamp", "data", "status")
            .Select(s => s + "_" + Guid.NewGuid().ToString("N")[..4]);
    }

    private static Gen<string> ValidAttributeTypeGen()
    {
        return Gen.Elements(ValidAttributeTypes);
    }

    private static Arbitrary<string> TableNameArb()
    {
        // Generate valid DynamoDB table names:
        // - 3-255 characters
        // - Alphanumeric, hyphens, underscores, periods
        var prefixGen = Gen.Elements(
            "TestTable", "MyTable", "Users", "Orders", "Products", 
            "Inventory", "Customers", "Transactions", "Events", "Logs");
        
        var suffixGen = Gen.Elements(
            "-dev", "-test", "-prod", "_v1", "_v2", ".main", ".backup",
            "-integration", "-unit", "_staging");
        
        var uniqueIdGen = Gen.Choose(1000, 9999).Select(i => i.ToString());
        
        return (from prefix in prefixGen
                from suffix in Gen.OneOf(Gen.Constant(""), suffixGen)
                from uniqueId in uniqueIdGen
                select $"{prefix}{suffix}_{uniqueId}")
            .ToArbitrary();
    }

    #endregion
}
