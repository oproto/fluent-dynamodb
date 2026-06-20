using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug condition exploration tests for the extracted property type conversion fix.
/// These tests encode the EXPECTED behavior (proper type conversion for non-string extracted properties)
/// and are expected to FAIL on unfixed code — failure confirms the bug exists.
///
/// Bug Condition: Enum and numeric extracted properties generate uncompilable code because:
/// 1. MapperGenerator.GenerateExtractedKeyLogic always assigns raw string from Split() without conversion
/// 2. KeysGenerator.IsEnumType uses a name-based heuristic that misses enums like "SnsSubscriptionTopic"
///
/// **Feature: extracted-property-type-conversion, Property 1 &amp; 2: Bug Condition**
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class ExtractedPropertyTypeConversionBugExplorationTests
{
    /// <summary>
    /// Test 1: Entity with [Extracted("Topic", 0)] on a property of type SnsSubscriptionTopic (enum).
    /// Assert generated FromDynamoDb contains Enum.Parse&lt;SnsSubscriptionTopic&gt;(topicParts[0])
    /// rather than bare topicParts[0].
    /// </summary>
    [Fact]
    public void EnumExtractedProperty_FromDynamoDb_ShouldContainEnumParse()
    {
        // Arrange
        var entity = CreateEntityWithEnumExtractedProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - the generated FromDynamoDb should use Enum.Parse for enum extraction
        result.Should().Contain("Enum.Parse<SnsSubscriptionTopic>(topicParts[0])",
            "Generated FromDynamoDb should convert string to enum using Enum.Parse<SnsSubscriptionTopic>, " +
            "but the bug causes it to emit bare 'topicParts[0]' which is a string-to-enum assignment (CS0029)");
    }

    /// <summary>
    /// Test 2: Entity with [Extracted("Topic", 0)] on enum property.
    /// Assert generated ExtractTopicComponents returns Enum.Parse&lt;SnsSubscriptionTopic&gt;(parts[0])
    /// rather than bare parts[0].
    /// </summary>
    [Fact]
    public void EnumExtractedProperty_ExtractionHelper_ShouldContainEnumParse()
    {
        // Arrange
        var entity = CreateEntityWithEnumExtractedProperty();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - the generated ExtractTopicComponents should use Enum.Parse for enum extraction
        result.Should().Contain("Enum.Parse<SnsSubscriptionTopic>(parts[0])",
            "Generated ExtractTopicComponents should convert string to enum using Enum.Parse<SnsSubscriptionTopic>, " +
            "but the bug causes it to emit bare 'parts[0]' because IsEnumType doesn't match 'SnsSubscriptionTopic'");
    }

    /// <summary>
    /// Test 3: Entity with [Extracted("Pk", 0)] on a property of type int.
    /// Assert generated FromDynamoDb contains int.Parse(pkParts[0]) rather than bare pkParts[0].
    /// </summary>
    [Fact]
    public void IntExtractedProperty_FromDynamoDb_ShouldContainIntParse()
    {
        // Arrange
        var entity = CreateEntityWithIntExtractedProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - the generated FromDynamoDb should use int.Parse for numeric extraction
        result.Should().Contain("int.Parse(pkParts[0])",
            "Generated FromDynamoDb should convert string to int using int.Parse, " +
            "but the bug causes it to emit bare 'pkParts[0]' which is a string-to-int assignment (CS0029)");
    }

    /// <summary>
    /// Test 4: Entity with [Extracted("Pk", 0)] on int property.
    /// Assert generated ExtractPkComponents returns int.Parse(parts[0]) rather than bare parts[0].
    /// </summary>
    [Fact]
    public void IntExtractedProperty_ExtractionHelper_ShouldContainIntParse()
    {
        // Arrange
        var entity = CreateEntityWithIntExtractedProperty();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - the generated ExtractPkComponents should use int.Parse for numeric extraction
        result.Should().Contain("int.Parse(parts[0])",
            "Generated ExtractPkComponents should convert string to int using int.Parse, " +
            "but the bug causes it to emit bare 'parts[0]' which doesn't match the int return type");
    }

    /// <summary>
    /// Test 5: Entity with multiple extracted properties from one source —
    /// int Year at index 0, int Month at index 1, string Label at index 2.
    /// Assert the tuple return has int.Parse(parts[0]), int.Parse(parts[1]), and parts[2] (string untouched).
    /// </summary>
    [Fact]
    public void MultipleExtractedProperties_ExtractionHelper_ShouldHaveCorrectConversions()
    {
        // Arrange
        var entity = CreateEntityWithMultipleMixedExtractedProperties();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - verify each component in the extraction helper has correct conversion
        result.Should().Contain("int.Parse(parts[0])",
            "Year (int at index 0) should use int.Parse conversion");
        result.Should().Contain("int.Parse(parts[1])",
            "Month (int at index 1) should use int.Parse conversion");
        result.Should().Contain("parts[2]",
            "Label (string at index 2) should use direct assignment without conversion");
    }

    /// <summary>
    /// Test 6: Entity with nullable enum [Extracted] property (e.g., SnsSubscriptionTopic?).
    /// Assert proper nullable handling in generated code.
    /// </summary>
    [Fact]
    public void NullableEnumExtractedProperty_ExtractionHelper_ShouldContainEnumParse()
    {
        // Arrange
        var entity = CreateEntityWithNullableEnumExtractedProperty();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - the generated code should still contain Enum.Parse for the nullable enum
        result.Should().Contain("Enum.Parse<SnsSubscriptionTopic>(parts[0])",
            "Generated ExtractTopicComponents should convert string to nullable enum using Enum.Parse<SnsSubscriptionTopic>, " +
            "but the bug causes it to emit bare 'parts[0]' because IsEnumType doesn't match 'SnsSubscriptionTopic?'");
    }

    #region Helper Methods

    private static EntityModel CreateEntityWithEnumExtractedProperty()
    {
        return new EntityModel
        {
            ClassName = "Subscription",
            Namespace = "TestNamespace",
            TableName = "subscriptions",
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
                    PropertyName = "Topic",
                    AttributeName = "topic",
                    PropertyType = "string",
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "TopicType", "TopicId" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "TopicType",
                    AttributeName = "topicType",
                    PropertyType = "SnsSubscriptionTopic",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Topic",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "TopicId",
                    AttributeName = "topicId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Topic",
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

    private static EntityModel CreateEntityWithIntExtractedProperty()
    {
        return new EntityModel
        {
            ClassName = "Event",
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
                        SourceProperties = new[] { "Year", "Label" },
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
                        Index = 0,
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

    private static EntityModel CreateEntityWithMultipleMixedExtractedProperties()
    {
        return new EntityModel
        {
            ClassName = "Record",
            Namespace = "TestNamespace",
            TableName = "records",
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
                    PropertyName = "Year",
                    AttributeName = "year",
                    PropertyType = "int",
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
                    PropertyType = "int",
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

    private static EntityModel CreateEntityWithNullableEnumExtractedProperty()
    {
        return new EntityModel
        {
            ClassName = "Subscription",
            Namespace = "TestNamespace",
            TableName = "subscriptions",
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
                    PropertyName = "Topic",
                    AttributeName = "topic",
                    PropertyType = "string",
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "TopicType", "TopicId" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "TopicType",
                    AttributeName = "topicType",
                    PropertyType = "SnsSubscriptionTopic?",
                    IsNullable = true,
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Topic",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "TopicId",
                    AttributeName = "topicId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Topic",
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
