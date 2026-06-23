using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for non-string source property types in computed keys.
/// Verifies that typed overload parameter types match int, DateTime, Guid, and enum
/// source property types, with correct camelCase naming for each parameter.
///
/// **Validates: Requirements 2.2, 2.3, 13.5**
/// </summary>
[Trait("Category", "Unit")]
public class NonStringSourcePropertyTypeUnitTests
{
    /// <summary>
    /// Verifies that int source properties produce typed overload parameters with int type.
    /// </summary>
    [Fact]
    public void IntSourceProperty_TypedOverload_HasIntParameterType()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload parameters are int
        generatedCode.Should().Contain("Get(int year, int month)");
    }

    /// <summary>
    /// Verifies that DateTime source properties produce typed overload parameters with DateTime type.
    /// </summary>
    [Fact]
    public void DateTimeSourceProperty_TypedOverload_HasDateTimeParameterType()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("StartDate", "DateTime"), ("EndDate", "DateTime") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload parameters are DateTime
        generatedCode.Should().Contain("Get(DateTime startDate, DateTime endDate)");
    }

    /// <summary>
    /// Verifies that Guid source properties produce typed overload parameters with Guid type.
    /// </summary>
    [Fact]
    public void GuidSourceProperty_TypedOverload_HasGuidParameterType()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("TenantId", "Guid"), ("EventId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload parameters are Guid
        generatedCode.Should().Contain("Get(Guid tenantId, Guid eventId)");
    }

    /// <summary>
    /// Verifies that enum source properties (namespace-qualified) produce typed overload
    /// parameters with the exact enum type.
    /// </summary>
    [Fact]
    public void EnumSourceProperty_TypedOverload_HasEnumParameterType()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("Status", "OrderStatus"), ("Priority", "PriorityLevel") },
            enumProps: new[] { "Status", "Priority" });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload parameters are the enum types
        generatedCode.Should().Contain("Get(OrderStatus status, PriorityLevel priority)");
    }

    /// <summary>
    /// Verifies that a mix of int, DateTime, Guid, and enum source properties
    /// all have correct types in the typed overload.
    /// </summary>
    [Fact]
    public void MixedNonStringTypes_TypedOverload_AllParameterTypesMatchSourceProperties()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[]
            {
                ("Year", "int"),
                ("StartDate", "DateTime"),
                ("EventId", "Guid"),
                ("Status", "EventStatus")
            },
            enumProps: new[] { "Status" });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload has all parameter types matching source property types
        generatedCode.Should().Contain("Get(int year, DateTime startDate, Guid eventId, EventStatus status)");
    }

    /// <summary>
    /// Verifies camelCase naming for int source property parameter.
    /// PropertyName "TenantId" → parameter name "tenantId"
    /// </summary>
    [Fact]
    public void IntSourceProperty_ParameterName_IsCamelCase()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("TenantId", "int"), ("RegionCode", "int") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — parameter names are camelCase
        generatedCode.Should().Contain("int tenantId");
        generatedCode.Should().Contain("int regionCode");
    }

    /// <summary>
    /// Verifies camelCase naming for DateTime source property parameter.
    /// PropertyName "StartDate" → parameter name "startDate"
    /// </summary>
    [Fact]
    public void DateTimeSourceProperty_ParameterName_IsCamelCase()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("StartDate", "DateTime"), ("CreatedAt", "DateTime") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — parameter names are camelCase
        generatedCode.Should().Contain("DateTime startDate");
        generatedCode.Should().Contain("DateTime createdAt");
    }

    /// <summary>
    /// Verifies camelCase naming for Guid source property parameter.
    /// PropertyName "EventId" → parameter name "eventId"
    /// </summary>
    [Fact]
    public void GuidSourceProperty_ParameterName_IsCamelCase()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("EventId", "Guid"), ("CorrelationId", "Guid") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — parameter names are camelCase
        generatedCode.Should().Contain("Guid eventId");
        generatedCode.Should().Contain("Guid correlationId");
    }

    /// <summary>
    /// Verifies camelCase naming for enum source property parameter.
    /// PropertyName "Status" → parameter name "status"
    /// </summary>
    [Fact]
    public void EnumSourceProperty_ParameterName_IsCamelCase()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[] { ("Status", "OrderStatus"), ("Category", "ItemCategory") },
            enumProps: new[] { "Status", "Category" });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — parameter names are camelCase
        generatedCode.Should().Contain("OrderStatus status");
        generatedCode.Should().Contain("ItemCategory category");
    }

    /// <summary>
    /// Verifies that typed overloads for Delete, Update, and ConditionCheck
    /// also use the correct non-string parameter types.
    /// </summary>
    [Fact]
    public void MixedNonStringTypes_AllCrudMethods_HaveCorrectParameterTypes()
    {
        // Arrange
        var entity = CreateComputedPkEntity(
            pkSourceProps: new[]
            {
                ("Year", "int"),
                ("StartDate", "DateTime"),
                ("EventId", "Guid")
            });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — all CRUD methods have same typed parameter signature
        var expectedParams = "int year, DateTime startDate, Guid eventId";
        generatedCode.Should().Contain($"Get({expectedParams})");
        generatedCode.Should().Contain($"Delete({expectedParams})");
        generatedCode.Should().Contain($"Update({expectedParams})");
        generatedCode.Should().Contain($"ConditionCheck({expectedParams})");
    }

    /// <summary>
    /// Verifies that non-string types in a computed SK (with simple string PK)
    /// produce correct typed overload with PK string followed by typed SK params.
    /// </summary>
    [Fact]
    public void ComputedSkWithNonStringTypes_TypedOverload_HasPkStringFollowedByTypedSkParams()
    {
        // Arrange
        var entity = CreateSimplePkComputedSkEntity(
            skSourceProps: new[]
            {
                ("Year", "int"),
                ("StartDate", "DateTime"),
                ("EventId", "Guid")
            });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — typed overload has string pK + typed SK params
        generatedCode.Should().Contain("Get(string pK, int year, DateTime startDate, Guid eventId)");
    }

    /// <summary>
    /// Verifies that non-string types in both computed PK and SK produce
    /// a single typed overload with all parameters correctly typed.
    /// </summary>
    [Fact]
    public void BothKeysComputed_NonStringTypes_SingleTypedOverloadWithCorrectTypes()
    {
        // Arrange
        var entity = CreateBothKeysComputedEntity(
            pkSourceProps: new[] { ("Year", "int"), ("Month", "int") },
            skSourceProps: new[] { ("EventId", "Guid"), ("Timestamp", "DateTime") });

        // Act
        var generatedCode = GenerateCode(entity);

        // Assert — single typed overload with PK params first, then SK params
        generatedCode.Should().Contain("Get(int year, int month, Guid eventId, DateTime timestamp)");
    }

    #region Helper Methods

    private static string GenerateCode(EntityModel entity)
    {
        return TableGenerator.GenerateTableClass(entity.TableName, new List<EntityModel> { entity });
    }

    private static EntityModel CreateComputedPkEntity(
        (string Name, string Type)[] pkSourceProps,
        string[]? enumProps = null)
    {
        var properties = new List<PropertyModel>();
        var sourcePropertyNames = new List<string>();
        var enumSet = new HashSet<string>(enumProps ?? Array.Empty<string>());

        // Add source properties
        foreach (var (name, type) in pkSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsEnum = enumSet.Contains(name)
            });
            sourcePropertyNames.Add(name);
        }

        // Add computed PK property (no SK)
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = sourcePropertyNames.ToArray(),
                Separator = "#"
            }
        });

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateSimplePkComputedSkEntity(
        (string Name, string Type)[] skSourceProps,
        string[]? enumProps = null)
    {
        var properties = new List<PropertyModel>();
        var sourcePropertyNames = new List<string>();
        var enumSet = new HashSet<string>(enumProps ?? Array.Empty<string>());

        // Simple string PK
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true
        });

        // Add SK source properties
        foreach (var (name, type) in skSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsEnum = enumSet.Contains(name)
            });
            sourcePropertyNames.Add(name);
        }

        // Add computed SK property
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = sourcePropertyNames.ToArray(),
                Separator = "#"
            }
        });

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateBothKeysComputedEntity(
        (string Name, string Type)[] pkSourceProps,
        (string Name, string Type)[] skSourceProps,
        string[]? enumProps = null)
    {
        var properties = new List<PropertyModel>();
        var pkSourceNames = new List<string>();
        var skSourceNames = new List<string>();
        var enumSet = new HashSet<string>(enumProps ?? Array.Empty<string>());

        // Add PK source properties
        foreach (var (name, type) in pkSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsEnum = enumSet.Contains(name)
            });
            pkSourceNames.Add(name);
        }

        // Add computed PK property
        properties.Add(new PropertyModel
        {
            PropertyName = "Pk",
            PropertyType = "string",
            AttributeName = "pk",
            IsPartitionKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = pkSourceNames.ToArray(),
                Separator = "#"
            }
        });

        // Add SK source properties
        foreach (var (name, type) in skSourceProps)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsEnum = enumSet.Contains(name)
            });
            skSourceNames.Add(name);
        }

        // Add computed SK property
        properties.Add(new PropertyModel
        {
            PropertyName = "Sk",
            PropertyType = "string",
            AttributeName = "sk",
            IsSortKey = true,
            ComputedKey = new ComputedKeyModel
            {
                SourceProperties = skSourceNames.ToArray(),
                Separator = "#"
            }
        });

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    #endregion
}
