using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation tests for the extract components naive split indexing fix.
/// These tests verify that separator-based extraction (no format string, HasCustomFormat == false)
/// is UNCHANGED by the fix. Separator-based keys have no constant segments, so placeholder
/// index == split index and the current behavior is correct.
///
/// These tests MUST PASS on both unfixed and fixed code.
///
/// **Feature: extract-components-naive-split-indexing, Property 3: Preservation**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
public class ExtractComponentsNaiveSplitIndexingPreservationTests
{
    /// <summary>
    /// Test 1: KeysGenerator — separator-based entity with two sources ["TenantId", "UserId"].
    /// ExtractPkComponents should return parts[0] and parts[1] using direct string assignment.
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void KeysGenerator_TwoSourceSeparatorEntity_ShouldUseDirectParts0AndParts1()
    {
        // Arrange
        var entity = CreateTwoSourceSeparatorEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — observed: return (TenantId: parts[0], UserId: parts[1]);
        result.Should().Contain("parts[0]",
            "Separator-based entity with two sources should use parts[0] for the first extracted property");
        result.Should().Contain("parts[1]",
            "Separator-based entity with two sources should use parts[1] for the second extracted property");
        result.Should().Contain("TenantId: parts[0]",
            "TenantId should be extracted from parts[0] with direct assignment (no Parse)");
        result.Should().Contain("UserId: parts[1]",
            "UserId should be extracted from parts[1] with direct assignment (no Parse)");
    }

    /// <summary>
    /// Test 2: MapperGenerator — separator-based entity with two sources ["TenantId", "UserId"].
    /// FromDynamoDb hydration should use pkParts[0] and pkParts[1] with direct assignment.
    /// Validates: Requirements 3.1, 3.3
    /// </summary>
    [Fact]
    public void MapperGenerator_TwoSourceSeparatorEntity_ShouldUsePkParts0AndPkParts1()
    {
        // Arrange
        var entity = CreateTwoSourceSeparatorEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — observed: entity.TenantId = pkParts[0]; entity.UserId = pkParts[1];
        result.Should().Contain("entity.TenantId = pkParts[0]",
            "Separator-based entity should hydrate TenantId from pkParts[0] with direct assignment");
        result.Should().Contain("entity.UserId = pkParts[1]",
            "Separator-based entity should hydrate UserId from pkParts[1] with direct assignment");
    }

    /// <summary>
    /// Test 3: KeysGenerator — separator-based entity with three sources ["Year", "Month", "Label"].
    /// ExtractPkComponents should use parts[0], parts[1], parts[2].
    /// Validates: Requirements 3.1, 3.2
    /// </summary>
    [Fact]
    public void KeysGenerator_ThreeSourceSeparatorEntity_ShouldUseParts0Through2()
    {
        // Arrange
        var entity = CreateThreeSourceSeparatorEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — observed: return (Year: parts[0], Month: parts[1], Label: parts[2]);
        result.Should().Contain("Year: parts[0]",
            "Year should be extracted from parts[0]");
        result.Should().Contain("Month: parts[1]",
            "Month should be extracted from parts[1]");
        result.Should().Contain("Label: parts[2]",
            "Label should be extracted from parts[2]");
    }

    /// <summary>
    /// Test 4: KeysGenerator — string extracted properties use direct assignment (no Parse).
    /// For separator-based computed keys with string sources, extraction should be parts[N] directly.
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void KeysGenerator_StringExtractedProperties_ShouldNotUseParse()
    {
        // Arrange
        var entity = CreateTwoSourceSeparatorEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — string extracted properties should not use Parse
        result.Should().NotContain("Parse(parts[0])",
            "String extracted properties should use direct assignment, not Parse");
        result.Should().NotContain("Parse(parts[1])",
            "String extracted properties should use direct assignment, not Parse");
    }

    /// <summary>
    /// Test 5: KeysGenerator — int extracted property uses int.Parse(parts[N]).
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void KeysGenerator_IntExtractedProperty_ShouldUseIntParse()
    {
        // Arrange
        var entity = CreateIntExtractedPropertyEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — observed: return (Region: parts[0], Year: int.Parse(parts[1]));
        result.Should().Contain("int.Parse(parts[1])",
            "Int extracted property should use int.Parse(parts[N]) for type conversion");
        result.Should().Contain("Region: parts[0]",
            "String extracted property alongside int should use direct assignment");
    }

