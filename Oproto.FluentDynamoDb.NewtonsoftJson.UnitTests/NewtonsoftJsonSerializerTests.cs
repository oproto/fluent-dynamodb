using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oproto.FluentDynamoDb.NewtonsoftJson;

namespace Oproto.FluentDynamoDb.NewtonsoftJson.UnitTests;

/// <summary>
/// Tests for <see cref="NewtonsoftJsonBlobSerializer"/>.
/// </summary>
public class NewtonsoftJsonBlobSerializerTests
{
    #region NewtonsoftJsonBlobSerializer - Default Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesSerializer_ThatCanSerialize()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var testObject = new TestData { Id = "test-123", Name = "Test Name", Value = 42 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"Id\":\"test-123\"");
        json.Should().Contain("\"Name\":\"Test Name\"");
        json.Should().Contain("\"Value\":42");
    }

    [Fact]
    public void DefaultConstructor_CreatesSerializer_ThatCanDeserialize()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var json = "{\"Id\":\"test-456\",\"Name\":\"Another Test\",\"Value\":99}";

        // Act
        var result = serializer.Deserialize<TestData>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-456");
        result.Name.Should().Be("Another Test");
        result.Value.Should().Be(99);
    }

    [Fact]
    public void DefaultConstructor_RoundTrip_PreservesData()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var original = new TestData { Id = "round-trip-test", Name = "Round Trip", Value = 123 };

        // Act
        var json = serializer.Serialize(original);
        var restored = serializer.Deserialize<TestData>(json);

        // Assert
        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Value.Should().Be(original.Value);
    }


    [Fact]
    public void DefaultConstructor_UsesTypeNameHandlingNone_NoTypeMetadata()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var testObject = new TestData { Id = "aot-safe-test", Name = "AOT Safe", Value = 100 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert - TypeNameHandling.None means no $type metadata
        json.Should().NotContain("$type");
        json.Should().NotContain("$values");
    }

    [Fact]
    public void DefaultConstructor_UsesNullValueHandlingIgnore_OmitsNulls()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var testObject = new TestData { Id = "test-null", Name = null, Value = 0 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert - NullValueHandling.Ignore means null properties are omitted
        json.Should().NotContain("\"Name\"");
        json.Should().Contain("\"Id\":\"test-null\"");
    }

    [Fact]
    public void DefaultConstructor_UsesIsoDateFormat()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var testObject = new TestDataWithDate
        {
            Id = "date-test",
            CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert
        json.Should().Contain("2024-01-15");
        
        // Should deserialize correctly
        var restored = serializer.Deserialize<TestDataWithDate>(json);
        restored.Should().NotBeNull();
        restored!.CreatedAt.Should().BeCloseTo(testObject.CreatedAt, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region NewtonsoftJsonBlobSerializer - Custom Settings Constructor Tests

    [Fact]
    public void CustomSettingsConstructor_WithCamelCase_SerializesWithCamelCase()
    {
        // Arrange
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var serializer = new NewtonsoftJsonBlobSerializer(settings);
        var testObject = new TestData { Id = "test-123", Name = "Test Name", Value = 42 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert
        json.Should().Contain("\"id\":\"test-123\"");
        json.Should().Contain("\"name\":\"Test Name\"");
        json.Should().Contain("\"value\":42");
    }

    [Fact]
    public void CustomSettingsConstructor_WithCamelCase_DeserializesFromCamelCase()
    {
        // Arrange
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var serializer = new NewtonsoftJsonBlobSerializer(settings);
        var json = "{\"id\":\"test-456\",\"name\":\"Another Test\",\"value\":99}";

        // Act
        var result = serializer.Deserialize<TestData>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("test-456");
        result.Name.Should().Be("Another Test");
        result.Value.Should().Be(99);
    }

    [Fact]
    public void CustomSettingsConstructor_WithNullValueHandlingInclude_IncludesNulls()
    {
        // Arrange
        var settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include
        };
        var serializer = new NewtonsoftJsonBlobSerializer(settings);
        var testObject = new TestData { Id = "test-null", Name = null, Value = 0 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert - NullValueHandling.Include means null properties are included
        json.Should().Contain("\"Name\":null");
    }

    [Fact]
    public void CustomSettingsConstructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new NewtonsoftJsonBlobSerializer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    #endregion

    #region NewtonsoftJsonBlobSerializer - Edge Cases

    [Fact]
    public void Deserialize_WithNullJson_ReturnsDefault()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();

        // Act
        var result = serializer.Deserialize<TestData>(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithEmptyJson_ReturnsDefault()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();

        // Act
        var result = serializer.Deserialize<TestData>("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Serialize_WithComplexObject_HandlesNestedProperties()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var complexObject = new ComplexTestData
        {
            Id = "complex-123",
            Metadata = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" },
            Tags = new List<string> { "tag1", "tag2", "tag3" }
        };

        // Act
        var json = serializer.Serialize(complexObject);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"Id\":\"complex-123\"");
        json.Should().Contain("\"Metadata\"");
        json.Should().Contain("\"Tags\"");
    }

    [Fact]
    public void RoundTrip_WithComplexObject_PreservesAllData()
    {
        // Arrange
        var serializer = new NewtonsoftJsonBlobSerializer();
        var original = new ComplexTestData
        {
            Id = "complex-round-trip",
            Metadata = new Dictionary<string, string> { ["author"] = "John Doe", ["version"] = "1.0" },
            Tags = new List<string> { "important", "reviewed" }
        };

        // Act
        var json = serializer.Serialize(original);
        var restored = serializer.Deserialize<ComplexTestData>(json);

        // Assert
        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.Metadata.Should().BeEquivalentTo(original.Metadata);
        restored.Tags.Should().BeEquivalentTo(original.Tags);
    }

    #endregion
}


/// <summary>
/// Tests for <see cref="NewtonsoftJsonOptionsExtensions"/>.
/// </summary>
public class NewtonsoftJsonOptionsExtensionsTests
{
    [Fact]
    public void WithNewtonsoftJson_Default_ConfiguresJsonSerializer()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.WithNewtonsoftJson();

        // Assert
        result.JsonSerializer.Should().NotBeNull();
        result.JsonSerializer.Should().BeOfType<NewtonsoftJsonBlobSerializer>();
    }

    [Fact]
    public void WithNewtonsoftJson_Default_ReturnsNewInstance()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.WithNewtonsoftJson();

        // Assert
        result.Should().NotBeSameAs(options);
    }

    [Fact]
    public void WithNewtonsoftJson_Default_PreservesOtherOptions()
    {
        // Arrange
        var logger = new TestLogger();
        var options = new FluentDynamoDbOptions().WithLogger(logger);

        // Act
        var result = options.WithNewtonsoftJson();

        // Assert
        result.Logger.Should().BeSameAs(logger);
        result.JsonSerializer.Should().NotBeNull();
    }

    [Fact]
    public void WithNewtonsoftJson_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        FluentDynamoDbOptions options = null!;

        // Act
        var act = () => options.WithNewtonsoftJson();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void WithNewtonsoftJson_WithJsonSerializerSettings_ConfiguresSerializer()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        // Act
        var result = options.WithNewtonsoftJson(settings);

        // Assert
        result.JsonSerializer.Should().NotBeNull();
        result.JsonSerializer.Should().BeOfType<NewtonsoftJsonBlobSerializer>();
    }

    [Fact]
    public void WithNewtonsoftJson_WithJsonSerializerSettings_UsesProvidedSettings()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var testObject = new TestData { Id = "test", Name = "Test", Value = 1 };

        // Act
        var result = options.WithNewtonsoftJson(settings);
        var json = result.JsonSerializer!.Serialize(testObject);

        // Assert - camelCase property names indicate custom settings were used
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"value\":");
    }

    [Fact]
    public void WithNewtonsoftJson_WithNullJsonSerializerSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var act = () => options.WithNewtonsoftJson(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void WithNewtonsoftJson_Chained_LastOneWins()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var camelCaseSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var testObject = new TestData { Id = "test", Name = "Test", Value = 1 };

        // Act - chain default then custom settings
        var result = options.WithNewtonsoftJson().WithNewtonsoftJson(camelCaseSettings);
        var json = result.JsonSerializer!.Serialize(testObject);

        // Assert - should use camelCase (last configured)
        json.Should().Contain("\"id\":");
    }
}

// Test data classes
public class TestData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int Value { get; set; }
}

public class ComplexTestData
{
    public string? Id { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class TestDataWithDate
{
    public string? Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Simple test logger for testing options preservation
internal class TestLogger : Oproto.FluentDynamoDb.Logging.IDynamoDbLogger
{
    public bool IsEnabled(Oproto.FluentDynamoDb.Logging.LogLevel logLevel) => true;
    public void LogTrace(int eventId, string message, params object[] args) { }
    public void LogDebug(int eventId, string message, params object[] args) { }
    public void LogInformation(int eventId, string message, params object[] args) { }
    public void LogWarning(int eventId, string message, params object[] args) { }
    public void LogError(int eventId, string message, params object[] args) { }
    public void LogError(int eventId, Exception exception, string message, params object[] args) { }
    public void LogCritical(int eventId, Exception exception, string message, params object[] args) { }
}
