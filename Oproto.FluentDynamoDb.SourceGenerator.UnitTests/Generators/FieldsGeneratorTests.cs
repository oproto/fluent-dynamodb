using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class FieldsGeneratorTests
{
    [Fact]
    public void GenerateFieldsClass_WithBasicEntity_ProducesCorrectCode()
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name"
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("namespace TestNamespace");
        result.Should().Contain("public static partial class TestEntityFields");
        result.Should().Contain("public const string Id = \"pk\";");
        result.Should().Contain("public const string Name = \"name\";");
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// DynamoDB attribute name for Id property.");
        result.Should().Contain("/// </summary>");
    }

    [Fact]
    public void GenerateFieldsClass_WithGsiProperties_GeneratesNestedGsiClasses()
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiKey",
                    AttributeName = "gsi_pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "TestGSI",
                            IsPartitionKey = true
                        }
                    }
                },
                new PropertyModel
                {
                    PropertyName = "GsiSort",
                    AttributeName = "gsi_sk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "TestGSI",
                            IsSortKey = true
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "TestGSI",
                    PartitionKeyProperty = "GsiKey",
                    SortKeyProperty = "GsiSort"
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestGSIFields");
        result.Should().Contain("public const string PartitionKey = \"gsi_pk\";");
        result.Should().Contain("public const string SortKey = \"gsi_sk\";");
        result.Should().Contain("/// Field name constants for TestGSI Global Secondary Index.");
    }

    [Fact]
    public void GenerateFieldsClass_WithReservedWords_HandlesCorrectly()
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
                    PropertyName = "class", // C# reserved word
                    AttributeName = "class_attr"
                },
                new PropertyModel
                {
                    PropertyName = "COUNT", // DynamoDB reserved word
                    AttributeName = "count_attr"
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public const string @class = \"class_attr\";");
        result.Should().Contain("public const string @COUNT = \"count_attr\";");
    }

    [Fact]
    public void GenerateFieldsClass_WithNoAttributeMappings_GeneratesEmptyClass()
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
                    AttributeName = "", // No attribute mapping
                    IsPartitionKey = false
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestEntityFields");
        result.Should().NotContain("public const string Id");
    }

    [Fact]
    public void GenerateFieldsClass_WithComplexGsiName_GeneratesSafeClassName()
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiKey",
                    AttributeName = "gsi_pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "test-gsi-with-dashes",
                            IsPartitionKey = true
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "test-gsi-with-dashes",
                    PartitionKeyProperty = "GsiKey"
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class test_gsi_with_dashesFields");
        result.Should().Contain("public const string PartitionKey = \"gsi_pk\";");
    }

    [Fact]
    public void GenerateFieldsClass_WithMultipleGsis_GeneratesAllNestedClasses()
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Gsi1Key",
                    AttributeName = "gsi1_pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "GSI1",
                            IsPartitionKey = true
                        }
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Gsi2Key",
                    AttributeName = "gsi2_pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "GSI2",
                            IsPartitionKey = true
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "GSI1",
                    PartitionKeyProperty = "Gsi1Key"
                },
                new IndexModel
                {
                    IndexName = "GSI2",
                    PartitionKeyProperty = "Gsi2Key"
                }
            }
        };

        // Act
        var result = FieldsGenerator.GenerateFieldsClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class GSI1Fields");
        result.Should().Contain("public static partial class GSI2Fields");
    }
}