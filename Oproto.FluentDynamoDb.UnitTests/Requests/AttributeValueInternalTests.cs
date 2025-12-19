using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Requests;
using System.Globalization;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

public class AttributeValueInternalTests
{
    private readonly AttributeValueInternal _helper = new();

    #region WithValues Tests

    [Fact]
    public void WithValues_Dictionary_ShouldAddAllValues()
    {
        // Arrange
        var values = new Dictionary<string, AttributeValue>
        {
            { ":pk", new AttributeValue { S = "test" } },
            { ":sk", new AttributeValue { N = "123" } }
        };

        // Act
        _helper.WithValues(values);

        // Assert
        _helper.AttributeValues.Should().HaveCount(2);
        _helper.AttributeValues[":pk"].S.Should().Be("test");
        _helper.AttributeValues[":sk"].N.Should().Be("123");
    }

    [Fact]
    public void WithValues_Action_ShouldConfigureValues()
    {
        // Act
        _helper.WithValues(values =>
        {
            values.Add(":pk", new AttributeValue { S = "test" });
            values.Add(":sk", new AttributeValue { N = "456" });
        });

        // Assert
        _helper.AttributeValues.Should().HaveCount(2);
        _helper.AttributeValues[":pk"].S.Should().Be("test");
        _helper.AttributeValues[":sk"].N.Should().Be("456");
    }

    [Fact]
    public void WithValues_EmptyDictionary_ShouldNotAddValues()
    {
        // Arrange
        var values = new Dictionary<string, AttributeValue>();

        // Act
        _helper.WithValues(values);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region WithValue String Tests

    [Fact]
    public void WithValue_String_ConditionalUseTrue_ShouldAddValue()
    {
        // Act
        _helper.WithValue(":name", "John Doe", conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":name"].S.Should().Be("John Doe");
    }

    [Fact]
    public void WithValue_String_Null_ConditionalUseTrue_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":name", (string?)null, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_String_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":name", "John Doe", conditionalUse: false);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_String_EmptyString_ShouldAddEmptyValue()
    {
        // Act
        _helper.WithValue(":name", "", conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":name"].S.Should().Be("");
    }

    #endregion

    #region WithValue Boolean Tests

