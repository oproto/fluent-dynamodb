using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests verifying that MapperGenerator produces correct FromDynamoDb hydration code
/// for entities with format-string computed keys. These tests validate the FIXED behavior:
/// correct split indices and type conversion expressions in generated hydration code.
///
/// **Feature: extract-components-naive-split-indexing**
/// **Validates: Requirements 2.2, 2.3, 2.4, 3.5**
/// </summary>
public class ExtractComponentsFormatStringHydrationTests
{
    /// <summary>
    /// Single variable with leading constant: Format = "TENANT#{0}#EXTERNAL_ACCESS"
    /// Split: ["TENANT", "{0}", "EXTERNAL_ACCESS"] → placeholder {0} maps to split index 1.
    /// FromDynamoDb should contain pkParts[1] with direct string assignment (no Parse).
    /// </summary>
    [Fact]
    public void FromDynamoDb_SingleVariableWithLeadingConstant_UsesPkParts1WithDirectAssignment()
    {
        // Arrange
        var entity = CreateSingleVariableLeadingConstantEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — hydration reads pkParts[1] for the variable value
        result.Should().Contain("pkParts[1]",
            "Format 'TENANT#{0}#EXTERNAL_ACCESS' splits to [TENANT, {0}, EXTERNAL_ACCESS] — " +
            "placeholder {0} is at split index 1");

        // String type should use direct assignment, not a Parse call
        result.Should().NotContain("int.Parse(pkParts[1])",
            "TenantId is a string property — should use direct assignment, not int.Parse");
    }

    /// <summary>
    /// Multiple variables with interspersed constants: Format = "TENANT#{0}#ROLE#{1}"
    /// Split: ["TENANT", "{0}", "ROLE", "{1}"] → {0}→1, {1}→3.
    /// FromDynamoDb should contain pkParts[1] and pkParts[3].
    /// </summary>
    [Fact]
    public void FromDynamoDb_MultipleVariablesWithInterspersedConstants_UsesPkParts1AndPkParts3()
    {
        // Arrange
        var entity = CreateMultiVariableInterspersedConstantsEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — hydration reads correct split indices for both placeholders
        result.Should().Contain("pkParts[1]",
            "Placeholder {0} is at split index 1 in 'TENANT#{0}#ROLE#{1}'");
        result.Should().Contain("pkParts[3]",
            "Placeholder {1} is at split index 3 in 'TENANT#{0}#ROLE#{1}'");
    }

    /// <summary>
    /// Sort key with leading constant: SK Format = "CAP#{0}#{1}"
    /// Split: ["CAP", "{0}", "{1}"] → {0}→1, {1}→2.
    /// FromDynamoDb should contain skParts[1] and skParts[2].
    /// </summary>
    [Fact]
    public void FromDynamoDb_SortKeyWithLeadingConstant_UsesSkParts1AndSkParts2()
    {
        // Arrange
        var entity = CreateSortKeyLeadingConstantEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — hydration uses skParts (sort key), not pkParts
        result.Should().Contain("skParts[1]",
            "Placeholder {0} is at split index 1 in SK 'CAP#{0}#{1}'");
        result.Should().Contain("skParts[2]",
            "Placeholder {1} is at split index 2 in SK 'CAP#{0}#{1}'");
    }

    /// <summary>
    /// Format specifier: SK Format = "SEQ#{0:D4}" with int extracted property.
    /// Split: ["SEQ", "{0:D4}"] → {0}→1.
    /// FromDynamoDb should contain int.Parse(skParts[1]).
    /// </summary>
    [Fact]
    public void FromDynamoDb_FormatSpecifierWithIntProperty_UsesIntParseSkParts1()
    {
        // Arrange
        var entity = CreateFormatSpecifierIntEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — hydration uses int.Parse for the int property at the correct split index
        result.Should().Contain("int.Parse(skParts[1])",
            "Format 'SEQ#{0:D4}' splits to [SEQ, {0:D4}] — placeholder {0} at split index 1, " +
            "and Sequence is int so it should use int.Parse(skParts[1])");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an entity with a single extracted string property from a format-string PK.
    /// Format = "TENANT#{0}#EXTERNAL_ACCESS", Extracted("Pk", 0) → TenantId (string)
    /// </summary>
    private static EntityModel CreateSingleVariableLeadingConstantEntity()
    {
        return new EntityModel
        {
            ClassName = "ExternalAccess",
            Namespace = "TestNamespace",
            TableName = "external_access",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "TenantId" },
                        Format = "TENANT#{0}#EXTERNAL_ACCESS",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "tenantId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 0,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with two extracted string properties from a format-string PK
    /// with interspersed constants.
    /// Format = "TENANT#{0}#ROLE#{1}"
    /// Split: ["TENANT", "{0}", "ROLE", "{1}"] → {0}→1, {1}→3
    /// </summary>
    private static EntityModel CreateMultiVariableInterspersedConstantsEntity()
    {
        return new EntityModel
        {
            ClassName = "TenantRole",
            Namespace = "TestNamespace",
            TableName = "tenant_roles",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "TenantId", "RoleId" },
                        Format = "TENANT#{0}#ROLE#{1}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "tenantId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "RoleId",
                    AttributeName = "roleId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 1,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with two extracted string properties from a format-string SK.
    /// SK Format = "CAP#{0}#{1}"
    /// Split: ["CAP", "{0}", "{1}"] → {0}→1, {1}→2
    /// </summary>
    private static EntityModel CreateSortKeyLeadingConstantEntity()
    {
        return new EntityModel
        {
            ClassName = "Capability",
            Namespace = "TestNamespace",
            TableName = "capabilities",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "ServiceName", "CapabilityName" },
                        Format = "CAP#{0}#{1}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "ServiceName",
                    AttributeName = "serviceName",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Sk",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "CapabilityName",
                    AttributeName = "capabilityName",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Sk",
                        Index = 1,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with an int extracted property from a format-string SK with format specifier.
    /// SK Format = "SEQ#{0:D4}", Extracted("Sk", 0) → Sequence (int)
    /// Split: ["SEQ", "{0:D4}"] → {0}→1
    /// </summary>
    private static EntityModel CreateFormatSpecifierIntEntity()
    {
        return new EntityModel
        {
            ClassName = "TimeEntry",
            Namespace = "TestNamespace",
            TableName = "time_entries",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Sequence" },
                        Format = "SEQ#{0:D4}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sequence",
                    AttributeName = "sequence",
                    PropertyType = "int",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Sk",
                        Index = 0,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    #endregion
}
