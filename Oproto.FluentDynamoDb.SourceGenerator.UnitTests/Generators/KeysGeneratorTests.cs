// Migration Status: COMPLETED
// This test file has been migrated to use compilation verification and semantic assertions
// instead of brittle string matching. Key format strings are preserved with descriptive messages.
// See: .kiro/specs/unit-test-fixes/design.md for migration patterns

using AwesomeAssertions;
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method");
        
        // Assert - Class and key format checks (DynamoDB-specific)
        result.Should().Contain("public static partial class TestEntityKeys",
            "should generate keys class for entity");
        result.Should().Contain("var keyValue = \"tenant#\" + id;",
            "should use the specified prefix 'tenant' with separator '#' for partition key format");
        result.Should().Contain("/// Builds the partition key value for Id.",
            "should include XML documentation describing the key builder purpose");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method");
        result.ShouldContainMethod("Sk", "should generate sort key builder method");
        result.ShouldContainMethod("Key", "should generate composite key builder method");

        // Assert - Class and key format checks (DynamoDB-specific)
        result.Should().Contain("public static partial class TestEntityKeys",
            "should generate keys class for entity");
        result.Should().Contain("var keyValue = \"tenant#\" + tenantId;",
            "should use the specified prefix 'tenant' with separator '#' for partition key format");
        result.Should().Contain("var keyValue = \"txn#\" + transactionId.ToString();",
            "should use the specified prefix 'txn' with separator '#' for sort key format and convert Guid to string");
        result.Should().Contain("return (Pk(tenantId), Sk(transactionId));",
            "should return tuple calling both Pk and Sk methods for composite key");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method for main table");
        result.ShouldContainMethod("Key", "should generate composite key builder for GSI");
        
        // Assert - Class and documentation checks (DynamoDB-specific)
        result.Should().Contain("public static partial class TestEntityKeys",
            "should generate main keys class for entity");
        // Note: GSI classes use the index name without redundant "Keys" suffix
        result.Should().Contain("public static partial class StatusIndex",
            "should generate nested keys class for GSI");
        result.Should().Contain("/// Key builder methods for StatusIndex Global Secondary Index.",
            "should include XML documentation describing the GSI key builder purpose");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method");
        
        // Assert - Null handling (DynamoDB-specific behavior)
        result.Should().Contain("if (id == null)",
            "should check for null parameter before building key");
        result.Should().Contain("throw new System.ArgumentNullException(nameof(id), \"Key parameter cannot be null.\");",
            "should throw ArgumentNullException with descriptive message when key parameter is null");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method");
        
        // Assert - Guid conversion (DynamoDB-specific type handling)
        result.Should().Contain("id.ToString()",
            "should convert Guid to string using ToString() for DynamoDB key format");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method");
        
        // Assert - DateTime formatting (DynamoDB-specific type handling)
        result.Should().Contain("createdDate.ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\")",
            "should format DateTime using ISO 8601 format with milliseconds for sortable DynamoDB keys");
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

        // Assert - Class generation (DynamoDB-specific)
        result.Should().Contain("public static partial class TestEntityKeys",
            "should generate keys class even when no keys are defined");
        
        // Assert - No key methods should be generated
        result.Should().NotContain("public static string Pk(",
            "should not generate Pk method when no partition key is defined");
        result.Should().NotContain("public static string Sk(",
            "should not generate Sk method when no sort key is defined");
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

        // Assert - Structural checks
        result.ShouldContainMethod("Pk", "should generate partition key builder method for GSI");
        
        // Assert - Class and custom key format parsing (DynamoDB-specific)
        // Note: GSI classes use the index name without redundant "Keys" suffix
        result.Should().Contain("public static partial class CustomIndex",
            "should generate nested keys class for custom GSI");
        result.Should().Contain("var keyValue = \"custom_\" + id;",
            "should parse custom format string 'custom_{0}_suffix' and generate concatenation with prefix 'custom_'");
    }

    [Fact]
    public void GenerateNestedKeysClass_ProducesSameMethodSignatures_AsGenerateKeysClass()
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

        // Act - Generate both versions
        var topLevelResult = KeysGenerator.GenerateKeysClass(entity);
        
        var sb = new System.Text.StringBuilder();
        KeysGenerator.GenerateNestedKeysClass(sb, entity);
        var nestedResult = sb.ToString();

        // Verify both compile
        CompilationVerifier.AssertGeneratedCodeCompiles(topLevelResult);
        CompilationVerifier.AssertGeneratedCodeCompiles($"namespace TestNamespace {{ public partial class TestEntity {{ {nestedResult} }} }}");

        // Assert - Main table Pk method signature
        topLevelResult.Should().Contain("public static string Pk(string tenantId)",
            "top-level class should have Pk method with string parameter");
        nestedResult.Should().Contain("public static string Pk(string tenantId)",
            "nested class should have identical Pk method signature");

        // Assert - Main table Sk method signature
        topLevelResult.Should().Contain("public static string Sk(System.Guid transactionId)",
            "top-level class should have Sk method with Guid parameter");
        nestedResult.Should().Contain("public static string Sk(System.Guid transactionId)",
            "nested class should have identical Sk method signature");

        // Assert - Main table Key method signature
        topLevelResult.Should().Contain("public static (string PartitionKey, string SortKey) Key(string tenantId, System.Guid transactionId)",
            "top-level class should have Key method returning tuple");
        nestedResult.Should().Contain("public static (string PartitionKey, string SortKey) Key(string tenantId, System.Guid transactionId)",
            "nested class should have identical Key method signature");

        // Assert - GSI class name is consistent between both methods
        // Note: Both methods now use the index name without redundant "Keys" suffix
        topLevelResult.Should().Contain("public static partial class StatusIndex",
            "top-level class should have GSI class without redundant 'Keys' suffix");
        nestedResult.Should().Contain("public static partial class StatusIndex",
            "nested class should have GSI class without redundant 'Keys' suffix");
        topLevelResult.Should().NotContain("StatusIndexKeys",
            "top-level class should not have redundant 'Keys' suffix in GSI class name");
        nestedResult.Should().NotContain("StatusIndexKeys",
            "nested class should not have redundant 'Keys' suffix in GSI class name");

        // Assert - GSI Pk method signature remains identical
        topLevelResult.Should().Contain("public static string Pk(string status)",
            "top-level GSI class should have Pk method");
        nestedResult.Should().Contain("public static string Pk(string status)",
            "nested GSI class should have identical Pk method signature");

        // Assert - Parameter validation logic is identical
        topLevelResult.Should().Contain("if (tenantId == null)",
            "top-level class should validate null parameters");
        nestedResult.Should().Contain("if (tenantId == null)",
            "nested class should have identical parameter validation");

        topLevelResult.Should().Contain("throw new System.ArgumentNullException(nameof(tenantId), \"Key parameter cannot be null.\")",
            "top-level class should throw ArgumentNullException");
        nestedResult.Should().Contain("throw new System.ArgumentNullException(nameof(tenantId), \"Key parameter cannot be null.\")",
            "nested class should throw identical exception");

        // Assert - Key building logic is identical
        topLevelResult.Should().Contain("var keyValue = \"tenant#\" + tenantId;",
            "top-level class should use same key format");
        nestedResult.Should().Contain("var keyValue = \"tenant#\" + tenantId;",
            "nested class should use identical key format");
    }
}