    [Fact]
    public void WithValue_Boolean_True_ConditionalUseTrue_ShouldAddTrueValue()
    {
        // Act
        _helper.WithValue(":active", true, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":active"].BOOL.Should().BeTrue();
        _helper.AttributeValues[":active"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void WithValue_Boolean_False_ConditionalUseTrue_ShouldAddFalseValue()
    {
        // Act
        _helper.WithValue(":active", false, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":active"].BOOL.Should().BeFalse();
        _helper.AttributeValues[":active"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void WithValue_Boolean_Null_ConditionalUseTrue_ShouldAddFalseValueWithoutBoolSet()
    {
        // Act
        _helper.WithValue(":active", (bool?)null, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":active"].BOOL.Should().BeNull();
        _helper.AttributeValues[":active"].IsBOOLSet.Should().BeFalse();
    }

    [Fact]
    public void WithValue_Boolean_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":active", true, conditionalUse: false);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region WithValue Decimal Tests

    [Fact]
    public void WithValue_Decimal_ConditionalUseTrue_ShouldAddNumericValue()
    {
        // Act
        _helper.WithValue(":amount", 123.45m, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":amount"].N.Should().Be("123.45");
    }

    [Fact]
    public void WithValue_Decimal_Null_ConditionalUseTrue_ShouldAddEmptyNumericValue()
    {
        // Act
        _helper.WithValue(":amount", (decimal?)null, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":amount"].N.Should().Be("");
    }

    [Fact]
    public void WithValue_Decimal_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":amount", 123.45m, conditionalUse: false);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_Decimal_Zero_ShouldAddZeroValue()
    {
        // Act
        _helper.WithValue(":amount", 0m, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":amount"].N.Should().Be("0");
    }

    #endregion

    #region WithValue Dictionary<string, string> Tests

    [Fact]
    public void WithValue_StringDictionary_ConditionalUseTrue_ShouldAddMapValue()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Act
        _helper.WithValue(":metadata", dict, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":metadata"].M.Should().HaveCount(2);
        _helper.AttributeValues[":metadata"].M["key1"].S.Should().Be("value1");
        _helper.AttributeValues[":metadata"].M["key2"].S.Should().Be("value2");
    }

    [Fact]
    public void WithValue_StringDictionary_Empty_ShouldNotAddValue()
    {
        // Arrange
        var dict = new Dictionary<string, string>();

        // Act
        _helper.WithValue(":metadata", dict, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty collections)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_StringDictionary_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Arrange
        var dict = new Dictionary<string, string> { { "key", "value" } };

        // Act
        _helper.WithValue(":metadata", dict, conditionalUse: false);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region WithValue Dictionary<string, AttributeValue> Tests

    [Fact]
    public void WithValue_AttributeValueDictionary_ConditionalUseTrue_ShouldAddMapValue()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>
        {
            { "key1", new AttributeValue { S = "value1" } },
            { "key2", new AttributeValue { N = "123" } }
        };

        // Act
        _helper.WithValue(":complex", dict, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":complex"].M.Should().HaveCount(2);
        _helper.AttributeValues[":complex"].M["key1"].S.Should().Be("value1");
        _helper.AttributeValues[":complex"].M["key2"].N.Should().Be("123");
    }

    [Fact]
    public void WithValue_AttributeValueDictionary_Empty_ShouldNotAddValue()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>();

        // Act
        _helper.WithValue(":complex", dict, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty collections)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_AttributeValueDictionary_ConditionalUseFalse_ShouldNotAddValue()
    {
        // Arrange
        var dict = new Dictionary<string, AttributeValue>
        {
            { "key", new AttributeValue { S = "value" } }
        };

        // Act
        _helper.WithValue(":complex", dict, conditionalUse: false);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region WithValue HashSet Tests

    [Fact]
    public void WithValue_HashSetString_ConditionalUseTrue_ShouldAddStringSetValue()
    {
        // Arrange
        var set = new HashSet<string> { "value1", "value2", "value3" };

        // Act
        _helper.WithValue(":tags", set, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":tags"].SS.Should().HaveCount(3);
        _helper.AttributeValues[":tags"].SS.Should().Contain("value1");
        _helper.AttributeValues[":tags"].SS.Should().Contain("value2");
        _helper.AttributeValues[":tags"].SS.Should().Contain("value3");
    }

    [Fact]
    public void WithValue_HashSetString_Empty_ShouldNotAddValue()
    {
        // Arrange
        var set = new HashSet<string>();

        // Act
        _helper.WithValue(":tags", set, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty sets)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_HashSetString_Null_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":tags", (HashSet<string>?)null, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_HashSetInt_ConditionalUseTrue_ShouldAddNumberSetValue()
    {
        // Arrange
        var set = new HashSet<int> { 1, 2, 3 };

        // Act
        _helper.WithValue(":numbers", set, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":numbers"].NS.Should().HaveCount(3);
        _helper.AttributeValues[":numbers"].NS.Should().Contain("1");
        _helper.AttributeValues[":numbers"].NS.Should().Contain("2");
        _helper.AttributeValues[":numbers"].NS.Should().Contain("3");
    }

    [Fact]
    public void WithValue_HashSetInt_Empty_ShouldNotAddValue()
    {
        // Arrange
        var set = new HashSet<int>();

        // Act
        _helper.WithValue(":numbers", set, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty sets)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_HashSetLong_Empty_ShouldNotAddValue()
    {
        // Arrange
        var set = new HashSet<long>();

        // Act
        _helper.WithValue(":longs", set, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty sets)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_HashSetDecimal_Empty_ShouldNotAddValue()
    {
        // Arrange
        var set = new HashSet<decimal>();

        // Act
        _helper.WithValue(":decimals", set, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty sets)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_HashSetByteArray_Empty_ShouldNotAddValue()
    {
        // Arrange
        var set = new HashSet<byte[]>();

        // Act
        _helper.WithValue(":binaries", set, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty sets)
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region WithValue List Tests

    [Fact]
    public void WithValue_ListString_ConditionalUseTrue_ShouldAddListValue()
    {
        // Arrange
        var list = new List<string> { "item1", "item2", "item3" };

        // Act
        _helper.WithValue(":items", list, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":items"].L.Should().HaveCount(3);
        _helper.AttributeValues[":items"].L[0].S.Should().Be("item1");
        _helper.AttributeValues[":items"].L[1].S.Should().Be("item2");
        _helper.AttributeValues[":items"].L[2].S.Should().Be("item3");
    }

    [Fact]
    public void WithValue_ListString_Empty_ShouldNotAddValue()
    {
        // Arrange
        var list = new List<string>();

        // Act
        _helper.WithValue(":items", list, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty lists)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_ListString_Null_ShouldNotAddValue()
    {
        // Act
        _helper.WithValue(":items", (List<string>?)null, conditionalUse: true);

        // Assert
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_ListInt_Empty_ShouldNotAddValue()
    {
        // Arrange
        var list = new List<int>();

        // Act
        _helper.WithValue(":numbers", list, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty lists)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_ListLong_Empty_ShouldNotAddValue()
    {
        // Arrange
        var list = new List<long>();

        // Act
        _helper.WithValue(":longs", list, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty lists)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_ListDecimal_Empty_ShouldNotAddValue()
    {
        // Arrange
        var list = new List<decimal>();

        // Act
        _helper.WithValue(":decimals", list, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty lists)
        _helper.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void WithValue_ListBool_Empty_ShouldNotAddValue()
    {
        // Arrange
        var list = new List<bool>();

        // Act
        _helper.WithValue(":bools", list, conditionalUse: true);

        // Assert - Empty collections should not be added (DynamoDB doesn't support empty lists)
        _helper.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region AddFormattedValue Tests - Basic Types

    [Fact]
    public void AddFormattedValue_String_ShouldReturnParameterNameAndAddStringValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue("test string");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].S.Should().Be("test string");
    }

    [Fact]
    public void AddFormattedValue_Null_ShouldReturnParameterNameAndAddNullValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(null);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void AddFormattedValue_Boolean_ShouldReturnParameterNameAndAddBooleanValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(true);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].BOOL.Should().BeTrue();
        _helper.AttributeValues[":p0"].IsBOOLSet.Should().BeTrue();
    }

    [Fact]
    public void AddFormattedValue_Integer_ShouldReturnParameterNameAndAddNumericValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(42);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].N.Should().Be("42");
    }

    [Fact]
    public void AddFormattedValue_Decimal_ShouldReturnParameterNameAndAddNumericValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(123.45m);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].N.Should().Be("123.45");
    }

    [Fact]
    public void AddFormattedValue_Double_ShouldReturnParameterNameAndAddNumericValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(99.99);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].N.Should().Be("99.99");
    }

    [Fact]
    public void AddFormattedValue_Enum_ShouldReturnParameterNameAndAddStringValue()
    {
        // Act
        var paramName = _helper.AddFormattedValue(TestEnum.Active);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].S.Should().Be("Active");
    }

    [Fact]
    public void AddFormattedValue_Guid_ShouldReturnParameterNameAndAddStringValue()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var paramName = _helper.AddFormattedValue(guid);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues.Should().HaveCount(1);
        _helper.AttributeValues[":p0"].S.Should().Be("12345678-1234-1234-1234-123456789012");
    }

    #endregion

    #region AddFormattedValue Tests - DateTime

    [Fact]
    public void AddFormattedValue_DateTime_NoFormat_ShouldUseISOFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var paramName = _helper.AddFormattedValue(dateTime);

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000Z");
    }

