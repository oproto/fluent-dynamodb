using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

public class MapperGeneratorTests
{
    [Fact]
    public void GenerateEntityImplementation_WithBasicEntity_ProducesCorrectCode()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("namespace TestNamespace");
        result.Should().Contain("public partial class TestEntity : IDynamoDbEntity");
        result.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)");
        result.Should().Contain("public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item)");
        result.Should().Contain("public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items)");
        result.Should().Contain("public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
        result.Should().Contain("public static bool MatchesEntity(Dictionary<string, AttributeValue> item)");
        result.Should().Contain("public static EntityMetadata GetEntityMetadata()");
    }

    [Fact]
    public void GenerateEntityImplementation_WithMultiItemEntity_GeneratesMultiItemMethods()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TransactionEntity",
            Namespace = "TestNamespace",
            TableName = "transactions",
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "TransactionId",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "LedgerEntries",
                    AttributeName = "ledger_entries",
                    PropertyType = "List<LedgerEntry>",
                    IsCollection = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("Multi-item entity: Supports entities that span multiple DynamoDB items.");
        result.Should().Contain("public static List<Dictionary<string, AttributeValue>> ToDynamoDbMultiple<TSelf>(TSelf entity)");
        result.Should().Contain("// For backward compatibility, return the first item from multi-item conversion");
        result.Should().Contain("// Multi-item entity: combine all items into a single entity");
    }

    [Fact]
    public void GenerateEntityImplementation_WithRelatedEntities_GeneratesRelationshipMapping()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "SortKey",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "AuditEntries",
                    SortKeyPattern = "audit#*",
                    PropertyType = "List<AuditEntry>",
                    IsCollection = true,
                    EntityType = "AuditEntry"
                },
                new RelationshipModel
                {
                    PropertyName = "Summary",
                    SortKeyPattern = "summary",
                    PropertyType = "Summary",
                    IsCollection = false,
                    EntityType = "Summary"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("Related entities: 2 relationship(s) defined.");
        result.Should().Contain("// Populate related entity properties based on sort key patterns");
        result.Should().Contain("// Map related entity: AuditEntries");
        result.Should().Contain("// Map related entity: Summary");
        result.Should().Contain("if (sortKey.StartsWith(\"audit#\"))");
        result.Should().Contain("if (sortKey == \"summary\" || sortKey.StartsWith(\"summary#\"))");
    }

    [Fact]
    public void GenerateEntityImplementation_WithGsiProperties_GeneratesCorrectMetadata()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = "string",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "StatusIndex",
                            IsPartitionKey = true
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "StatusIndex",
                    PartitionKeyProperty = "Status"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("public static EntityMetadata GetEntityMetadata()");
        result.Should().Contain("Indexes = new IndexMetadata[]");
        result.Should().Contain("IndexName = \"StatusIndex\"");
        result.Should().Contain("PartitionKeyProperty = \"Status\"");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableProperties_GeneratesNullChecks()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "OptionalField",
                    AttributeName = "optional_field",
                    PropertyType = "string?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("if (typedEntity.OptionalField != null)");
        result.Should().Contain("item[\"optional_field\"] = new AttributeValue { S = typedEntity.OptionalField };");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCollectionProperties_GeneratesJsonSerialization()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Tags",
                    AttributeName = "tags",
                    PropertyType = "List<string>",
                    IsCollection = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("// Serialize collection Tags as JSON for single-item storage");
        result.Should().Contain("var tagsJson = System.Text.Json.JsonSerializer.Serialize(typedEntity.Tags);");
        result.Should().Contain("// Deserialize collection Tags from JSON");
        result.Should().Contain("entity.Tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(tagsValue.S);");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDifferentPropertyTypes_GeneratesCorrectConversions()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Count",
                    AttributeName = "count",
                    PropertyType = "int"
                },
                new PropertyModel
                {
                    PropertyName = "IsActive",
                    AttributeName = "is_active",
                    PropertyType = "bool"
                },
                new PropertyModel
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = "DateTime"
                },
                new PropertyModel
                {
                    PropertyName = "UniqueId",
                    AttributeName = "unique_id",
                    PropertyType = "Guid"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // Check ToDynamoDb conversions
        result.Should().Contain("new AttributeValue { S = typedEntity.Id }");
        result.Should().Contain("new AttributeValue { N = typedEntity.Count.ToString() }");
        result.Should().Contain("new AttributeValue { BOOL = typedEntity.IsActive }");
        result.Should().Contain("new AttributeValue { S = typedEntity.CreatedDate.ToString(\"O\") }");
        result.Should().Contain("new AttributeValue { S = typedEntity.UniqueId.ToString() }");
        
        // Check FromDynamoDb conversions
        result.Should().Contain("entity.Id = idValue.S");
        result.Should().Contain("entity.Count = int.Parse(countValue.N)");
        result.Should().Contain("entity.IsActive = isActiveValue.BOOL");
        result.Should().Contain("entity.CreatedDate = DateTime.Parse(createdDateValue.S)");
        result.Should().Contain("entity.UniqueId = Guid.Parse(uniqueIdValue.S)");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEntityDiscriminator_GeneratesDiscriminatorLogic()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            EntityDiscriminator = "TEST_ENTITY",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "SortKey",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("// Check entity discriminator");
        result.Should().Contain("return entityTypeValue.S == \"TEST_ENTITY\";");
        result.Should().Contain("EntityDiscriminator = \"TEST_ENTITY\"");
        result.Should().Contain("return sortKey == \"TEST_ENTITY\" || sortKey.StartsWith(\"TEST_ENTITY#\");");
    }

    [Fact]
    public void GenerateEntityImplementation_WithErrorHandling_GeneratesExceptionHandling()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Count",
                    AttributeName = "count",
                    PropertyType = "int"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("try");
        result.Should().Contain("catch (Exception ex)");
        result.Should().Contain("throw DynamoDbMappingException.PropertyConversionFailed(");
        result.Should().Contain("catch (DynamoDbMappingException)");
        result.Should().Contain("// Re-throw mapping exceptions as-is");
        result.Should().Contain("throw DynamoDbMappingException.EntityConstructionFailed(");
    }
}