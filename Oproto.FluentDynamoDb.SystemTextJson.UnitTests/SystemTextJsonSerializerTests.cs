using System.Text.Json;
using System.Text.Json.Serialization;
using Oproto.FluentDynamoDb.SystemTextJson;

namespace Oproto.FluentDynamoDb.SystemTextJson.UnitTests;

/// <summary>
/// Tests for <see cref="SystemTextJsonBlobSerializer"/> and <see cref="SystemTextJsonOptionsExtensions"/>.
/// </summary>
public class SystemTextJsonBlobSerializerTests
{
    #region SystemTextJsonBlobSerializer - Default Constructor Tests

    [Fact]
    public void DefaultConstructor_CreatesSerializer_ThatCanSerialize()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer();
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
        var serializer = new SystemTextJsonBlobSerializer();
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
        var serializer = new SystemTextJsonBlobSerializer();
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

    #endregion

    #region SystemTextJsonBlobSerializer - Custom Options Constructor Tests

    [Fact]
    public void CustomOptionsConstructor_WithCamelCase_SerializesWithCamelCase()
    {
        // Arrange
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var serializer = new SystemTextJsonBlobSerializer(options);
        var testObject = new TestData { Id = "test-123", Name = "Test Name", Value = 42 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert
        json.Should().Contain("\"id\":\"test-123\"");
        json.Should().Contain("\"name\":\"Test Name\"");
        json.Should().Contain("\"value\":42");
    }

    [Fact]
    public void CustomOptionsConstructor_WithCamelCase_DeserializesFromCamelCase()
    {
        // Arrange
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var serializer = new SystemTextJsonBlobSerializer(options);
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
    public void CustomOptionsConstructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemTextJsonBlobSerializer((JsonSerializerOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    #endregion

    #region SystemTextJsonBlobSerializer - AOT Context Constructor Tests

    [Fact]
    public void AotContextConstructor_WithContext_SerializesCorrectly()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer(TestJsonContext.Default);
        var testObject = new TestData { Id = "aot-test", Name = "AOT Compatible", Value = 100 };

        // Act
        var json = serializer.Serialize(testObject);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"Id\":\"aot-test\"");
        json.Should().Contain("\"Name\":\"AOT Compatible\"");
        json.Should().Contain("\"Value\":100");
    }

    [Fact]
    public void AotContextConstructor_WithContext_DeserializesCorrectly()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer(TestJsonContext.Default);
        var json = "{\"Id\":\"aot-456\",\"Name\":\"AOT Test\",\"Value\":200}";

        // Act
        var result = serializer.Deserialize<TestData>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("aot-456");
        result.Name.Should().Be("AOT Test");
        result.Value.Should().Be(200);
    }

    [Fact]
    public void AotContextConstructor_RoundTrip_PreservesData()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer(TestJsonContext.Default);
        var original = new TestData { Id = "aot-round-trip", Name = "AOT Round Trip", Value = 300 };

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
    public void AotContextConstructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemTextJsonBlobSerializer((JsonSerializerContext)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    #endregion

    #region SystemTextJsonBlobSerializer - Edge Cases

    [Fact]
    public void Deserialize_WithNullJson_ReturnsDefault()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer();

        // Act
        var result = serializer.Deserialize<TestData>(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithEmptyJson_ReturnsDefault()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer();

        // Act
        var result = serializer.Deserialize<TestData>("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Serialize_WithComplexObject_HandlesNestedProperties()
    {
        // Arrange
        var serializer = new SystemTextJsonBlobSerializer();
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
        var serializer = new SystemTextJsonBlobSerializer();
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
/// Tests for <see cref="SystemTextJsonOptionsExtensions"/>.
/// </summary>
public class SystemTextJsonOptionsExtensionsTests
{
    [Fact]
    public void WithSystemTextJson_Default_ConfiguresJsonSerializer()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.WithSystemTextJson();

        // Assert
        result.JsonSerializer.Should().NotBeNull();
        result.JsonSerializer.Should().BeOfType<SystemTextJsonBlobSerializer>();
    }

    [Fact]
    public void WithSystemTextJson_Default_ReturnsNewInstance()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.WithSystemTextJson();

        // Assert
        result.Should().NotBeSameAs(options);
    }

    [Fact]
    public void WithSystemTextJson_Default_PreservesOtherOptions()
    {
        // Arrange
        var logger = new TestLogger();
        var options = new FluentDynamoDbOptions().WithLogger(logger);

        // Act
        var result = options.WithSystemTextJson();

        // Assert
        result.Logger.Should().BeSameAs(logger);
        result.JsonSerializer.Should().NotBeNull();
    }

    [Fact]
    public void WithSystemTextJson_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        FluentDynamoDbOptions options = null!;

        // Act
        var act = () => options.WithSystemTextJson();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void WithSystemTextJson_WithJsonSerializerOptions_ConfiguresSerializer()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Act
        var result = options.WithSystemTextJson(jsonOptions);

        // Assert
        result.JsonSerializer.Should().NotBeNull();
        result.JsonSerializer.Should().BeOfType<SystemTextJsonBlobSerializer>();
    }

    [Fact]
    public void WithSystemTextJson_WithJsonSerializerOptions_UsesProvidedOptions()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var testObject = new TestData { Id = "test", Name = "Test", Value = 1 };

        // Act
        var result = options.WithSystemTextJson(jsonOptions);
        var json = result.JsonSerializer!.Serialize(testObject);

        // Assert - camelCase property names indicate custom options were used
        json.Should().Contain("\"id\":");
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"value\":");
    }

    [Fact]
    public void WithSystemTextJson_WithNullJsonSerializerOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var act = () => options.WithSystemTextJson((JsonSerializerOptions)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("serializerOptions");
    }

    [Fact]
    public void WithSystemTextJson_WithJsonSerializerContext_ConfiguresSerializer()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var result = options.WithSystemTextJson(TestJsonContext.Default);

        // Assert
        result.JsonSerializer.Should().NotBeNull();
        result.JsonSerializer.Should().BeOfType<SystemTextJsonBlobSerializer>();
    }

    [Fact]
    public void WithSystemTextJson_WithJsonSerializerContext_UsesProvidedContext()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var testObject = new TestData { Id = "aot-test", Name = "AOT", Value = 42 };

        // Act
        var result = options.WithSystemTextJson(TestJsonContext.Default);
        var json = result.JsonSerializer!.Serialize(testObject);
        var restored = result.JsonSerializer.Deserialize<TestData>(json);

        // Assert
        restored.Should().NotBeNull();
        restored!.Id.Should().Be(testObject.Id);
        restored.Name.Should().Be(testObject.Name);
        restored.Value.Should().Be(testObject.Value);
    }

    [Fact]
    public void WithSystemTextJson_WithNullJsonSerializerContext_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();

        // Act
        var act = () => options.WithSystemTextJson((JsonSerializerContext)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void WithSystemTextJson_Chained_LastOneWins()
    {
        // Arrange
        var options = new FluentDynamoDbOptions();
        var camelCaseOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var testObject = new TestData { Id = "test", Name = "Test", Value = 1 };

        // Act - chain default then custom options
        var result = options.WithSystemTextJson().WithSystemTextJson(camelCaseOptions);
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

// JsonSerializerContext for AOT compatibility
[JsonSerializable(typeof(TestData))]
[JsonSerializable(typeof(ComplexTestData))]
internal partial class TestJsonContext : JsonSerializerContext
{
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