    [Fact]
    public void AddFormattedValue_DateTime_ISOFormat_ShouldUseISOFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var paramName = _helper.AddFormattedValue(dateTime, "o");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000Z");
    }

    [Fact]
    public void AddFormattedValue_DateTime_CustomFormat_ShouldUseCustomFormat()
    {
        // Arrange
        var dateTime = new DateTime(2024, 1, 15, 10, 30, 45);

        // Act
        var paramName = _helper.AddFormattedValue(dateTime, "yyyy-MM-dd");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("2024-01-15");
    }

    [Fact]
    public void AddFormattedValue_DateTimeOffset_ShouldFormatCorrectly()
    {
        // Arrange
        var dateTimeOffset = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(-5));

        // Act
        var paramName = _helper.AddFormattedValue(dateTimeOffset, "o");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:45.0000000-05:00");
    }

    #endregion

    #region AddFormattedValue Tests - Numeric Formatting

    [Fact]
    public void AddFormattedValue_Decimal_F2Format_ShouldFormatToTwoDecimalPlaces()
    {
        // Act
        var paramName = _helper.AddFormattedValue(123.456m, "F2");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].N.Should().Be("123.46");
    }

    [Fact]
    public void AddFormattedValue_Integer_HexFormat_ShouldFormatAsHex()
    {
        // Act
        var paramName = _helper.AddFormattedValue(255, "X");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].N.Should().Be("FF");
    }

    [Fact]
    public void AddFormattedValue_Double_ExponentialFormat_ShouldFormatAsExponential()
    {
        // Act
        var paramName = _helper.AddFormattedValue(1234.5, "E2");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].N.Should().Be("1.23E+003");
    }

    #endregion

    #region AddFormattedValue Tests - Guid Formatting

    [Fact]
    public void AddFormattedValue_Guid_NFormat_ShouldFormatWithoutHyphens()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var paramName = _helper.AddFormattedValue(guid, "N");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("12345678123412341234123456789012");
    }

    [Fact]
    public void AddFormattedValue_Guid_BFormat_ShouldFormatWithBraces()
    {
        // Arrange
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789012");

        // Act
        var paramName = _helper.AddFormattedValue(guid, "B");

        // Assert
        paramName.Should().Be(":p0");
        _helper.AttributeValues[":p0"].S.Should().Be("{12345678-1234-1234-1234-123456789012}");
    }

    #endregion

    #region AddFormattedValue Tests - Error Conditions

    [Fact]
    public void AddFormattedValue_Boolean_WithFormat_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _helper.AddFormattedValue(true, "F2");
        action.Should().Throw<FormatException>()
            .WithMessage("Boolean values do not support format strings.*");
    }

    [Fact]
    public void AddFormattedValue_Enum_WithFormat_ShouldThrowFormatException()
    {
        // Act & Assert
        var action = () => _helper.AddFormattedValue(TestEnum.Active, "F2");
        action.Should().Throw<FormatException>()
            .WithMessage("Enum values do not support format strings.*");
    }

    [Fact]
    public void AddFormattedValue_InvalidFormatSpecifier_ShouldThrowFormatException()
    {
        // Act & Assert - Use "Z" format which is invalid for integers
        var action = () => _helper.AddFormattedValue(123, "Z");
        action.Should().Throw<FormatException>()
            .WithMessage("*Format specifier was invalid*");
    }

    [Fact]
    public void AddFormattedValue_UnsupportedTypeWithFormat_ShouldThrowFormatException()
    {
        // Arrange
        var customObject = new { Name = "Test" };

        // Act & Assert
        var action = () => _helper.AddFormattedValue(customObject, "F2");
        action.Should().Throw<FormatException>()
            .WithMessage("*does not support format strings*");
    }

    #endregion

    #region AddFormattedValue Tests - Multiple Calls

    [Fact]
    public void AddFormattedValue_MultipleCalls_ShouldIncrementParameterNames()
    {
        // Act
        var param1 = _helper.AddFormattedValue("value1");
        var param2 = _helper.AddFormattedValue("value2");
        var param3 = _helper.AddFormattedValue("value3");

        // Assert
        param1.Should().Be(":p0");
        param2.Should().Be(":p1");
        param3.Should().Be(":p2");
        _helper.AttributeValues.Should().HaveCount(3);
        _helper.AttributeValues[":p0"].S.Should().Be("value1");
        _helper.AttributeValues[":p1"].S.Should().Be("value2");
        _helper.AttributeValues[":p2"].S.Should().Be("value3");
    }

    #endregion

    #region GetParameterGenerator Tests

    [Fact]
    public void GetParameterGenerator_ShouldReturnParameterGeneratorInstance()
    {
        // Act
        var generator = _helper.GetParameterGenerator();

        // Assert
        generator.Should().NotBeNull();
        generator.Should().BeOfType<ParameterGenerator>();
    }

    [Fact]
    public void GetParameterGenerator_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Act
        var generator1 = _helper.GetParameterGenerator();
        var generator2 = _helper.GetParameterGenerator();

        // Assert
        generator1.Should().BeSameAs(generator2);
    }

    #endregion

    // Test enum for testing enum formatting
    private enum TestEnum
    {
        Active,
        Inactive,
        Pending
    }
}