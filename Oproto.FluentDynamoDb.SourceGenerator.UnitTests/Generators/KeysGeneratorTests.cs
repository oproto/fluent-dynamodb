using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class KeysGeneratorTests
{
    [Fact]
    public void GenerateKeysClass_WithPartitionKeyOnly_GeneratesPartitionKeyBuilder()
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
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "tenant", Separator = "#" }
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestEntityKeys");
        result.Should().Contain("public static string Pk(string id)");
        result.Should().Contain("var keyValue = \"tenant#\" + id;");
        result.Should().Contain("/// <summary>");
        result.Should().Contain("/// Builds the partition key value for Id.");
    }

    [Fact]
    public void GenerateKeysClass_WithPartitionAndSortKey_GeneratesAllKeyBuilders()
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
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "tenant", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "TransactionId",
                    AttributeName = "sk",
                    PropertyType = "System.Guid",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "txn", Separator = "#" }
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestEntityKeys");

        // Partition key builder
        result.Should().Contain("public static string Pk(string tenantId)");
        result.Should().Contain("var keyValue = \"tenant#\" + tenantId;");

        // Sort key builder
        result.Should().Contain("public static string Sk(System.Guid transactionId)");
        result.Should().Contain("var keyValue = \"txn#\" + transactionId.ToString();");

        // Composite key builder
        result.Should().Contain("public static (string PartitionKey, string SortKey) Key(string tenantId, System.Guid transactionId)");
        result.Should().Contain("return (Pk(tenantId), Sk(transactionId));");
    }

    [Fact]
    public void GenerateKeysClass_WithGsi_GeneratesGsiKeyBuilders()
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
                },
                new PropertyModel
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = "System.DateTime",
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

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestEntityKeys");

        // Main table key builders
        result.Should().Contain("public static string Pk(string id)");

        // GSI key builders
        result.Should().Contain("public static partial class StatusIndexKeys");
        result.Should().Contain("/// Key builder methods for StatusIndex Global Secondary Index.");
        result.Should().Contain("public static string Pk(string status)");
        result.Should().Contain("public static string Sk(System.DateTime createdDate)");
        result.Should().Contain("public static (string PartitionKey, string SortKey) Key(string status, System.DateTime createdDate)");
    }

    [Fact]
    public void GenerateKeysClass_WithNullableTypes_GeneratesNullChecks()
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
                    PropertyType = "string?",
                    IsPartitionKey = true,
                    IsNullable = true
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static string Pk(string? id)");
        result.Should().Contain("if (id == null)");
        result.Should().Contain("throw new System.ArgumentNullException(nameof(id), \"Key parameter cannot be null.\");");
    }

    [Fact]
    public void GenerateKeysClass_WithGuidType_GeneratesToStringConversion()
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
                    PropertyType = "System.Guid",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static string Pk(System.Guid id)");
        result.Should().Contain("var keyValue = id.ToString();");
    }

    [Fact]
    public void GenerateKeysClass_WithDateTimeType_GeneratesFormattedString()
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
                    PropertyName = "CreatedDate",
                    AttributeName = "pk",
                    PropertyType = "System.DateTime",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static string Pk(System.DateTime createdDate)");
        result.Should().Contain("var keyValue = createdDate.ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\");");
    }

    [Fact]
    public void GenerateKeysClass_WithNoKeys_GeneratesEmptyClass()
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
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class TestEntityKeys");
        result.Should().Contain("namespace TestNamespace");
        result.Should().NotContain("public static string Pk(");
        result.Should().NotContain("public static string Sk(");
    }

    [Fact]
    public void GenerateKeysClass_WithCustomKeyFormat_ParsesFormatCorrectly()
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
                    IsPartitionKey = true,
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = "CustomIndex",
                            IsPartitionKey = true,
                            KeyFormat = "custom_{0}_suffix"
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "CustomIndex",
                    PartitionKeyProperty = "Id"
                }
            }
        };

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert
        result.Should().Contain("public static partial class CustomIndexKeys");
        result.Should().Contain("public static string Pk(string id)");
        // The key format parsing should handle the custom format
        result.Should().Contain("var keyValue = \"custom_\" + id;");
    }
}