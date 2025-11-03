using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class AttributeValueConverterFormatStringTests
{
    #region Dictionary in Format String Tests

    [Fact]
    public void AddFormattedValue_StringDictionary_NonEmpty_ShouldAddMapParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dict = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        var paramName = helper.AddFormattedValue(dict);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].M.Should().NotBeNull();
        helper.AttributeValues[":p0"].M.Should().HaveCount(2);
        helper.AttributeValues[":p0"].M["key1"].S.Should().Be("value1");
        helper.AttributeValues[":p0"].M["key2"].S.Should().Be("value2");
    }

    [Fact]
    public void AddFormattedValue_AttributeValueDictionary_NonEmpty_ShouldAddMapParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dict = new Dictionary<string, AttributeValue>
        {
            { "name", new AttributeValue { S = "Product" } },
            { "price", new AttributeValue { N = "99.99" } }
        };

        // Act
        var paramName = helper.AddFormattedValue(dict);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].M.Should().NotBeNull();
        helper.AttributeValues[":p0"].M.Should().HaveCount(2);
        helper.AttributeValues[":p0"].M["name"].S.Should().Be("Product");
        helper.AttributeValues[":p0"].M["price"].N.Should().Be("99.99");
    }

    [Fact]
    public void AddFormattedValue_StringDictionary_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dict = new Dictionary<string, string>();

        // Act
        var action = () => helper.AddFormattedValue(dict);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*Dictionary*")
            .WithMessage("*DynamoDB does not support empty Maps*")
            .WithMessage("*:p0*");
    }

    [Fact]
    public void AddFormattedValue_AttributeValueDictionary_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dict = new Dictionary<string, AttributeValue>();

        // Act
        var action = () => helper.AddFormattedValue(dict);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*Dictionary*")
            .WithMessage("*DynamoDB does not support empty Maps*");
    }

    #endregion

    #region HashSet in Format String Tests

    [Fact]
    public void AddFormattedValue_StringHashSet_NonEmpty_ShouldAddStringSetParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<string> { "apple", "banana", "cherry" };

        // Act
        var paramName = helper.AddFormattedValue(set);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].SS.Should().NotBeNull();
        helper.AttributeValues[":p0"].SS.Should().HaveCount(3);
        helper.AttributeValues[":p0"].SS.Should().Contain("apple");
        helper.AttributeValues[":p0"].SS.Should().Contain("banana");
        helper.AttributeValues[":p0"].SS.Should().Contain("cherry");
    }

    [Fact]
    public void AddFormattedValue_IntHashSet_NonEmpty_ShouldAddNumberSetParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<int> { 10, 20, 30 };

        // Act
        var paramName = helper.AddFormattedValue(set);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].NS.Should().NotBeNull();
        helper.AttributeValues[":p0"].NS.Should().HaveCount(3);
        helper.AttributeValues[":p0"].NS.Should().Contain("10");
        helper.AttributeValues[":p0"].NS.Should().Contain("20");
        helper.AttributeValues[":p0"].NS.Should().Contain("30");
    }

    [Fact]
    public void AddFormattedValue_LongHashSet_NonEmpty_ShouldAddNumberSetParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<long> { 1000000000L, 2000000000L };

        // Act
        var paramName = helper.AddFormattedValue(set);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].NS.Should().NotBeNull();
        helper.AttributeValues[":p0"].NS.Should().HaveCount(2);
        helper.AttributeValues[":p0"].NS.Should().Contain("1000000000");
        helper.AttributeValues[":p0"].NS.Should().Contain("2000000000");
    }

    [Fact]
    public void AddFormattedValue_DecimalHashSet_NonEmpty_ShouldAddNumberSetParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<decimal> { 10.5m, 20.75m };

        // Act
        var paramName = helper.AddFormattedValue(set);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].NS.Should().NotBeNull();
        helper.AttributeValues[":p0"].NS.Should().HaveCount(2);
        helper.AttributeValues[":p0"].NS.Should().Contain("10.5");
        helper.AttributeValues[":p0"].NS.Should().Contain("20.75");
    }

    [Fact]
    public void AddFormattedValue_BinaryHashSet_NonEmpty_ShouldAddBinarySetParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<byte[]>
        {
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 }
        };

        // Act
        var paramName = helper.AddFormattedValue(set);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].BS.Should().NotBeNull();
        helper.AttributeValues[":p0"].BS.Should().HaveCount(2);
    }

    [Fact]
    public void AddFormattedValue_StringHashSet_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<string>();

        // Act
        var action = () => helper.AddFormattedValue(set);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*HashSet*")
            .WithMessage("*DynamoDB does not support empty Sets*")
            .WithMessage("*:p0*");
    }

    [Fact]
    public void AddFormattedValue_IntHashSet_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<int>();

        // Act
        var action = () => helper.AddFormattedValue(set);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*HashSet*")
            .WithMessage("*DynamoDB does not support empty Sets*");
    }

    [Fact]
    public void AddFormattedValue_DecimalHashSet_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var set = new HashSet<decimal>();

        // Act
        var action = () => helper.AddFormattedValue(set);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*HashSet*")
            .WithMessage("*DynamoDB does not support empty Sets*");
    }

    #endregion

    #region List in Format String Tests

    [Fact]
    public void AddFormattedValue_StringList_NonEmpty_ShouldAddListParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<string> { "first", "second", "third" };

        // Act
        var paramName = helper.AddFormattedValue(list);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].L.Should().NotBeNull();
        helper.AttributeValues[":p0"].L.Should().HaveCount(3);
        helper.AttributeValues[":p0"].L[0].S.Should().Be("first");
        helper.AttributeValues[":p0"].L[1].S.Should().Be("second");
        helper.AttributeValues[":p0"].L[2].S.Should().Be("third");
    }

    [Fact]
    public void AddFormattedValue_IntList_NonEmpty_ShouldAddListParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<int> { 100, 200, 300 };

        // Act
        var paramName = helper.AddFormattedValue(list);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].L.Should().NotBeNull();
        helper.AttributeValues[":p0"].L.Should().HaveCount(3);
        helper.AttributeValues[":p0"].L[0].N.Should().Be("100");
        helper.AttributeValues[":p0"].L[1].N.Should().Be("200");
        helper.AttributeValues[":p0"].L[2].N.Should().Be("300");
    }

    [Fact]
    public void AddFormattedValue_LongList_NonEmpty_ShouldAddListParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<long> { 1000000000L, 2000000000L };

        // Act
        var paramName = helper.AddFormattedValue(list);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].L.Should().NotBeNull();
        helper.AttributeValues[":p0"].L.Should().HaveCount(2);
        helper.AttributeValues[":p0"].L[0].N.Should().Be("1000000000");
        helper.AttributeValues[":p0"].L[1].N.Should().Be("2000000000");
    }

    [Fact]
    public void AddFormattedValue_DecimalList_NonEmpty_ShouldAddListParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<decimal> { 10.5m, 20.75m };

        // Act
        var paramName = helper.AddFormattedValue(list);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].L.Should().NotBeNull();
        helper.AttributeValues[":p0"].L.Should().HaveCount(2);
        helper.AttributeValues[":p0"].L[0].N.Should().Be("10.5");
        helper.AttributeValues[":p0"].L[1].N.Should().Be("20.75");
    }

    [Fact]
    public void AddFormattedValue_BoolList_NonEmpty_ShouldAddListParameter()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<bool> { true, false, true };

        // Act
        var paramName = helper.AddFormattedValue(list);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].L.Should().NotBeNull();
        helper.AttributeValues[":p0"].L.Should().HaveCount(3);
        helper.AttributeValues[":p0"].L[0].BOOL.Should().BeTrue();
        helper.AttributeValues[":p0"].L[1].BOOL.Should().BeFalse();
        helper.AttributeValues[":p0"].L[2].BOOL.Should().BeTrue();
    }

    [Fact]
    public void AddFormattedValue_StringList_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<string>();

        // Act
        var action = () => helper.AddFormattedValue(list);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*List*")
            .WithMessage("*DynamoDB does not support empty Lists*")
            .WithMessage("*:p0*");
    }

    [Fact]
    public void AddFormattedValue_IntList_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<int>();

        // Act
        var action = () => helper.AddFormattedValue(list);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*List*")
            .WithMessage("*DynamoDB does not support empty Lists*");
    }

    [Fact]
    public void AddFormattedValue_BoolList_Empty_ShouldThrowArgumentException()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var list = new List<bool>();

        // Act
        var action = () => helper.AddFormattedValue(list);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*empty*List*")
            .WithMessage("*DynamoDB does not support empty Lists*");
    }

    #endregion

    #region Error Message Validation Tests

    [Fact]
    public void AddFormattedValue_EmptyCollection_ErrorMessageShouldIncludeParameterName()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var emptyDict = new Dictionary<string, string>();

        // Act
        var action = () => helper.AddFormattedValue(emptyDict);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*:p0*"); // Should include the parameter name
    }

    [Fact]
    public void AddFormattedValue_EmptyCollection_ErrorMessageShouldIncludeTypeName()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var emptySet = new HashSet<string>();

        // Act
        var action = () => helper.AddFormattedValue(emptySet);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*HashSet*"); // Should include the type name
    }

    [Fact]
    public void AddFormattedValue_EmptyCollection_ErrorMessageShouldExplainDynamoDbLimitation()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var emptyList = new List<int>();

        // Act
        var action = () => helper.AddFormattedValue(emptyList);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DynamoDB does not support empty*");
    }

    [Fact]
    public void AddFormattedValue_MultipleEmptyCollections_ShouldIncludeCorrectParameterNames()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        
        // Add a valid parameter first
        helper.AddFormattedValue("valid");

        // Act & Assert - Second parameter should be :p1
        var action = () => helper.AddFormattedValue(new Dictionary<string, string>());
        action.Should().Throw<ArgumentException>()
            .WithMessage("*:p1*");
    }

    #endregion

    #region TTL Format String Tests

    [Fact]
    public void AddFormattedValue_DateTime_WithTtlFormat_ShouldConvertToUnixEpoch()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedEpochSeconds = 1704067200;

        // Act
        var paramName = helper.AddFormattedValue(dateTime, "ttl");

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].N.Should().Be(expectedEpochSeconds.ToString());
    }

    [Fact]
    public void AddFormattedValue_DateTimeOffset_WithTtlFormat_ShouldConvertToUnixEpoch()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dateTimeOffset = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedEpochSeconds = 1704067200;

        // Act
        var paramName = helper.AddFormattedValue(dateTimeOffset, "ttl");

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].N.Should().Be(expectedEpochSeconds.ToString());
    }

    [Fact]
    public void AddFormattedValue_DateTime_WithTtlFormat_CaseInsensitive_ShouldWork()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act - Test various case combinations
        var paramName1 = helper.AddFormattedValue(dateTime, "TTL");
        var paramName2 = helper.AddFormattedValue(dateTime, "Ttl");
        var paramName3 = helper.AddFormattedValue(dateTime, "ttl");

        // Assert - All should produce numeric values (Unix epoch)
        helper.AttributeValues[paramName1].N.Should().NotBeNullOrEmpty();
        helper.AttributeValues[paramName2].N.Should().NotBeNullOrEmpty();
        helper.AttributeValues[paramName3].N.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AddFormattedValue_DateTime_WithoutTtlFormat_ShouldUseISOFormat()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var paramName = helper.AddFormattedValue(dateTime);

        // Assert
        paramName.Should().Be(":p0");
        helper.AttributeValues.Should().HaveCount(1);
        helper.AttributeValues[":p0"].S.Should().NotBeNullOrEmpty();
        helper.AttributeValues[":p0"].S.Should().Contain("2024-01-01");
    }

    #endregion

    #region Multiple Parameters Tests

    [Fact]
    public void AddFormattedValue_MultipleAdvancedTypes_ShouldGenerateUniqueParameterNames()
    {
        // Arrange
        var helper = new AttributeValueInternal();
        var dict = new Dictionary<string, string> { { "key", "value" } };
        var set = new HashSet<string> { "item" };
        var list = new List<int> { 1, 2, 3 };

        // Act
        var param1 = helper.AddFormattedValue(dict);
        var param2 = helper.AddFormattedValue(set);
        var param3 = helper.AddFormattedValue(list);

        // Assert
        param1.Should().Be(":p0");
        param2.Should().Be(":p1");
        param3.Should().Be(":p2");
        helper.AttributeValues.Should().HaveCount(3);
    }

    [Fact]
    public void AddFormattedValue_MixedStandardAndAdvancedTypes_ShouldWorkCorrectly()
    {
        // Arrange
        var helper = new AttributeValueInternal();

        // Act
        var param1 = helper.AddFormattedValue("string value");
        var param2 = helper.AddFormattedValue(new HashSet<int> { 1, 2, 3 });
        var param3 = helper.AddFormattedValue(42);
        var param4 = helper.AddFormattedValue(new List<string> { "a", "b" });

        // Assert
        helper.AttributeValues.Should().HaveCount(4);
        helper.AttributeValues[param1].S.Should().Be("string value");
        helper.AttributeValues[param2].NS.Should().HaveCount(3);
        helper.AttributeValues[param3].N.Should().Be("42");
        helper.AttributeValues[param4].L.Should().HaveCount(2);
    }

    #endregion
}
