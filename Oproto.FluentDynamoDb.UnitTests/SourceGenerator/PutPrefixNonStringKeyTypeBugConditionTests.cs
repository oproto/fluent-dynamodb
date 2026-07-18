using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Bug condition exploration test for non-string key types with prefix generating non-compilable code.
///
/// The source generator's MapperGenerator.GenerateKeyPrefixApplication emits code that passes
/// non-string key property values directly to KeyPrefixHelper.ApplyKeyPrefix(string, ...),
/// which requires a string first argument. For non-string types (enum, DateTime, Guid, int),
/// this produces uncompilable generated code because there's no implicit conversion to string.
///
/// The correct behavior is to convert the value to a string using GetValueExpression logic:
/// - Enum → .ToString()
/// - DateTime → .ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
/// - Guid → .ToString()
/// - Numeric → .ToString()
///
/// On UNFIXED code, these tests are EXPECTED TO FAIL, confirming the bug exists.
/// The generated code will contain raw typed values (e.g., typedEntity.Topic) instead of
/// converted string expressions (e.g., typedEntity.Topic.ToString()).
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
public class PutPrefixNonStringKeyTypeBugConditionTests
{
    /// <summary>
    /// Creates an entity with an enum sort key that has a configured prefix.
    /// Bug condition: enum type + prefix → should call .ToString() before ApplyKeyPrefix.
    /// </summary>
    private static EntityModel CreateEnumKeyEntity()
    {
        return new EntityModel
        {
            ClassName = "SnsSubscriptionEntity",
            Namespace = "TestNamespace",
            TableName = "subscriptions",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "SUB", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Topic",
                    AttributeName = "sk",
                    PropertyType = "SnsSubscriptionTopic",
                    IsSortKey = true,
                    IsEnum = true,
                    KeyFormat = new KeyFormatModel { Prefix = "TOPIC", Separator = "#" }
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with a DateTime sort key that has a configured prefix.
    /// Bug condition: DateTime type + prefix → should call .ToString("yyyy-MM-ddTHH:mm:ss.fffZ") before ApplyKeyPrefix.
    /// </summary>
    private static EntityModel CreateDateTimeKeyEntity()
    {
        return new EntityModel
        {
            ClassName = "EventEntity",
            Namespace = "TestNamespace",
            TableName = "events",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "EVT", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "CreatedAt",
                    AttributeName = "sk",
                    PropertyType = "DateTime",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "DATE", Separator = "#" }
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with a Guid partition key that has a configured prefix.
    /// Bug condition: Guid type + prefix → should call .ToString() before ApplyKeyPrefix.
    /// </summary>
    private static EntityModel CreateGuidKeyEntity()
    {
        return new EntityModel
        {
            ClassName = "GuidEntity",
            Namespace = "TestNamespace",
            TableName = "entities",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "EntityId",
                    AttributeName = "pk",
                    PropertyType = "Guid",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ID", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity with a numeric (int) sort key that has a configured prefix.
    /// Bug condition: int type + prefix → should call .ToString() before ApplyKeyPrefix.
    /// </summary>
    private static EntityModel CreateNumericKeyEntity()
    {
        return new EntityModel
        {
            ClassName = "SequenceEntity",
            Namespace = "TestNamespace",
            TableName = "sequences",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "SEQ", Separator = "#" }
                },
                new PropertyModel
                {
                    PropertyName = "Sequence",
                    AttributeName = "sk",
                    PropertyType = "int",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "NUM", Separator = "#" }
                }
            }
        };
    }

    /// <summary>
    /// Enum key with prefix: [SortKey(Prefix = "TOPIC")] public SnsSubscriptionTopic Topic
    /// 
    /// Expected: Generated code contains ApplyKeyPrefix(typedEntity.Topic.ToString(), "TOPIC", "#", resolvedMode)
    /// On unfixed code: Generated code contains ApplyKeyPrefix(typedEntity.Topic, "TOPIC", "#", resolvedMode)
    /// which fails to compile because SnsSubscriptionTopic is not a string.
    ///
    /// Validates: Requirement 1.1
    /// </summary>
    [Fact]
    public void EnumKeyWithPrefix_GeneratedCode_ShouldConvertToString_BeforeApplyKeyPrefix()
    {
        // Arrange
        var entity = CreateEnumKeyEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must convert enum to string before passing to ApplyKeyPrefix
        generatedSource.Should().Contain(
            "ApplyKeyPrefix(typedEntity.Topic.ToString()",
            "Generated code should convert enum value to string via .ToString() before passing to ApplyKeyPrefix. " +
            "Without conversion, the code passes a non-string type directly to ApplyKeyPrefix(string, ...) causing a compilation error.");
    }

    /// <summary>
    /// DateTime key with prefix: [SortKey(Prefix = "DATE")] public DateTime CreatedAt
    /// 
    /// Expected: Generated code contains ApplyKeyPrefix(typedEntity.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), "DATE", "#", resolvedMode)
    /// On unfixed code: Generated code contains ApplyKeyPrefix(typedEntity.CreatedAt, "DATE", "#", resolvedMode)
    /// which fails to compile because DateTime is not a string.
    ///
    /// Validates: Requirement 1.2
    /// </summary>
    [Fact]
    public void DateTimeKeyWithPrefix_GeneratedCode_ShouldConvertWithFormat_BeforeApplyKeyPrefix()
    {
        // Arrange
        var entity = CreateDateTimeKeyEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must convert DateTime with ISO format before passing to ApplyKeyPrefix
        generatedSource.Should().Contain(
            "ApplyKeyPrefix(typedEntity.CreatedAt.ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\")",
            "Generated code should convert DateTime value using .ToString(\"yyyy-MM-ddTHH:mm:ss.fffZ\") before passing to ApplyKeyPrefix. " +
            "Without conversion, the code passes a DateTime directly to ApplyKeyPrefix(string, ...) causing a compilation error.");
    }

    /// <summary>
    /// Guid key with prefix: [PartitionKey(Prefix = "ID")] public Guid EntityId
    /// 
    /// Expected: Generated code contains ApplyKeyPrefix(typedEntity.EntityId.ToString(), "ID", "#", resolvedMode)
    /// On unfixed code: Generated code contains ApplyKeyPrefix(typedEntity.EntityId, "ID", "#", resolvedMode)
    /// which fails to compile because Guid is not a string.
    ///
    /// Validates: Requirement 1.3
    /// </summary>
    [Fact]
    public void GuidKeyWithPrefix_GeneratedCode_ShouldConvertToString_BeforeApplyKeyPrefix()
    {
        // Arrange
        var entity = CreateGuidKeyEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must convert Guid to string before passing to ApplyKeyPrefix
        generatedSource.Should().Contain(
            "ApplyKeyPrefix(typedEntity.EntityId.ToString()",
            "Generated code should convert Guid value to string via .ToString() before passing to ApplyKeyPrefix. " +
            "Without conversion, the code passes a Guid directly to ApplyKeyPrefix(string, ...) causing a compilation error.");
    }

    /// <summary>
    /// Numeric key with prefix: [SortKey(Prefix = "NUM")] public int Sequence
    /// 
    /// Expected: Generated code contains ApplyKeyPrefix(typedEntity.Sequence.ToString(), "NUM", "#", resolvedMode)
    /// On unfixed code: Generated code contains ApplyKeyPrefix(typedEntity.Sequence, "NUM", "#", resolvedMode)
    /// which fails to compile because int is not a string.
    ///
    /// Validates: Requirement 1.4
    /// </summary>
    [Fact]
    public void NumericKeyWithPrefix_GeneratedCode_ShouldConvertToString_BeforeApplyKeyPrefix()
    {
        // Arrange
        var entity = CreateNumericKeyEntity();

        // Act
        var generatedSource = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert — generated code must convert int to string before passing to ApplyKeyPrefix
        // Note: "Sequence" is a DynamoDB reserved word so the source generator escapes it with @
        generatedSource.Should().Contain(
            "ApplyKeyPrefix(typedEntity.@Sequence.ToString()",
            "Generated code should convert numeric value to string via .ToString() before passing to ApplyKeyPrefix. " +
            "Without conversion, the code passes an int directly to ApplyKeyPrefix(string, ...) causing a compilation error.");
    }
}