    /// <summary>
    /// Test 6: MapperGenerator — int extracted property uses int.Parse(pkParts[N]).
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void MapperGenerator_IntExtractedProperty_ShouldUseIntParse()
    {
        // Arrange
        var entity = CreateIntExtractedPropertyEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — observed: entity.@Year = int.Parse(pkParts[1]);
        result.Should().Contain("int.Parse(pkParts[1])",
            "Int extracted property in FromDynamoDb should use int.Parse(pkParts[N])");
    }

    /// <summary>
    /// Test 7: KeysGenerator — enum extracted property uses Enum.Parse&lt;T&gt;(parts[N]).
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void KeysGenerator_EnumExtractedProperty_ShouldUseEnumParse()
    {
        // Arrange
        var entity = CreateEnumExtractedPropertyEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — observed: return (Category: parts[0], Priority: Enum.Parse<TaskPriority>(parts[1]));
        result.Should().Contain("Enum.Parse<TaskPriority>(parts[1])",
            "Enum extracted property should use Enum.Parse<T>(parts[N]) for type conversion");
        result.Should().Contain("Category: parts[0]",
            "String extracted property alongside enum should use direct assignment");
    }

    /// <summary>
    /// Test 8: MapperGenerator — enum extracted property uses Enum.Parse&lt;T&gt;(pkParts[N]).
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void MapperGenerator_EnumExtractedProperty_ShouldUseEnumParse()
    {
        // Arrange
        var entity = CreateEnumExtractedPropertyEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — observed: entity.Priority = Enum.Parse<TaskPriority>(pkParts[1]);
        result.Should().Contain("Enum.Parse<TaskPriority>(pkParts[1])",
            "Enum extracted property in FromDynamoDb should use Enum.Parse<T>(pkParts[N])");
    }

    #region Helper Methods

    /// <summary>
    /// Creates a separator-based entity with two string extracted properties.
    /// Separator = "#", sources = ["TenantId", "UserId"], no Format (HasCustomFormat == false).
    /// </summary>
    private static EntityModel CreateTwoSourceSeparatorEntity()
    {
        return new EntityModel
        {
            ClassName = "UserRecord",
            Namespace = "TestNamespace",
            TableName = "users",
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
                        SourceProperties = new[] { "TenantId", "UserId" },
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
                    PropertyName = "UserId",
                    AttributeName = "userId",
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
    /// Creates a separator-based entity with three string extracted properties.
    /// Separator = "#", sources = ["Year", "Month", "Label"], no Format.
    /// </summary>
    private static EntityModel CreateThreeSourceSeparatorEntity()
    {
        return new EntityModel
        {
            ClassName = "EventRecord",
            Namespace = "TestNamespace",
            TableName = "events",
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
                        SourceProperties = new[] { "Year", "Month", "Label" },
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
                    PropertyName = "Year",
                    AttributeName = "year",
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
                    PropertyName = "Month",
                    AttributeName = "month",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 1,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Label",
                    AttributeName = "label",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 2,
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
    /// Creates a separator-based entity with a string and an int extracted property.
    /// Separator = "#", sources = ["Region", "Year"], no Format.
    /// Region is string (index 0), Year is int (index 1).
    /// </summary>
    private static EntityModel CreateIntExtractedPropertyEntity()
    {
        return new EntityModel
        {
            ClassName = "MetricRecord",
            Namespace = "TestNamespace",
            TableName = "metrics",
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
                        SourceProperties = new[] { "Region", "Year" },
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
                    PropertyName = "Region",
                    AttributeName = "region",
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
                    PropertyName = "Year",
                    AttributeName = "year",
                    PropertyType = "int",
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
    /// Creates a separator-based entity with a string and an enum extracted property.
    /// Separator = "#", sources = ["Category", "Priority"], no Format.
    /// Category is string (index 0), Priority is TaskPriority enum (index 1).
    /// </summary>
    private static EntityModel CreateEnumExtractedPropertyEntity()
    {
        return new EntityModel
        {
            ClassName = "TaskRecord",
            Namespace = "TestNamespace",
            TableName = "tasks",
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
                        SourceProperties = new[] { "Category", "Priority" },
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
                    PropertyName = "Category",
                    AttributeName = "category",
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
                    PropertyName = "Priority",
                    AttributeName = "priority",
                    PropertyType = "TaskPriority",
                    IsEnum = true,
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

    #endregion
}
