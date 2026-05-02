using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for Local Secondary Index (LSI) metadata generation.
/// Validates Requirements 4.1, 7.2 from the schema-validation spec.
/// </summary>
[Trait("Category", "Unit")]
public class LsiMetadataGenerationTests
{
    [Fact]
    public void GenerateEntityImplementation_WithLsiIndex_GeneratesCorrectIndexType()
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
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "CreatedAt",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = "string",
                    LsiSortKeys = new[]
                    {
                        new LsiSortKeyModel
                        {
                            IndexName = "StatusIndex"
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "StatusIndex",
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyProperty = "TenantId",
                    SortKeyProperty = "Status"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - LSI should have correct IndexType (uses full namespace to avoid ambiguity)
        result.Should().Contain("IndexType = Oproto.FluentDynamoDb.Metadata.IndexType.LocalSecondaryIndex",
            "LSI should have IndexType.LocalSecondaryIndex");
        result.Should().Contain("IndexName = \"StatusIndex\"",
            "should set correct LSI index name");
        result.Should().Contain("PartitionKeyProperty = \"TenantId\"",
            "LSI should inherit partition key from base table");
        result.Should().Contain("SortKeyProperty = \"Status\"",
            "LSI should have its own sort key");
    }

    [Fact]
    public void GenerateEntityImplementation_WithGsiIndex_GeneratesCorrectIndexType()
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
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = "string",
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel
                        {
                            IndexName = "EmailIndex",
}
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "EmailIndex",
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "Email"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - GSI should have correct IndexType (uses full namespace to avoid ambiguity)
        result.Should().Contain("IndexType = Oproto.FluentDynamoDb.Metadata.IndexType.GlobalSecondaryIndex",
            "GSI should have IndexType.GlobalSecondaryIndex");
        result.Should().Contain("IndexName = \"EmailIndex\"",
            "should set correct GSI index name");
    }

    [Fact]
    public void GenerateEntityImplementation_WithMixedIndexes_GeneratesCorrectIndexTypes()
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
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "CreatedAt",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = "string",
                    LsiSortKeys = new[]
                    {
                        new LsiSortKeyModel { IndexName = "StatusLSI" }
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = "string",
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel
                        {
                            IndexName = "EmailGSI",
}
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "StatusLSI",
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyProperty = "TenantId",
                    SortKeyProperty = "Status"
                },
                new IndexModel
                {
                    IndexName = "EmailGSI",
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "Email"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Both index types should be present (uses full namespace to avoid ambiguity)
        result.Should().Contain("IndexType = Oproto.FluentDynamoDb.Metadata.IndexType.LocalSecondaryIndex",
            "LSI should have IndexType.LocalSecondaryIndex");
        result.Should().Contain("IndexType = Oproto.FluentDynamoDb.Metadata.IndexType.GlobalSecondaryIndex",
            "GSI should have IndexType.GlobalSecondaryIndex");
        result.Should().Contain("IndexName = \"StatusLSI\"",
            "should set correct LSI index name");
        result.Should().Contain("IndexName = \"EmailGSI\"",
            "should set correct GSI index name");
    }

    [Fact]
    public void GenerateEntityImplementation_WithLsi_GeneratesKeyAttributeInfo()
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
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "CreatedAt",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Priority",
                    AttributeName = "priority",
                    PropertyType = "int",
                    LsiSortKeys = new[]
                    {
                        new LsiSortKeyModel { IndexName = "PriorityIndex" }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "PriorityIndex",
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyProperty = "TenantId",
                    SortKeyProperty = "Priority"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Should include attribute names and types
        result.Should().Contain("PartitionKeyAttributeName = \"pk\"",
            "should include partition key attribute name");
        result.Should().Contain("PartitionKeyAttributeType = \"S\"",
            "should include partition key attribute type (string = S)");
        result.Should().Contain("SortKeyAttributeName = \"priority\"",
            "should include sort key attribute name for LSI");
        result.Should().Contain("SortKeyAttributeType = \"N\"",
            "should include sort key attribute type (int = N)");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEntityMetadata_IncludesKeyInfo()
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
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "ItemId",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>()
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - EntityMetadata should include key attribute info
        result.Should().Contain("PartitionKeyAttributeName = \"pk\"",
            "EntityMetadata should include partition key attribute name");
        result.Should().Contain("PartitionKeyAttributeType = \"S\"",
            "EntityMetadata should include partition key attribute type");
        result.Should().Contain("SortKeyAttributeName = \"sk\"",
            "EntityMetadata should include sort key attribute name");
        result.Should().Contain("SortKeyAttributeType = \"S\"",
            "EntityMetadata should include sort key attribute type");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNumericKeys_GeneratesCorrectAttributeTypes()
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
                    PropertyName = "UserId",
                    AttributeName = "pk",
                    PropertyType = "long",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Timestamp",
                    AttributeName = "sk",
                    PropertyType = "long",
                    IsSortKey = true
                }
            },
            Indexes = Array.Empty<IndexModel>()
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Numeric types should be "N"
        result.Should().Contain("PartitionKeyAttributeType = \"N\"",
            "long partition key should have attribute type N");
        result.Should().Contain("SortKeyAttributeType = \"N\"",
            "long sort key should have attribute type N");
    }

    [Fact]
    public void GenerateEntityImplementation_WithProjectionType_GeneratesDefaultAll()
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
                    LsiSortKeys = new[]
                    {
                        new LsiSortKeyModel { IndexName = "StatusIndex" }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "StatusIndex",
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyProperty = "Id",
                    SortKeyProperty = "Status"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Default projection type should be All (uses full namespace to avoid ambiguity)
        result.Should().Contain("ProjectionType = Oproto.FluentDynamoDb.Metadata.ProjectionType.All",
            "default projection type should be All");
    }

    /// <summary>
    /// Helper method to create entity source code from an EntityModel for compilation testing.
    /// </summary>
    private static string CreateEntitySource(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();
        
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
            var propertyType = prop.PropertyType;
            if (prop.IsNullable && !propertyType.EndsWith("?") && !propertyType.Contains("<"))
            {
                propertyType += "?";
            }
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}
