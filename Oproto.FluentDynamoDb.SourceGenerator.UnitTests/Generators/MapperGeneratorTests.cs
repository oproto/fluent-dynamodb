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
        result.Should().Contain("Regex.IsMatch(sortKey, @\"^audit\\#[^\\#]*$\")",
            "should check sort key pattern for AuditEntries relationship using regex");
        result.Should().Contain("Regex.IsMatch(sortKey, @\"^summary$\")",
            "should check sort key pattern for Summary relationship using regex");
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
    public void GenerateEntityImplementation_WithDiscriminator_GeneratesDiscriminatorLogic()
    {
        // Arrange
        // Note: Using Discriminator property (new API) instead of EntityDiscriminator (obsolete)
        // The generator internally still uses EntityDiscriminator for backward compatibility
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "entity_type",
                ExactValue = "TEST_ENTITY",
                Strategy = DiscriminatorStrategy.ExactMatch
            },
#pragma warning disable CS0618 // Type or member is obsolete - Required for backward compatibility with generator
            EntityDiscriminator = "TEST_ENTITY", // Generator still uses this internally
#pragma warning restore CS0618
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

    [Fact]
    public void GenerateEntityImplementation_WithDateTimeKindUtc_GeneratesUtcConversion()
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
                    PropertyName = "CreatedAt",
                    AttributeName = "created_at",
                    PropertyType = "DateTime",
                    DateTimeKind = DateTimeKind.Utc
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should convert to UTC before serialization
        result.Should().Contain(".ToUniversalTime()",
            "should convert DateTime to UTC before serialization");
        result.Should().Contain("DateTime.SpecifyKind",
            "should set DateTime.Kind after deserialization");
        result.Should().Contain("DateTimeKind.Utc",
            "should specify UTC kind");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDateTimeKindLocal_GeneratesLocalConversion()
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
                    PropertyName = "LocalTime",
                    AttributeName = "local_time",
                    PropertyType = "DateTime",
                    DateTimeKind = DateTimeKind.Local
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should convert to Local before serialization
        result.Should().Contain(".ToLocalTime()",
            "should convert DateTime to Local time before serialization");
        result.Should().Contain("DateTime.SpecifyKind",
            "should set DateTime.Kind after deserialization");
        result.Should().Contain("DateTimeKind.Local",
            "should specify Local kind");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDateTimeFormatString_GeneratesFormattedSerialization()
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
                    PropertyName = "DateOnly",
                    AttributeName = "date_only",
                    PropertyType = "DateTime",
                    Format = "yyyy-MM-dd"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use custom format string
        result.Should().Contain("yyyy-MM-dd",
            "should use custom format string for DateTime serialization");
        result.Should().Contain("CultureInfo.InvariantCulture",
            "should use InvariantCulture for formatting");
        result.Should().Contain("ParseExact",
            "should use ParseExact for formatted DateTime deserialization");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDateTimeKindAndFormat_GeneratesBothConversions()
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
                    PropertyName = "UtcDate",
                    AttributeName = "utc_date",
                    PropertyType = "DateTime",
                    DateTimeKind = DateTimeKind.Utc,
                    Format = "yyyy-MM-dd"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should apply both Kind conversion and format string
        result.Should().Contain(".ToUniversalTime()",
            "should convert DateTime to UTC before serialization");
        result.Should().Contain("yyyy-MM-dd",
            "should use custom format string");
        result.Should().Contain("DateTime.SpecifyKind",
            "should set DateTime.Kind after deserialization");
        result.Should().Contain("ParseExact",
            "should use ParseExact for formatted DateTime deserialization");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNumericFormatString_GeneratesFormattedSerialization()
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
                    PropertyName = "Price",
                    AttributeName = "price",
                    PropertyType = "decimal",
                    Format = "F2"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use format string for decimal
        result.Should().Contain("F2",
            "should use custom format string for decimal serialization");
        result.Should().Contain("CultureInfo.InvariantCulture",
            "should use InvariantCulture for formatting");
        result.Should().Contain("NumberStyles.Any",
            "should use NumberStyles.Any for parsing formatted numbers");
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
            sb.AppendLine("using Oproto.FluentDynamoDb.Entities;");
            sb.AppendLine("using Oproto.FluentDynamoDb.Metadata;");
            sb.AppendLine();
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {entityType} : IDynamoDbEntity");
            sb.AppendLine("    {");
            sb.AppendLine("        public string Id { get; set; } = string.Empty;");
            sb.AppendLine();
            sb.AppendLine("        public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, Oproto.FluentDynamoDb.FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
            sb.AppendLine("        {");
            sb.AppendLine("            return new Dictionary<string, AttributeValue>();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, Oproto.FluentDynamoDb.FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
            sb.AppendLine("        {");
            sb.AppendLine($"            return (TSelf)(object)new {entityType}();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, Oproto.FluentDynamoDb.FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity");
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

    [Fact]
    public void GenerateEntityImplementation_WithIntegerFormatString_GeneratesFormattedSerialization()
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
                    PropertyName = "OrderNumber",
                    AttributeName = "order_number",
                    PropertyType = "int",
                    Format = "D5"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use format string for integer with zero-padding
        result.Should().Contain("D5",
            "should use custom format string for integer serialization with zero-padding");
        result.Should().Contain("CultureInfo.InvariantCulture",
            "should use InvariantCulture for formatting");
        result.Should().Contain("NumberStyles.Any",
            "should use NumberStyles.Any for parsing formatted integers");
        result.Should().Contain("int.TryParse",
            "should use TryParse for safe integer parsing");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDoubleFormatString_GeneratesFormattedSerialization()
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
                    PropertyName = "Temperature",
                    AttributeName = "temperature",
                    PropertyType = "double",
                    Format = "F4"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use format string for double
        result.Should().Contain("F4",
            "should use custom format string for double serialization with 4 decimal places");
        result.Should().Contain("CultureInfo.InvariantCulture",
            "should use InvariantCulture for formatting");
        result.Should().Contain("double.TryParse",
            "should use TryParse for safe double parsing");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCustomDateTimeFormat_GeneratesFormattedSerialization()
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
                    PropertyName = "EventDate",
                    AttributeName = "event_date",
                    PropertyType = "DateTime",
                    Format = "yyyy-MM-dd HH:mm:ss"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use custom DateTime format
        result.Should().Contain("yyyy-MM-dd HH:mm:ss",
            "should use custom format string for DateTime serialization");
        result.Should().Contain("DateTime.TryParseExact",
            "should use TryParseExact for formatted DateTime deserialization");
        result.Should().Contain("DateTimeStyles.None",
            "should use DateTimeStyles.None for strict parsing");
    }

    [Fact]
    public void GenerateEntityImplementation_WithFormatStringErrorHandling_GeneratesExceptionHandling()
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
                    PropertyName = "FormattedValue",
                    AttributeName = "formatted_value",
                    PropertyType = "decimal",
                    Format = "N2"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should have error handling for format string failures
        result.Should().Contain("catch (FormatException ex)",
            "should catch FormatException for invalid format strings during serialization");
        result.Should().Contain("Invalid format string",
            "should provide clear error message for format string failures");
        result.Should().Contain("DynamoDbMappingException",
            "should throw DynamoDbMappingException for parsing failures during deserialization");
        result.Should().Contain("Failed to parse",
            "should provide clear error message for parsing failures");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullablePropertyAndFormatString_GeneratesNullChecks()
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
                    PropertyName = "OptionalPrice",
                    AttributeName = "optional_price",
                    PropertyType = "decimal?",
                    IsNullable = true,
                    Format = "F2"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should check for null before applying format string
        result.Should().Contain("if (typedEntity.OptionalPrice != null)",
            "should check for null before formatting nullable property");
        result.Should().Contain("F2",
            "should use format string for nullable decimal");
        result.Should().Contain(".Value",
            "should access Value property of nullable type before formatting");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDateTimeOffsetFormat_GeneratesFormattedSerialization()
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
                    PropertyName = "Timestamp",
                    AttributeName = "timestamp",
                    PropertyType = "DateTimeOffset",
                    Format = "o"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should use format string for DateTimeOffset
        result.Should().Contain("DateTimeOffset.TryParseExact",
            "should use TryParseExact for formatted DateTimeOffset deserialization");
        result.Should().Contain("CultureInfo.InvariantCulture",
            "should use InvariantCulture for formatting");
    }

    [Fact]
    public void GenerateEntityImplementation_WithMultipleFormattedProperties_GeneratesAllFormats()
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
                    PropertyName = "Date",
                    AttributeName = "date",
                    PropertyType = "DateTime",
                    Format = "yyyy-MM-dd"
                },
                new PropertyModel
                {
                    PropertyName = "Price",
                    AttributeName = "price",
                    PropertyType = "decimal",
                    Format = "F2"
                },
                new PropertyModel
                {
                    PropertyName = "Quantity",
                    AttributeName = "quantity",
                    PropertyType = "int",
                    Format = "D8"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should generate format strings for all properties
        result.Should().Contain("yyyy-MM-dd",
            "should use DateTime format string");
        result.Should().Contain("F2",
            "should use decimal format string");
        result.Should().Contain("D8",
            "should use integer format string");
        result.Should().Contain("DateTime.TryParseExact",
            "should parse formatted DateTime");
        result.Should().Contain("decimal.TryParse",
            "should parse formatted decimal");
        result.Should().Contain("int.TryParse",
            "should parse formatted integer");
    }

    /// <summary>
    /// Tests that when only related items exist (no primary entity), the FromDynamoDb method returns null.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithMultiItemEntity_GeneratesNullReturnWhenNoPrimaryEntity()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Invoice",
            Namespace = "TestNamespace",
            TableName = "invoices",
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "CUSTOMER" }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "INVOICE" }
                },
                new PropertyModel
                {
                    PropertyName = "InvoiceNumber",
                    AttributeName = "invoice_number",
                    PropertyType = "string"
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Lines",
                    PropertyType = "List<InvoiceLine>",
                    SortKeyPattern = "INVOICE#*#LINE#*",
                    EntityType = "InvoiceLine",
                    IsCollection = true
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        var relatedSources = CreateRelatedEntitySources(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, new[] { entitySource }.Concat(relatedSources).ToArray());

        // Assert - Should generate null check for primary entity
        result.Should().Contain("if (primaryItem == null)",
            "should check if primary entity item was found");
        result.Should().Contain("return default!",
            "should return null (default!) when no primary entity item is found");
        result.Should().Contain("// Return null if no primary entity item found",
            "should have comment explaining the null return behavior");
    }
}