// ============================================================================
// MIGRATION STATUS: COMPLETED
// ============================================================================
// This test file has been migrated from brittle string-based assertions to
// semantic assertions and compilation verification.
//
// Migration changes:
// - Added CompilationVerifier.AssertGeneratedCodeCompiles() to all tests
// - Replaced method existence checks with .ShouldContainMethod()
// - Replaced assignment checks with .ShouldContainAssignment()
// - Replaced LINQ checks with .ShouldUseLinqMethod()
// - Replaced type reference checks with .ShouldReferenceType()
// - Preserved DynamoDB-specific checks (attribute types, null handling)
//   with descriptive "because" messages
//
// These tests now verify code structure semantically rather than through
// exact string matching, making them resilient to formatting changes while
// still catching actual errors.
// ============================================================================

using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Structural checks using semantic assertions
        result.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");
        result.Should().Contain("public partial class TestEntity : IDynamoDbEntity", "should implement IDynamoDbEntity interface for better UX");
        result.ShouldContainMethod("ToDynamoDb");
        result.ShouldContainMethod("FromDynamoDb");
        result.ShouldContainMethod("GetPartitionKey");
        result.ShouldContainMethod("MatchesEntity");
        result.ShouldContainMethod("GetEntityMetadata");
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
                    PropertyType = "List<string>",
                    IsCollection = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - DynamoDB-specific behavior checks
        result.Should().Contain("Multi-item entity: Supports entities that span multiple DynamoDB items.", 
            "should document multi-item entity support");
        result.Should().NotContain("ToDynamoDbMultiple", 
            "method was removed in Task 41");
        result.Should().Contain("// Multi-item entity: combine all items into a single entity",
            "should include comment explaining multi-item entity mapping logic");
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
            IsMultiItemEntity = true, // Make this a multi-item entity to test related entity mapping
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        var relatedEntitySources = CreateRelatedEntitySources(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, new[] { entitySource }.Concat(relatedEntitySources).ToArray());

        // Assert - DynamoDB-specific relationship mapping checks
        result.Should().Contain("Related entities: 2 relationship(s) defined.",
            "should document the number of relationships");
        result.Should().Contain("// Populate related entity properties based on sort key patterns",
            "should include comment explaining relationship mapping strategy");
        result.Should().Contain("// Map related entity: AuditEntries",
            "should document AuditEntries relationship mapping");
        result.Should().Contain("// Map related entity: Summary",
            "should document Summary relationship mapping");
        result.Should().Contain("if (sortKey.StartsWith(\"audit#\"))",
            "should check sort key pattern for AuditEntries relationship");
        result.Should().Contain("if (sortKey == \"summary\" || sortKey.StartsWith(\"summary#\"))",
            "should check sort key pattern for Summary relationship");
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Structural and metadata checks
        result.ShouldContainMethod("GetEntityMetadata");
        result.ShouldReferenceType("IndexMetadata");
        result.Should().Contain("IndexName = \"StatusIndex\"",
            "should set correct GSI index name");
        result.Should().Contain("PartitionKeyProperty = \"Status\"",
            "should set correct partition key property for GSI");
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - DynamoDB null handling checks
        result.Should().Contain("if (typedEntity.OptionalField != null)",
            "should check for null before adding nullable property to DynamoDB item");
        result.Should().Contain("S =", 
            "should use String (S) attribute type for string properties");
        result.ShouldContainAssignment("item[\"optional_field\"]");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCollectionProperties_GeneratesNativeDynamoDbCollections()
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - DynamoDB collection type conversions
        result.Should().Contain("// Convert collection Tags to native DynamoDB type",
            "should document collection conversion to DynamoDB");
        result.Should().Contain("// Convert List<string> to DynamoDB List (L)",
            "should document specific collection type conversion");
        result.Should().Contain("L =",
            "should use List (L) attribute type for List<string> collections");
        result.Should().Contain("S =",
            "should use String (S) attribute type for string elements in list");
        result.ShouldUseLinqMethod("Select");
        result.ShouldUseLinqMethod("ToList");
        result.Should().Contain("// Convert collection Tags from native DynamoDB type",
            "should document collection conversion from DynamoDB");
        result.Should().Contain("// Convert DynamoDB List (L) to List<string>",
            "should document specific collection type deserialization");
        result.ShouldContainAssignment("entity.Tags");
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - DynamoDB attribute type conversions
        // Check ToDynamoDb conversions use correct DynamoDB attribute types
        result.Should().Contain("S =",
            "should use String (S) attribute type for string, DateTime, and Guid properties");
        result.Should().Contain("N =",
            "should use Number (N) attribute type for int properties");
        result.Should().Contain("BOOL =",
            "should use Boolean (BOOL) attribute type for bool properties");
        result.Should().Contain(".ToString(\"O\")",
            "should use ISO 8601 format for DateTime serialization");
        
        // Check FromDynamoDb conversions use correct parsing
        result.ShouldContainAssignment("entity.Id");
        result.ShouldContainAssignment("entity.@Count"); // COUNT is a DynamoDB reserved word, so it's escaped
        result.ShouldContainAssignment("entity.IsActive");
        result.ShouldContainAssignment("entity.CreatedDate");
        result.ShouldContainAssignment("entity.UniqueId");
        result.ShouldReferenceType("DateTime");
        result.ShouldReferenceType("Guid");
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - DynamoDB entity discriminator logic
        result.Should().Contain("// Check entity discriminator",
            "should document entity discriminator check");
        result.Should().Contain("S =",
            "should use String (S) attribute type for entity discriminator");
        result.Should().Contain("== \"TEST_ENTITY\"",
            "should check for exact entity discriminator value");
        result.Should().Contain("EntityDiscriminator = \"TEST_ENTITY\"",
            "should set entity discriminator in metadata");
        result.Should().Contain("sortKey.StartsWith(\"TEST_ENTITY#\")",
            "should check sort key pattern for entity discriminator");
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

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Error handling structure
        result.Should().Contain("try",
            "should wrap conversion logic in try-catch blocks");
        result.Should().Contain("catch (Exception ex)",
            "should catch general exceptions during conversion");
        result.ShouldReferenceType("DynamoDbMappingException");
        result.Should().Contain("PropertyConversionFailed",
            "should throw PropertyConversionFailed for property conversion errors");
        result.Should().Contain("catch (DynamoDbMappingException)",
            "should have specific catch block for DynamoDbMappingException");
        result.Should().Contain("// Re-throw mapping exceptions as-is",
            "should document re-throwing of mapping exceptions");
        result.Should().Contain("EntityConstructionFailed",
            "should throw EntityConstructionFailed for entity construction errors");
    }

    /// <summary>
    /// Helper method to create entity source code from an EntityModel for compilation testing.
    /// </summary>
    private static string CreateEntitySource(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();
        
        // Add necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {entity.ClassName}");
        sb.AppendLine("    {");
        
        foreach (var prop in entity.Properties)
        {
            // Handle nullable types properly
            var propertyType = prop.PropertyType;
            if (prop.IsNullable && !propertyType.EndsWith("?") && !propertyType.Contains("<"))
            {
                propertyType += "?";
            }
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}");
        }
        
        // Add relationship properties
        foreach (var relationship in entity.Relationships)
        {
            sb.AppendLine($"        public {relationship.PropertyType} {relationship.PropertyName} {{ get; set; }}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Helper method to create stub source code for related entity types.
    /// </summary>
    private static string[] CreateRelatedEntitySources(EntityModel entity)
    {
        var sources = new List<string>();
        
        foreach (var relationship in entity.Relationships)
        {
            var entityType = relationship.EntityType;
            if (string.IsNullOrEmpty(entityType))
                continue;
                
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Amazon.DynamoDBv2.Model;");
            sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
            sb.AppendLine();
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {entityType} : IDynamoDbEntity");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Id { get; set; } = string.Empty;");
            sb.AppendLine();
            sb.AppendLine("        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, Oproto.FluentDynamoDb.Logging.IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity");
            sb.AppendLine("        {");
            sb.AppendLine("            return new Dictionary<string, AttributeValue>();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, Oproto.FluentDynamoDb.Logging.IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity");
            sb.AppendLine("        {");
            sb.AppendLine($"            return (TSelf)(object)new {entityType}();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, Oproto.FluentDynamoDb.Logging.IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity");
            sb.AppendLine("        {");
            sb.AppendLine($"            return (TSelf)(object)new {entityType}();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
            sb.AppendLine("        {");
            sb.AppendLine("            return string.Empty;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static bool MatchesEntity(Dictionary<string, AttributeValue> item)");
            sb.AppendLine("        {");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static EntityMetadata GetEntityMetadata()");
            sb.AppendLine("        {");
            sb.AppendLine("            return new EntityMetadata();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            sources.Add(sb.ToString());
        }
        
        return sources.ToArray();
    }
}