using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.UnitTests.MetadataTests;

namespace Oproto.FluentDynamoDb.UnitTests.Mapper;

/// <summary>
/// Tests for the generated ToDynamoDb overloads on source-generated entities.
/// Verifies that both the existing overload (options only) and the new overload
/// (options + KeyInputMode) produce consistent results, and that backward compatibility
/// is maintained.
/// Requirements: 7.1, 7.4, 8.1, 8.3
/// </summary>
public class ToDynamoDbOverloadTests
{
    [Fact]
    public void BothOverloadsProduceConsistentResults_WhenModeIsDefault()
    {
        // Arrange — KeyFormatTestEntity has PK prefix "TEST" and SK prefix "SK"
        var entity = new KeyFormatTestEntity
        {
            Pk = "myValue",
            Sk = "sortValue",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act — call both overloads
        var resultExisting = KeyFormatTestEntity.ToDynamoDb(entity, options);
        var resultNew = KeyFormatTestEntity.ToDynamoDb(entity, options, KeyInputMode.Default);

        // Assert — both should produce the same output
        resultExisting.Should().BeEquivalentTo(resultNew);
    }

    [Fact]
    public void BothOverloadsProduceConsistentResults_WhenModeIsDefault_WithAlreadyPrefixedValue()
    {
        // Arrange — value already has the prefix "TEST#"
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#myValue",
            Sk = "SK#sortValue",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var resultExisting = KeyFormatTestEntity.ToDynamoDb(entity, options);
        var resultNew = KeyFormatTestEntity.ToDynamoDb(entity, options, KeyInputMode.Default);

        // Assert — both should produce the same output (Auto mode detects prefix already present)
        resultExisting.Should().BeEquivalentTo(resultNew);
    }

    [Fact]
    public void ExistingOverload_DelegatesCorrectly_BackwardCompatibility()
    {
        // Arrange — the existing overload (without KeyInputMode) should resolve to Auto
        // which is the default. Passing a raw value should get prefix prepended.
        var entity = new KeyFormatTestEntity
        {
            Pk = "rawValue",
            Sk = "rawSort",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var result = KeyFormatTestEntity.ToDynamoDb(entity, options);

        // Assert — the PK should have prefix "TEST#" prepended (Auto mode, not already prefixed)
        result["pk"].S.Should().Be("TEST#rawValue");
        // SK should have prefix "SK#" prepended
        result["sk"].S.Should().Be("SK#rawSort");
        // Non-key property should be unchanged
        result["name"].S.Should().Be("TestName");
    }

    [Fact]
    public void ExistingOverload_PassesThroughAlreadyPrefixedValues()
    {
        // Arrange — backward compatibility: values that already have the prefix pass through
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#existingValue",
            Sk = "SK#existingSort",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var result = KeyFormatTestEntity.ToDynamoDb(entity, options);

        // Assert — Auto mode detects prefix is present and passes through unchanged
        result["pk"].S.Should().Be("TEST#existingValue");
        result["sk"].S.Should().Be("SK#existingSort");
    }

    [Fact]
    public void NullOptionsHandling_DoesNotThrow()
    {
        // Arrange
        var entity = new KeyFormatTestEntity
        {
            Pk = "someValue",
            Sk = "someSort",
            Name = "TestName"
        };

        // Act — passing null options should not throw
        var resultExisting = KeyFormatTestEntity.ToDynamoDb(entity, null);
        var resultNew = KeyFormatTestEntity.ToDynamoDb(entity, null, KeyInputMode.Default);

        // Assert — should produce results without exceptions
        resultExisting.Should().NotBeNull();
        resultNew.Should().NotBeNull();
        resultExisting.Should().BeEquivalentTo(resultNew);
    }

    [Fact]
    public void NullOptionsHandling_WithExplicitAutoMode_DoesNotThrow()
    {
        // Arrange
        var entity = new KeyFormatTestEntity
        {
            Pk = "value",
            Sk = "sort",
            Name = "Test"
        };

        // Act — null options with explicit Auto mode
        var result = KeyFormatTestEntity.ToDynamoDb(entity, null, KeyInputMode.Auto);

        // Assert — should still apply prefix (Auto resolves from new FluentDynamoDbOptions())
        result.Should().NotBeNull();
        result["pk"].S.Should().Be("TEST#value");
        result["sk"].S.Should().Be("SK#sort");
    }

    [Fact]
    public void NewOverload_WithRawMode_PassesValuesUnchanged()
    {
        // Arrange
        var entity = new KeyFormatTestEntity
        {
            Pk = "rawValue",
            Sk = "rawSort",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var result = KeyFormatTestEntity.ToDynamoDb(entity, options, KeyInputMode.Raw);

        // Assert — Raw mode passes values through without prefix
        result["pk"].S.Should().Be("rawValue");
        result["sk"].S.Should().Be("rawSort");
    }

    [Fact]
    public void NewOverload_WithValueMode_AlwaysPrependsPrefix()
    {
        // Arrange — even if value already has prefix, Value mode always prepends
        var entity = new KeyFormatTestEntity
        {
            Pk = "TEST#alreadyPrefixed",
            Sk = "SK#alreadyPrefixed",
            Name = "TestName"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var result = KeyFormatTestEntity.ToDynamoDb(entity, options, KeyInputMode.Value);

        // Assert — Value mode always prepends the prefix
        result["pk"].S.Should().Be("TEST#TEST#alreadyPrefixed");
        result["sk"].S.Should().Be("SK#SK#alreadyPrefixed");
    }

    [Fact]
    public void CustomSeparatorEntity_BothOverloadsConsistent()
    {
        // Arrange — KeyFormatCustomSeparatorTestEntity uses PK prefix "CUSTOM" separator "_"
        // and SK prefix "DATA" separator ":"
        var entity = new KeyFormatCustomSeparatorTestEntity
        {
            Pk = "myId",
            Sk = "mySort",
            Value = "TestValue"
        };
        var options = new FluentDynamoDbOptions();

        // Act
        var resultExisting = KeyFormatCustomSeparatorTestEntity.ToDynamoDb(entity, options);
        var resultNew = KeyFormatCustomSeparatorTestEntity.ToDynamoDb(entity, options, KeyInputMode.Default);

        // Assert
        resultExisting.Should().BeEquivalentTo(resultNew);
        // Verify prefix is applied with custom separator
        resultExisting["pk"].S.Should().Be("CUSTOM_myId");
        resultExisting["sk"].S.Should().Be("DATA:mySort");
    }
}
