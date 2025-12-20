using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for type-based table reference code generation.
/// Verifies that when using [DynamoDbTable(typeof(MyTable))], the generated code:
/// 1. Uses the exact type name (not appending "Table" again)
/// 2. Uses the namespace from the referenced type
/// 3. Generates accessor classes with correct parent table type references
/// </summary>
[Trait("Category", "Unit")]
public class TypeBasedTableReferenceGenerationTests
{
    /// <summary>
    /// Verifies that when using a type-based table reference, the generated table class
    /// uses the exact type name without appending "Table" again.
    /// 
    /// Regression test for: TenantsTable -> TenantsTableTable bug
    /// </summary>
    [Fact]
    public void TypeBasedTableReference_GeneratesCorrectClassName()
    {
        // Arrange - Entity using type-based table reference
        var entity = new EntityModel
        {
            ClassName = "TenantUser",
            Namespace = "MyApp.Entities",
            TableName = "TenantsTable", // This is set to the type name for grouping
            TableTypeName = "TenantsTable", // The actual type name
            TableNamespace = "MyApp.DataLayer",
            IsTableTypeReference = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert - Should use "TenantsTable" not "TenantsTableTable"
        result.Should().Contain("public partial class TenantsTable : IDynamoDbTable",
            "generated class should use the exact type name without appending 'Table'");
        result.Should().NotContain("TenantsTableTable",
            "generated code should NOT have doubled 'Table' suffix");
    }

    /// <summary>
    /// Verifies that when using a type-based table reference, the generated table class
    /// uses the namespace from the referenced type.
    /// </summary>
    [Fact]
    public void TypeBasedTableReference_UsesTypeNamespace()
    {
        // Arrange - Entity in different namespace than the table type
        var entity = new EntityModel
        {
            ClassName = "TenantUser",
            Namespace = "MyApp.Entities",
            TableName = "TenantsTable",
            TableTypeName = "TenantsTable",
            TableNamespace = "MyApp.DataLayer", // Namespace from the typeof() type
            IsTableTypeReference = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert - Should use the type's namespace
        result.Should().Contain("namespace MyApp.DataLayer;",
            "generated table class should be in the type's namespace");
        result.Should().Contain("using MyApp.Entities;",
            "generated code should include using directive for entity namespace");
    }

    /// <summary>
    /// Verifies that multi-entity tables with type-based references generate correct class names.
    /// </summary>
    [Fact]
    public void MultiEntityTable_TypeBasedReference_GeneratesCorrectClassName()
    {
        // Arrange - Multiple entities using type-based table reference
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "TenantUser",
                Namespace = "MyApp.Entities",
                TableName = "TenantsTable",
                TableTypeName = "TenantsTable",
                TableNamespace = "MyApp.DataLayer",
                IsTableTypeReference = true,
                IsDefault = true,
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "TenantId",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    }
                }
            },
            new EntityModel
            {
                ClassName = "TenantSettings",
                Namespace = "MyApp.Entities",
                TableName = "TenantsTable",
                TableTypeName = "TenantsTable",
                TableNamespace = "MyApp.DataLayer",
                IsTableTypeReference = true,
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "TenantId",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    }
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass("TenantsTable", entities);

        // Assert - Should use "TenantsTable" not "TenantsTableTable"
        result.Should().Contain("public partial class TenantsTable : IDynamoDbTable",
            "generated class should use the exact type name");
        result.Should().NotContain("TenantsTableTable",
            "generated code should NOT have doubled 'Table' suffix");
    }

    /// <summary>
    /// Verifies that accessor classes in multi-entity tables reference the correct parent table type.
    /// 
    /// Regression test for: Accessor classes referencing TenantsTableTable instead of TenantsTable
    /// </summary>
    [Fact]
    public void MultiEntityTable_TypeBasedReference_AccessorClassesReferenceCorrectTableType()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "TenantUser",
                Namespace = "MyApp.Entities",
                TableName = "TenantsTable",
                TableTypeName = "TenantsTable",
                TableNamespace = "MyApp.DataLayer",
                IsTableTypeReference = true,
                IsDefault = true,
                EntityPropertyConfig = new EntityPropertyConfig { Generate = true },
                Properties = new[]
                {
                    new PropertyModel
                    {
                        PropertyName = "TenantId",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    }
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass("TenantsTable", entities);

        // Assert - Accessor class should reference TenantsTable, not TenantsTableTable
        result.Should().Contain("private readonly TenantsTable _table;",
            "accessor class should reference the correct table type");
        result.Should().Contain("internal TenantUserAccessor(TenantsTable table)",
            "accessor constructor should accept the correct table type");
        result.Should().NotContain("TenantsTableTable",
            "accessor class should NOT reference doubled 'Table' suffix");
    }

    /// <summary>
    /// Property test: For any type name ending in "Table", the generated code should use
    /// that exact name without appending another "Table".
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TypeBasedReference_WithTableSuffix_DoesNotDoubleTableSuffix()
    {
        return Prop.ForAll(
            GenerateValidTypeName(),
            typeName =>
            {
                var tableTypeName = typeName + "Table";
                
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = "Test.Entities",
                    TableName = tableTypeName,
                    TableTypeName = tableTypeName,
                    TableNamespace = "Test.DataLayer",
                    IsTableTypeReference = true,
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Id",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true
                        }
                    }
                };

                var result = TableGenerator.GenerateTableClass(entity);

                var hasCorrectClassName = result.Contains($"public partial class {tableTypeName} : IDynamoDbTable");
                var hasNoDoubledSuffix = !result.Contains($"{tableTypeName}Table");

                return (hasCorrectClassName && hasNoDoubledSuffix).ToProperty()
                    .Label($"Type '{tableTypeName}' should generate class '{tableTypeName}', not '{tableTypeName}Table'. " +
                           $"HasCorrectClassName: {hasCorrectClassName}, HasNoDoubledSuffix: {hasNoDoubledSuffix}");
            });
    }

    /// <summary>
    /// Verifies that string-based table references still work correctly (appending "Table").
    /// </summary>
    [Fact]
    public void StringBasedTableReference_AppendsTableSuffix()
    {
        // Arrange - Entity using string-based table reference
        var entity = new EntityModel
        {
            ClassName = "Order",
            Namespace = "MyApp.Entities",
            TableName = "orders", // String table name
            TableTypeName = null,
            TableNamespace = null,
            IsTableTypeReference = false,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "OrderId",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = TableGenerator.GenerateTableClass(entity);

        // Assert - Should append "Table" to the table name
        result.Should().Contain("public partial class OrdersTable : IDynamoDbTable",
            "string-based table reference should append 'Table' suffix");
    }

    /// <summary>
    /// Generates valid C# type names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidTypeName()
    {
        return Arb.From(
            from length in Gen.Choose(3, 15)
            from firstChar in Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                                          'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z')
            from restChars in Gen.ArrayOf(length - 1, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'))
            select firstChar + new string(restChars)
        );
    }
}
