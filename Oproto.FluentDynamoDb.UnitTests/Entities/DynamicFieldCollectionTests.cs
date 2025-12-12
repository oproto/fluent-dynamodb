using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Entities;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

public class DynamicFieldCollectionTests
{
    #region GetFieldType Tests

    [Fact]
    public void GetFieldType_WithMissingField_ReturnsNotFound()
    {
        var collection = new DynamicFieldCollection();
        collection.GetFieldType("missing").Should().Be(DynamicFieldType.NotFound);
    }

    [Fact]
    public void GetFieldType_WithStringValue_ReturnsString()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.String);
    }

    [Fact]
    public void GetFieldType_WithIso8601DateString_ReturnsDateTime()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00Z" }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.DateTime);
    }

    [Fact]
    public void GetFieldType_WithIso8601DateStringWithOffset_ReturnsDateTime()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00+05:00" }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.DateTime);
    }

    [Fact]
    public void GetFieldType_WithNonDateString_ReturnsString()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "not-a-date" }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.String);
    }


    [Fact]
    public void GetFieldType_WithNumberValue_ReturnsNumber()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.Number);
    }

    [Fact]
    public void GetFieldType_WithBinaryValue_ReturnsBinary()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { B = new MemoryStream(new byte[] { 1, 2, 3 }) }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.Binary);
    }

    [Fact]
    public void GetFieldType_WithBooleanValue_ReturnsBoolean()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { BOOL = true }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.Boolean);
    }

    [Fact]
    public void GetFieldType_WithNullValue_ReturnsNull()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { NULL = true }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.Null);
    }

    [Fact]
    public void GetFieldType_WithListValue_ReturnsList()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { L = new List<AttributeValue> { new() { S = "item" } } }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.List);
    }

    [Fact]
    public void GetFieldType_WithMapValue_ReturnsMap()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { M = new Dictionary<string, AttributeValue> { ["key"] = new() { S = "value" } } }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.Map);
    }

    [Fact]
    public void GetFieldType_WithStringSetValue_ReturnsStringSet()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { SS = new List<string> { "a", "b" } }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.StringSet);
    }

    [Fact]
    public void GetFieldType_WithNumberSetValue_ReturnsNumberSet()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { NS = new List<string> { "1", "2" } }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.NumberSet);
    }

    [Fact]
    public void GetFieldType_WithBinarySetValue_ReturnsBinarySet()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { BS = new List<MemoryStream> { new(new byte[] { 1 }) } }
        });
        collection.GetFieldType("field").Should().Be(DynamicFieldType.BinarySet);
    }

    #endregion

    #region Typed Getters Tests

    [Fact]
    public void GetString_WithValidString_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        collection.GetString("field").Should().Be("hello");
    }

    [Fact]
    public void GetString_WithMissingField_ReturnsNull()
    {
        var collection = new DynamicFieldCollection();
        collection.GetString("missing").Should().BeNull();
    }

    [Fact]
    public void GetString_WithNullValue_ReturnsNull()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { NULL = true }
        });
        collection.GetString("field").Should().BeNull();
    }

    [Fact]
    public void GetString_WithWrongType_ThrowsDynamicFieldTypeException()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        var act = () => collection.GetString("field");
        act.Should().Throw<DynamicFieldTypeException>()
            .Where(e => e.FieldName == "field" && e.RequestedType == typeof(string));
    }

    [Fact]
    public void GetInt_WithValidNumber_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        collection.GetInt("field").Should().Be(42);
    }

    [Fact]
    public void GetInt_WithMissingField_ReturnsNull()
    {
        var collection = new DynamicFieldCollection();
        collection.GetInt("missing").Should().BeNull();
    }

    [Fact]
    public void GetLong_WithValidNumber_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "9223372036854775807" }
        });
        collection.GetLong("field").Should().Be(long.MaxValue);
    }

    [Fact]
    public void GetDouble_WithValidNumber_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "3.14159" }
        });
        collection.GetDouble("field").Should().BeApproximately(3.14159, 0.00001);
    }

    [Fact]
    public void GetDecimal_WithValidNumber_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "123.456" }
        });
        collection.GetDecimal("field").Should().Be(123.456m);
    }

    [Fact]
    public void GetBool_WithTrueValue_ReturnsTrue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { BOOL = true }
        });
        collection.GetBool("field").Should().BeTrue();
    }

    [Fact]
    public void GetBool_WithFalseValue_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { BOOL = false }
        });
        collection.GetBool("field").Should().BeFalse();
    }

    [Fact]
    public void GetDateTime_WithIso8601String_ReturnsDateTime()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00Z" }
        });
        var result = collection.GetDateTime("field");
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2024);
        result.Value.Month.Should().Be(1);
        result.Value.Day.Should().Be(15);
    }

    [Fact]
    public void GetDateTimeOffset_WithIso8601String_ReturnsDateTimeOffset()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00+05:00" }
        });
        var result = collection.GetDateTimeOffset("field");
        result.Should().NotBeNull();
        result!.Value.Offset.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void GetBytes_WithBinaryValue_ReturnsBytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { B = new MemoryStream(bytes) }
        });
        collection.GetBytes("field").Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void GetStringList_WithListOfStrings_ReturnsList()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue
            {
                L = new List<AttributeValue>
                {
                    new() { S = "a" },
                    new() { S = "b" },
                    new() { S = "c" }
                }
            }
        });
        collection.GetStringList("field").Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void GetIntList_WithListOfNumbers_ReturnsList()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue
            {
                L = new List<AttributeValue>
                {
                    new() { N = "1" },
                    new() { N = "2" },
                    new() { N = "3" }
                }
            }
        });
        collection.GetIntList("field").Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void GetStringSet_WithStringSet_ReturnsHashSet()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { SS = new List<string> { "a", "b", "c" } }
        });
        collection.GetStringSet("field").Should().BeEquivalentTo(new HashSet<string> { "a", "b", "c" });
    }

    [Fact]
    public void GetNumberSet_WithNumberSet_ReturnsHashSet()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { NS = new List<string> { "1", "2", "3" } }
        });
        collection.GetNumberSet("field").Should().BeEquivalentTo(new HashSet<int> { 1, 2, 3 });
    }

    #endregion


    #region TryGet Tests

    [Fact]
    public void TryGetString_WithValidString_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        var result = collection.TryGetString("field", out var value);
        result.Should().BeTrue();
        value.Should().Be("hello");
    }

    [Fact]
    public void TryGetString_WithMissingField_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection();
        var result = collection.TryGetString("missing", out var value);
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetString_WithWrongType_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        var result = collection.TryGetString("field", out var value);
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetInt_WithValidNumber_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        var result = collection.TryGetInt("field", out var value);
        result.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetInt_WithMissingField_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection();
        var result = collection.TryGetInt("missing", out var value);
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetBool_WithValidBool_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { BOOL = true }
        });
        var result = collection.TryGetBool("field", out var value);
        result.Should().BeTrue();
        value.Should().BeTrue();
    }

    [Fact]
    public void TryGetDateTime_WithValidDateString_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00Z" }
        });
        var result = collection.TryGetDateTime("field", out var value);
        result.Should().BeTrue();
        value.Should().NotBeNull();
        value!.Value.Year.Should().Be(2024);
    }

    [Fact]
    public void TryGetDateTime_WithNonDateString_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "not-a-date" }
        });
        var result = collection.TryGetDateTime("field", out var value);
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void TryGetDateTimeOffset_WithValidDateString_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "2024-01-15T10:30:00+05:00" }
        });
        var result = collection.TryGetDateTimeOffset("field", out var value);
        result.Should().BeTrue();
        value.Should().NotBeNull();
        value!.Value.Offset.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void TryGetBytes_WithValidBinary_ReturnsTrueAndValue()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { B = new MemoryStream(bytes) }
        });
        var result = collection.TryGetBytes("field", out var value);
        result.Should().BeTrue();
        value.Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void TryGetStringList_WithValidList_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue
            {
                L = new List<AttributeValue> { new() { S = "a" }, new() { S = "b" } }
            }
        });
        var result = collection.TryGetStringList("field", out var value);
        result.Should().BeTrue();
        value.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public void TryGetStringSet_WithValidSet_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { SS = new List<string> { "a", "b" } }
        });
        var result = collection.TryGetStringSet("field", out var value);
        result.Should().BeTrue();
        value.Should().BeEquivalentTo(new HashSet<string> { "a", "b" });
    }

    [Fact]
    public void TryGetNumberSet_WithValidSet_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { NS = new List<string> { "1", "2" } }
        });
        var result = collection.TryGetNumberSet("field", out var value);
        result.Should().BeTrue();
        value.Should().BeEquivalentTo(new HashSet<int> { 1, 2 });
    }

    #endregion

    #region Typed Setters Tests

    [Fact]
    public void SetString_WithValue_CreatesStringAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetString("field", "hello");
        collection.GetString("field").Should().Be("hello");
    }

    [Fact]
    public void SetString_WithNull_RemovesField()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        collection.SetString("field", null);
        collection.ContainsKey("field").Should().BeFalse();
    }

    [Fact]
    public void SetInt_WithValue_CreatesNumberAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetInt("field", 42);
        collection.GetInt("field").Should().Be(42);
    }

    [Fact]
    public void SetLong_WithValue_CreatesNumberAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetLong("field", long.MaxValue);
        collection.GetLong("field").Should().Be(long.MaxValue);
    }

    [Fact]
    public void SetDouble_WithValue_CreatesNumberAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetDouble("field", 3.14159);
        collection.GetDouble("field").Should().BeApproximately(3.14159, 0.00001);
    }

    [Fact]
    public void SetDecimal_WithValue_CreatesNumberAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetDecimal("field", 123.456m);
        collection.GetDecimal("field").Should().Be(123.456m);
    }

    [Fact]
    public void SetBool_WithValue_CreatesBoolAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetBool("field", true);
        collection.GetBool("field").Should().BeTrue();
    }

    [Fact]
    public void SetDateTime_WithValue_CreatesIso8601String()
    {
        var collection = new DynamicFieldCollection();
        var dt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        collection.SetDateTime("field", dt);
        var result = collection.GetDateTime("field");
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2024);
        result.Value.Month.Should().Be(1);
        result.Value.Day.Should().Be(15);
    }

    [Fact]
    public void SetDateTimeOffset_WithValue_CreatesIso8601String()
    {
        var collection = new DynamicFieldCollection();
        var dto = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(5));
        collection.SetDateTimeOffset("field", dto);
        var result = collection.GetDateTimeOffset("field");
        result.Should().NotBeNull();
        result!.Value.Offset.Should().Be(TimeSpan.FromHours(5));
    }

    [Fact]
    public void SetBytes_WithValue_CreatesBinaryAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        var bytes = new byte[] { 1, 2, 3 };
        collection.SetBytes("field", bytes);
        collection.GetBytes("field").Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void SetStringList_WithValue_CreatesListAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetStringList("field", new List<string> { "a", "b", "c" });
        collection.GetStringList("field").Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void SetIntList_WithValue_CreatesListAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetIntList("field", new List<int> { 1, 2, 3 });
        collection.GetIntList("field").Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void SetStringSet_WithValue_CreatesStringSetAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetStringSet("field", new HashSet<string> { "a", "b" });
        collection.GetStringSet("field").Should().BeEquivalentTo(new HashSet<string> { "a", "b" });
    }

    [Fact]
    public void SetNumberSet_WithValue_CreatesNumberSetAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        collection.SetNumberSet("field", new HashSet<int> { 1, 2 });
        collection.GetNumberSet("field").Should().BeEquivalentTo(new HashSet<int> { 1, 2 });
    }

    #endregion


    #region Collection Operations Tests

    [Fact]
    public void ContainsKey_WithExistingField_ReturnsTrue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        collection.ContainsKey("field").Should().BeTrue();
    }

    [Fact]
    public void ContainsKey_WithMissingField_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection();
        collection.ContainsKey("missing").Should().BeFalse();
    }

    [Fact]
    public void Remove_WithExistingField_RemovesAndReturnsTrue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        var result = collection.Remove("field");
        result.Should().BeTrue();
        collection.ContainsKey("field").Should().BeFalse();
    }

    [Fact]
    public void Remove_WithMissingField_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection();
        collection.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllFields()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" }
        });
        collection.Clear();
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void Count_ReturnsNumberOfFields()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" },
            ["field3"] = new AttributeValue { S = "value3" }
        });
        collection.Count.Should().Be(3);
    }

    [Fact]
    public void FieldNames_ReturnsAllFieldNames()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" }
        });
        collection.FieldNames.Should().BeEquivalentTo(new[] { "field1", "field2" });
    }

    #endregion

    #region Enumeration Tests

    [Fact]
    public void Enumeration_ReturnsAllFields()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { N = "42" }
        });

        var items = collection.ToList();
        items.Should().HaveCount(2);
        items.Should().Contain(kvp => kvp.Key == "field1" && kvp.Value.S == "value1");
        items.Should().Contain(kvp => kvp.Key == "field2" && kvp.Value.N == "42");
    }

    [Fact]
    public void Enumeration_WithEmptyCollection_ReturnsEmpty()
    {
        var collection = new DynamicFieldCollection();
        collection.ToList().Should().BeEmpty();
    }

    #endregion

    #region Raw Access Tests

    [Fact]
    public void GetRaw_WithExistingField_ReturnsAttributeValue()
    {
        var av = new AttributeValue { S = "hello" };
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = av
        });
        collection.GetRaw("field").Should().BeSameAs(av);
    }

    [Fact]
    public void GetRaw_WithMissingField_ReturnsNull()
    {
        var collection = new DynamicFieldCollection();
        collection.GetRaw("missing").Should().BeNull();
    }

    [Fact]
    public void SetRaw_WithValue_SetsAttributeValue()
    {
        var collection = new DynamicFieldCollection();
        var av = new AttributeValue { S = "hello" };
        collection.SetRaw("field", av);
        collection.GetRaw("field").Should().BeSameAs(av);
    }

    [Fact]
    public void SetRaw_WithNull_RemovesField()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        collection.SetRaw("field", null);
        collection.ContainsKey("field").Should().BeFalse();
    }

    #endregion

    #region Generic Get/Set Tests

    [Fact]
    public void GenericGet_WithString_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        collection.Get<string>("field").Should().Be("hello");
    }

    [Fact]
    public void GenericGet_WithInt_ReturnsValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { N = "42" }
        });
        collection.Get<int>("field").Should().Be(42);
    }

    [Fact]
    public void GenericTryGet_WithValidValue_ReturnsTrueAndValue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "hello" }
        });
        var result = collection.TryGet<string>("field", out var value);
        result.Should().BeTrue();
        value.Should().Be("hello");
    }

    [Fact]
    public void GenericSet_WithString_SetsValue()
    {
        var collection = new DynamicFieldCollection();
        collection.Set("field", "hello");
        collection.GetString("field").Should().Be("hello");
    }

    [Fact]
    public void GenericSet_WithInt_SetsValue()
    {
        var collection = new DynamicFieldCollection();
        collection.Set("field", 42);
        collection.GetInt("field").Should().Be(42);
    }

    #endregion

    #region ToDictionary Tests

    [Fact]
    public void ToDictionary_ReturnsInternalDictionary()
    {
        var original = new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        };
        var collection = new DynamicFieldCollection(original);
        collection.ToDictionary().Should().BeSameAs(original);
    }

    #endregion

    #region Change Tracking Tests

    [Fact]
    public void StartTrackingChanges_EnablesTracking()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["existing"] = new AttributeValue { S = "value" }
        });
        
        collection.StartTrackingChanges();
        collection.SetString("newField", "newValue");
        
        collection.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void SetOperations_BeforeStartTrackingChanges_DoNotTrack()
    {
        var collection = new DynamicFieldCollection();
        collection.SetString("field", "value");
        
        collection.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void SetOperations_AfterStartTrackingChanges_TrackAdditions()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        
        collection.SetString("field", "value");
        
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field").Should().BeTrue();
        changes.GetString("field").Should().Be("value");
    }

    [Fact]
    public void SetOperations_AfterStartTrackingChanges_TrackModifications()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "original" }
        });
        collection.StartTrackingChanges();
        
        collection.SetString("field", "modified");
        
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field").Should().BeTrue();
        changes.GetString("field").Should().Be("modified");
    }

    [Fact]
    public void Remove_AfterStartTrackingChanges_TracksRemoval()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        collection.StartTrackingChanges();
        
        collection.Remove("field");
        
        collection.HasChanges.Should().BeTrue();
        collection.RemovedFields.Should().Contain("field");
    }

    [Fact]
    public void SetNull_AfterStartTrackingChanges_TracksRemoval()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        collection.StartTrackingChanges();
        
        collection.SetString("field", null);
        
        collection.HasChanges.Should().BeTrue();
        collection.RemovedFields.Should().Contain("field");
        collection.ContainsKey("field").Should().BeFalse();
    }

    [Fact]
    public void ChangesOnly_ReturnsOnlyAddedOrModifiedFields()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["unchanged"] = new AttributeValue { S = "original" },
            ["toModify"] = new AttributeValue { S = "original" },
            ["toRemove"] = new AttributeValue { S = "original" }
        });
        collection.StartTrackingChanges();
        
        collection.SetString("toModify", "modified");
        collection.SetString("newField", "new");
        collection.Remove("toRemove");
        
        var changes = collection.ChangesOnly(resetTracking: false);
        
        changes.Count.Should().Be(2);
        changes.ContainsKey("toModify").Should().BeTrue();
        changes.ContainsKey("newField").Should().BeTrue();
        changes.ContainsKey("unchanged").Should().BeFalse();
        changes.RemovedFields.Should().Contain("toRemove");
    }

    [Fact]
    public void ChangesOnly_WithDefaultParameter_ResetsTracking()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        collection.SetString("field", "value");
        
        collection.ChangesOnly();
        
        collection.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void ChangesOnly_WithResetTrackingFalse_PreservesTracking()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        collection.SetString("field", "value");
        
        collection.ChangesOnly(resetTracking: false);
        
        collection.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void ResetChangeTracking_ClearsAllTracking()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["existing"] = new AttributeValue { S = "value" }
        });
        collection.StartTrackingChanges();
        collection.SetString("newField", "value");
        collection.Remove("existing");
        
        collection.ResetChangeTracking();
        
        collection.HasChanges.Should().BeFalse();
        collection.RemovedFields.Should().BeEmpty();
    }

    [Fact]
    public void RemovedFields_ReturnsSetOfRemovedFieldNames()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" }
        });
        collection.StartTrackingChanges();
        
        collection.Remove("field1");
        collection.Remove("field2");
        
        collection.RemovedFields.Should().BeEquivalentTo(new[] { "field1", "field2" });
    }

    [Fact]
    public void HasChanges_WithNoChanges_ReturnsFalse()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        collection.StartTrackingChanges();
        
        collection.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void HasChanges_WithAdditions_ReturnsTrue()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        collection.SetString("field", "value");
        
        collection.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void HasChanges_WithRemovals_ReturnsTrue()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "value" }
        });
        collection.StartTrackingChanges();
        collection.Remove("field");
        
        collection.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void SetThenRemove_TracksOnlyRemoval()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        
        collection.SetString("field", "value");
        collection.Remove("field");
        
        collection.RemovedFields.Should().Contain("field");
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field").Should().BeFalse();
    }

    [Fact]
    public void RemoveThenSet_TracksOnlyModification()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field"] = new AttributeValue { S = "original" }
        });
        collection.StartTrackingChanges();
        
        collection.Remove("field");
        collection.SetString("field", "newValue");
        
        collection.RemovedFields.Should().NotContain("field");
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field").Should().BeTrue();
        changes.GetString("field").Should().Be("newValue");
    }

    [Fact]
    public void Clear_AfterStartTrackingChanges_TracksAllRemovals()
    {
        var collection = new DynamicFieldCollection(new Dictionary<string, AttributeValue>
        {
            ["field1"] = new AttributeValue { S = "value1" },
            ["field2"] = new AttributeValue { S = "value2" }
        });
        collection.StartTrackingChanges();
        
        collection.Clear();
        
        collection.RemovedFields.Should().BeEquivalentTo(new[] { "field1", "field2" });
    }

    [Fact]
    public void AllTypedSetters_TrackChanges()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        
        collection.SetString("string", "value");
        collection.SetInt("int", 42);
        collection.SetLong("long", 123L);
        collection.SetDouble("double", 3.14);
        collection.SetDecimal("decimal", 99.99m);
        collection.SetBool("bool", true);
        collection.SetDateTime("datetime", DateTime.UtcNow);
        collection.SetDateTimeOffset("datetimeoffset", DateTimeOffset.UtcNow);
        collection.SetBytes("bytes", new byte[] { 1, 2, 3 });
        collection.SetStringList("stringlist", new List<string> { "a", "b" });
        collection.SetIntList("intlist", new List<int> { 1, 2 });
        collection.SetStringSet("stringset", new HashSet<string> { "x", "y" });
        collection.SetNumberSet("numberset", new HashSet<int> { 10, 20 });
        collection.SetRaw("raw", new AttributeValue { S = "raw" });
        
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.Count.Should().Be(14);
    }

    [Fact]
    public void GenericSet_TracksChanges()
    {
        var collection = new DynamicFieldCollection();
        collection.StartTrackingChanges();
        
        collection.Set("field", "value");
        
        collection.HasChanges.Should().BeTrue();
        var changes = collection.ChangesOnly(resetTracking: false);
        changes.ContainsKey("field").Should().BeTrue();
    }

    #endregion

    #region Property-Based Tests

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 16: Change Tracking Accuracy**
    /// **Validates: Requirements 11.2, 11.3, 11.4**
    /// 
    /// For any DynamicFieldCollection with change tracking enabled, after a sequence of Set and Remove operations,
    /// ChangesOnly() SHALL return a collection containing exactly the fields that were added or modified,
    /// and RemovedFields SHALL contain exactly the fields that were removed.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChangeTracking_Accuracy_AfterSetAndRemoveOperations()
    {
        return Prop.ForAll(
            GenerateInitialFields(),
            GenerateOperationSequence(),
            (initialFields, operations) =>
            {
                // Arrange - create collection with initial fields
                var initialDict = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
                foreach (var field in initialFields)
                {
                    initialDict[field] = new AttributeValue { S = $"initial_{field}" };
                }
                var collection = new DynamicFieldCollection(initialDict);
                collection.StartTrackingChanges();

                // Track expected changes manually
                var expectedModified = new HashSet<string>(StringComparer.Ordinal);
                var expectedRemoved = new HashSet<string>(StringComparer.Ordinal);
                var currentFields = new HashSet<string>(initialFields, StringComparer.Ordinal);

                // Act - apply operations
                foreach (var op in operations)
                {
                    if (op.IsSet)
                    {
                        collection.SetString(op.FieldName, op.Value);
                        currentFields.Add(op.FieldName);
                        expectedModified.Add(op.FieldName);
                        expectedRemoved.Remove(op.FieldName);
                    }
                    else // Remove
                    {
                        var wasPresent = collection.ContainsKey(op.FieldName);
                        collection.Remove(op.FieldName);
                        currentFields.Remove(op.FieldName);
                        if (wasPresent || expectedModified.Contains(op.FieldName))
                        {
                            expectedModified.Remove(op.FieldName);
                            expectedRemoved.Add(op.FieldName);
                        }
                    }
                }

                // Get changes without resetting tracking
                var changes = collection.ChangesOnly(resetTracking: false);

                // Assert - verify ChangesOnly contains exactly the modified fields
                var changesFieldNames = changes.FieldNames.ToHashSet(StringComparer.Ordinal);
                var modifiedFieldsMatch = changesFieldNames.SetEquals(
                    expectedModified.Where(f => collection.ContainsKey(f)));

                // Assert - verify RemovedFields contains exactly the removed fields
                var removedFieldsMatch = changes.RemovedFields.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expectedRemoved);

                // Assert - verify HasChanges is correct
                var hasChangesCorrect = collection.HasChanges == 
                    (expectedModified.Any(f => collection.ContainsKey(f)) || expectedRemoved.Any());

                return (modifiedFieldsMatch && removedFieldsMatch && hasChangesCorrect).ToProperty()
                    .Label($"Change tracking accuracy. " +
                           $"ModifiedFieldsMatch: {modifiedFieldsMatch}, " +
                           $"RemovedFieldsMatch: {removedFieldsMatch}, " +
                           $"HasChangesCorrect: {hasChangesCorrect}, " +
                           $"ExpectedModified: [{string.Join(", ", expectedModified)}], " +
                           $"ActualModified: [{string.Join(", ", changesFieldNames)}], " +
                           $"ExpectedRemoved: [{string.Join(", ", expectedRemoved)}], " +
                           $"ActualRemoved: [{string.Join(", ", changes.RemovedFields)}]");
            });
    }

    /// <summary>
    /// Generates a list of initial field names for the collection.
    /// </summary>
    private static Arbitrary<List<string>> GenerateInitialFields()
    {
        return Arb.Default.PositiveInt()
            .Filter(n => n.Get >= 0 && n.Get <= 5)
            .Generator
            .SelectMany(count =>
            {
                var fields = new List<string>();
                for (int i = 0; i < count.Get; i++)
                {
                    fields.Add($"initial_field_{i}");
                }
                return Gen.Constant(fields);
            })
            .ToArbitrary();
    }

    /// <summary>
    /// Generates a sequence of Set and Remove operations.
    /// </summary>
    private static Arbitrary<List<FieldOperation>> GenerateOperationSequence()
    {
        return Arb.Default.PositiveInt()
            .Filter(n => n.Get >= 1 && n.Get <= 20)
            .Generator
            .SelectMany(count =>
            {
                var operations = new List<FieldOperation>();
                var random = new System.Random();
                for (int i = 0; i < count.Get; i++)
                {
                    var isSet = random.Next(2) == 0;
                    var fieldIndex = random.Next(10); // Use a pool of 10 possible field names
                    var fieldName = $"field_{fieldIndex}";
                    operations.Add(new FieldOperation
                    {
                        IsSet = isSet,
                        FieldName = fieldName,
                        Value = isSet ? $"value_{i}" : null
                    });
                }
                return Gen.Constant(operations);
            })
            .ToArbitrary();
    }

    /// <summary>
    /// Represents a Set or Remove operation on a field.
    /// </summary>
    private class FieldOperation
    {
        public bool IsSet { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    #endregion
}
