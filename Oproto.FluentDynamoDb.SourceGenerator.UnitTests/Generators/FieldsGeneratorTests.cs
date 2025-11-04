// ============================================================================
// MIGRATION STATUS: COMPLETED
// ============================================================================
// This test file has been migrated from brittle string-based assertions to
// semantic assertions and compilation verification.
//
// Migration changes:
// - Compilation verification already present in all tests
// - Replaced class existence checks with .ShouldContainClass()
// - Replaced constant field checks with .ShouldContainConstant()
// - Preserved field constant value checks with descriptive "because" messages
// - Preserved attribute name mapping checks with descriptive "because" messages
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

        // Assert - Structural checks using semantic assertions
        result.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");
        result.ShouldContainClass("TestEntityFields");
        result.ShouldContainConstant("Id");
        result.ShouldContainConstant("Name");
        
        // Assert - DynamoDB-specific value checks
        result.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        result.Should().Contain("public const string Name = \"name\";", "should map Name property to name attribute");
        result.Should().Contain("/// <summary>", "should include XML documentation");
        result.Should().Contain("/// DynamoDB attribute name for Id property.", "should document the attribute mapping");
        result.Should().Contain("/// </summary>", "should close XML documentation");
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

        // Assert - Structural checks using semantic assertions
        result.ShouldContainClass("TestGSIFields");
        result.ShouldContainConstant("PartitionKey");
        result.ShouldContainConstant("SortKey");
        
        // Assert - DynamoDB-specific value checks
        result.Should().Contain("public const string PartitionKey = \"gsi_pk\";", "should map GSI partition key to gsi_pk attribute");
        result.Should().Contain("public const string SortKey = \"gsi_sk\";", "should map GSI sort key to gsi_sk attribute");
        result.Should().Contain("/// Field name constants for TestGSI Global Secondary Index.", "should document the GSI purpose");
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

        // Assert - DynamoDB-specific value checks (constants with @ prefix are still identifiable)
        result.Should().Contain("public const string @class = \"class_attr\";", "should escape C# reserved word 'class' with @ prefix");
        result.Should().Contain("public const string @COUNT = \"count_attr\";", "should escape DynamoDB reserved word 'COUNT' with @ prefix");
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

        // Assert - Structural checks using semantic assertions
        result.ShouldContainClass("TestEntityFields");
        
        // Assert - Verify no constants generated for unmapped properties
        result.Should().NotContain("public const string Id", "should not generate constant for property without attribute mapping");
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

        // Assert - Structural checks using semantic assertions
        result.ShouldContainClass("test_gsi_with_dashesFields");
        result.ShouldContainConstant("PartitionKey");
        
        // Assert - DynamoDB-specific value checks
        result.Should().Contain("public const string PartitionKey = \"gsi_pk\";", "should map GSI partition key to gsi_pk attribute");
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

        // Assert - Structural checks using semantic assertions
        result.ShouldContainClass("GSI1Fields");
        result.ShouldContainClass("GSI2Fields");
    }

    [Fact]
    public void GenerateNestedFieldsClass_ProducesSameFieldDeclarations_AsGenerateFieldsClass()
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
                },
                new PropertyModel
                {
                    PropertyName = "Email",
                    AttributeName = "email"
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "StatusIndex",
                            IsPartitionKey = true
                        }
                    }
                },
                new PropertyModel
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "StatusIndex",
                            IsSortKey = true
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "StatusIndex",
                    PartitionKeyProperty = "Status",
                    SortKeyProperty = "CreatedDate"
                }
            }
        };

        // Act - Generate both versions
        var topLevelResult = FieldsGenerator.GenerateFieldsClass(entity);
        
        var sb = new System.Text.StringBuilder();
        FieldsGenerator.GenerateNestedFieldsClass(sb, entity);
        var nestedResult = sb.ToString();

        // Verify both compile
        CompilationVerifier.AssertGeneratedCodeCompiles(topLevelResult);
        CompilationVerifier.AssertGeneratedCodeCompiles($"namespace TestNamespace {{ public partial class TestEntity {{ {nestedResult} }} }}");

        // Assert - Main field constants are identical
        topLevelResult.Should().Contain("public const string Id = \"pk\";",
            "top-level class should have Id constant");
        nestedResult.Should().Contain("public const string Id = \"pk\";",
            "nested class should have identical Id constant");

        topLevelResult.Should().Contain("public const string Name = \"name\";",
            "top-level class should have Name constant");
        nestedResult.Should().Contain("public const string Name = \"name\";",
            "nested class should have identical Name constant");

        topLevelResult.Should().Contain("public const string Email = \"email\";",
            "top-level class should have Email constant");
        nestedResult.Should().Contain("public const string Email = \"email\";",
            "nested class should have identical Email constant");

        // Assert - GSI class name differs (intentional breaking change)
        // Note: The old GenerateFieldsClass already uses "StatusIndex" without suffix
        // Both implementations use the same GSI class naming (without redundant suffix)
        topLevelResult.Should().Contain("public static partial class StatusIndex",
            "top-level class should have GSI class");
        nestedResult.Should().Contain("public static partial class StatusIndex",
            "nested class should have GSI class");
        topLevelResult.Should().NotContain("StatusIndexFields",
            "top-level class should not have redundant 'Fields' suffix in GSI class name");
        nestedResult.Should().NotContain("StatusIndexFields",
            "nested class should not have redundant 'Fields' suffix in GSI class name");

        // Assert - GSI field constants are identical
        topLevelResult.Should().Contain("public const string PartitionKey = \"status\";",
            "top-level GSI class should have PartitionKey constant");
        nestedResult.Should().Contain("public const string PartitionKey = \"status\";",
            "nested GSI class should have identical PartitionKey constant");

        topLevelResult.Should().Contain("public const string SortKey = \"created_date\";",
            "top-level GSI class should have SortKey constant");
        nestedResult.Should().Contain("public const string SortKey = \"created_date\";",
            "nested GSI class should have identical SortKey constant");

        // Assert - Field constant types are identical (const string)
        var topLevelIdMatch = System.Text.RegularExpressions.Regex.Match(topLevelResult, @"public const string Id = ""pk"";");
        var nestedIdMatch = System.Text.RegularExpressions.Regex.Match(nestedResult, @"public const string Id = ""pk"";");
        
        topLevelIdMatch.Success.Should().BeTrue("top-level class should have properly formatted Id constant");
        nestedIdMatch.Success.Should().BeTrue("nested class should have properly formatted Id constant");
        topLevelIdMatch.Value.Should().Be(nestedIdMatch.Value,
            "field constant declarations should be byte-for-byte identical");
    }
}