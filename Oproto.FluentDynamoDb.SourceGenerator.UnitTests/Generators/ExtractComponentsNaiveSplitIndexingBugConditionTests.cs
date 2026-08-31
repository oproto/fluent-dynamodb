using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for the extract components naive split indexing fix.
/// These tests encode the EXPECTED behavior (correct split indices for format-string computed keys)
/// and are expected to FAIL on unfixed code — failure confirms the bug exists.
///
/// Bug Condition: When a computed key uses Format = "TENANT#{0}#EXTERNAL_ACCESS", the generated
/// extraction code uses parts[0] (the placeholder index) instead of parts[1] (the split index).
/// After splitting "TENANT#val#EXTERNAL_ACCESS" on '#', parts[0] is "TENANT" (a constant),
/// not the variable value at parts[1].
///
/// **Feature: extract-components-naive-split-indexing, Property 1: Bug Condition**
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class ExtractComponentsNaiveSplitIndexingBugConditionTests
{
    /// <summary>
    /// Test 1: KeysGenerator single variable with leading constant.
    /// Entity with Format = "TENANT#{0}#EXTERNAL_ACCESS" and [Extracted("Pk", 0)].
    /// The generated ExtractPkComponents should use parts[1] (split index),
    /// NOT parts[0] (placeholder index).
    /// </summary>
    [Fact]
    public void KeysGenerator_SingleVariableWithLeadingConstant_ShouldUseSplitIndex()
    {
        // Arrange
        var entity = CreateSingleVariableEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — the generated code should index into parts[1], not parts[0]
        result.Should().Contain("parts[1]",
            "Format 'TENANT#{0}#EXTERNAL_ACCESS' splits to [TENANT, {0}, EXTERNAL_ACCESS] — " +
            "placeholder {0} is at split index 1, so extraction should use parts[1]");

        result.Should().NotContain("parts[0]",
            "parts[0] is the constant 'TENANT', not the variable value — " +
            "the bug uses placeholder index 0 directly as split index");
    }

    /// <summary>
    /// Test 2: KeysGenerator multiple variables with interspersed constants.
    /// Entity with Format = "TENANT#{0}#SHARE#RESOURCE#{1}#{2}" and three extracted properties.
    /// Split: ["TENANT", "{0}", "SHARE", "RESOURCE", "{1}", "{2}"]
    /// Mapping: {0}→1, {1}→4, {2}→5
    /// </summary>
    [Fact]
    public void KeysGenerator_MultipleVariablesWithInterspersedConstants_ShouldUseCorrectSplitIndices()
    {
        // Arrange
        var entity = CreateMultiVariableEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — verify correct split indices for all three placeholders
        result.Should().Contain("parts[1]",
            "Placeholder {0} is at split index 1 in 'TENANT#{0}#SHARE#RESOURCE#{1}#{2}'");
        result.Should().Contain("parts[4]",
            "Placeholder {1} is at split index 4 in 'TENANT#{0}#SHARE#RESOURCE#{1}#{2}'");
        result.Should().Contain("parts[5]",
            "Placeholder {2} is at split index 5 in 'TENANT#{0}#SHARE#RESOURCE#{1}#{2}'");
    }

    /// <summary>
    /// Test 3: KeysGenerator format specifier.
    /// Entity with Format = "SEQ#{0:D4}" and [Extracted("Sk", 0)].
    /// Split: ["SEQ", "{0:D4}"] — placeholder {0} is at split index 1.
    /// </summary>
    [Fact]
    public void KeysGenerator_FormatSpecifier_ShouldUseSplitIndex()
    {
        // Arrange
        var entity = CreateFormatSpecifierEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — the generated code should index into parts[1], not parts[0]
        result.Should().Contain("parts[1]",
            "Format 'SEQ#{0:D4}' splits to [SEQ, {0:D4}] — " +
            "placeholder {0} is at split index 1, so extraction should use parts[1]");

        result.Should().NotContain("parts[0]",
            "parts[0] is the constant 'SEQ', not the variable value — " +
            "the bug uses placeholder index 0 directly as split index");
    }

    /// <summary>
    /// Test 4: MapperGenerator single variable with leading constant.
    /// Same entity as Test 1 — assert FromDynamoDb hydration code contains pkParts[1], NOT pkParts[0].
    /// </summary>
    [Fact]
    public void MapperGenerator_SingleVariableWithLeadingConstant_ShouldUseSplitIndex()
    {
        // Arrange
        var entity = CreateSingleVariableEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — the generated FromDynamoDb should use pkParts[1], not pkParts[0]
        result.Should().Contain("pkParts[1]",
            "Format 'TENANT#{0}#EXTERNAL_ACCESS' splits to [TENANT, {0}, EXTERNAL_ACCESS] — " +
            "hydration should read pkParts[1] for the variable value");

        result.Should().NotContain("pkParts[0]",
            "pkParts[0] is the constant 'TENANT', not the variable value — " +
            "the bug uses placeholder index 0 directly as split index in FromDynamoDb");
    }

    /// <summary>
    /// Test 5: MapperGenerator multiple variables with interspersed constants.
    /// Same entity as Test 2 — assert FromDynamoDb hydration code uses
    /// pkParts[1], pkParts[4], pkParts[5].
    /// </summary>
    [Fact]
    public void MapperGenerator_MultipleVariables_ShouldUseCorrectSplitIndices()
    {
        // Arrange
        var entity = CreateMultiVariableEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — verify correct split indices for all three extracted properties
        result.Should().Contain("pkParts[1]",
            "Placeholder {0} → split index 1 in FromDynamoDb hydration");
        result.Should().Contain("pkParts[4]",
            "Placeholder {1} → split index 4 in FromDynamoDb hydration");
        result.Should().Contain("pkParts[5]",
            "Placeholder {2} → split index 5 in FromDynamoDb hydration");
    }

    /// <summary>
    /// Test 6: KeysGenerator bounds check uses max split index.
    /// For the multi-variable entity with Format = "TENANT#{0}#SHARE#RESOURCE#{1}#{2}",
    /// the bounds check should be parts.Length &lt;= 5 (max split index),
    /// NOT parts.Length &lt;= 2 (max placeholder index).
    /// </summary>
    [Fact]
    public void KeysGenerator_BoundsCheck_ShouldUseMaxSplitIndex()
    {
        // Arrange
        var entity = CreateMultiVariableEntity();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert — bounds check should use max split index 5
        result.Should().Contain("parts.Length <= 5",
            "Max split index is 5 (for placeholder {2} at position 5 in " +
            "'TENANT#{0}#SHARE#RESOURCE#{1}#{2}'), so bounds check should be parts.Length <= 5");

        result.Should().NotContain("parts.Length <= 2",
            "parts.Length <= 2 uses the max placeholder index, not the max split index — " +
            "this would not catch arrays too short for the actual positions needed");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an entity with a single extracted property from a format-string computed PK.
    /// Format = "TENANT#{0}#EXTERNAL_ACCESS", Extracted("Pk", 0) → TenantId (string)
    /// </summary>
    private static EntityModel CreateSingleVariableEntity()
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
    /// Creates an entity with multiple extracted properties from a format-string computed PK.
    /// Format = "TENANT#{0}#SHARE#RESOURCE#{1}#{2}"
    /// Split: ["TENANT", "{0}", "SHARE", "RESOURCE", "{1}", "{2}"]
    /// Mapping: {0}→1, {1}→4, {2}→5
    /// </summary>
    private static EntityModel CreateMultiVariableEntity()
    {
        return new EntityModel
        {
            ClassName = "SharedResource",
            Namespace = "TestNamespace",
            TableName = "shared_resources",
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
                        SourceProperties = new[] { "TenantId", "ResourceId", "AccessLevel" },
                        Format = "TENANT#{0}#SHARE#RESOURCE#{1}#{2}",
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
                    PropertyName = "ResourceId",
                    AttributeName = "resourceId",
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
                    PropertyName = "AccessLevel",
                    AttributeName = "accessLevel",
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
    /// Creates an entity with a format specifier in the computed SK.
    /// Format = "SEQ#{0:D4}", Extracted("Sk", 0) → Sequence (int)
    /// Split: ["SEQ", "{0:D4}"] — placeholder {0} is at split index 1.
    /// </summary>
    private static EntityModel CreateFormatSpecifierEntity()
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
