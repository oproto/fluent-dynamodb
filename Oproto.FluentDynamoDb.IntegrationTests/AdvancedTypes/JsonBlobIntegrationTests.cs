using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Newtonsoft.Json;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.NewtonsoftJson;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.SystemTextJson;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Oproto.FluentDynamoDb.IntegrationTests.AdvancedTypes;

/// <summary>
/// Integration tests for the JSON serializer refactor.
/// Tests the runtime configuration of JSON serializers via FluentDynamoDbOptions.
/// </summary>
/// <remarks>
/// These tests verify:
/// - Error message when no serializer is configured
/// - SystemTextJson works with WithSystemTextJson()
/// - NewtonsoftJson works with WithNewtonsoftJson()
/// - Custom options are respected
/// </remarks>
[Collection("DynamoDB Local")]
public class JsonBlobIntegrationTests : IntegrationTestBase
{
    public JsonBlobIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await CreateTableAsync<JsonBlobTestEntity>();
    }

    #region Test: Error message when no serializer configured

    [Fact]
    public void ToDynamoDb_WithoutJsonSerializer_ThrowsInvalidOperationException()
    {
        // Arrange
        var entity = new JsonBlobTestEntity
        {
            Id = "test-1",
            Content = new DocumentContent
            {
                Title = "Test Document",
                Body = "This is the body content"
            }
        };

        // Act - Call ToDynamoDb without configuring a JSON serializer
        var act = () => JsonBlobTestEntity.ToDynamoDb(entity, options: null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no JSON serializer is configured*")
            .WithMessage("*WithSystemTextJson()*")
            .WithMessage("*WithNewtonsoftJson()*");
    }

    [Fact]
    public void FromDynamoDb_WithoutJsonSerializer_ThrowsInvalidOperationException()
    {
        // Arrange - Create a DynamoDB item with a JSON blob value
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-1" },
            ["content"] = new AttributeValue { S = "{\"Title\":\"Test\",\"Body\":\"Content\"}" }
        };

        // Act - Call FromDynamoDb without configuring a JSON serializer
        var act = () => JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(item, options: null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no JSON serializer is configured*")
            .WithMessage("*WithSystemTextJson()*")
            .WithMessage("*WithNewtonsoftJson()*");
    }

    #endregion

    #region Test: SystemTextJson works with WithSystemTextJson()

    [Fact]
    public async Task SystemTextJson_RoundTrip_PreservesData()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "stj-test-1",
            Content = new DocumentContent
            {
                Title = "System.Text.Json Test",
                Body = "Testing round-trip with System.Text.Json"
            }
        };

        // Act - Save to DynamoDB
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);
        await DynamoDb.PutItemAsync(TableName, item);

        // Load from DynamoDB
        var getResponse = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "stj-test-1" }
        });

        var loadedEntity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(getResponse.Item, options);

        // Assert
        loadedEntity.Should().NotBeNull();
        loadedEntity.Id.Should().Be("stj-test-1");
        loadedEntity.Content.Should().NotBeNull();
        loadedEntity.Content!.Title.Should().Be("System.Text.Json Test");
        loadedEntity.Content.Body.Should().Be("Testing round-trip with System.Text.Json");
    }

    [Fact]
    public void SystemTextJson_SerializesToJsonString()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "stj-test-2",
            Content = new DocumentContent
            {
                Title = "Test",
                Body = "Content"
            }
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        item.Should().ContainKey("content");
        item["content"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var json = item["content"].S;
        var deserialized = JsonSerializer.Deserialize<DocumentContent>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Title.Should().Be("Test");
        deserialized.Body.Should().Be("Content");
    }

    #endregion

    #region Test: NewtonsoftJson works with WithNewtonsoftJson()

    [Fact]
    public async Task NewtonsoftJson_RoundTrip_PreservesData()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithNewtonsoftJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "newtonsoft-test-1",
            Content = new DocumentContent
            {
                Title = "Newtonsoft.Json Test",
                Body = "Testing round-trip with Newtonsoft.Json"
            }
        };

        // Act - Save to DynamoDB
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);
        await DynamoDb.PutItemAsync(TableName, item);

        // Load from DynamoDB
        var getResponse = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "newtonsoft-test-1" }
        });

        var loadedEntity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(getResponse.Item, options);

        // Assert
        loadedEntity.Should().NotBeNull();
        loadedEntity.Id.Should().Be("newtonsoft-test-1");
        loadedEntity.Content.Should().NotBeNull();
        loadedEntity.Content!.Title.Should().Be("Newtonsoft.Json Test");
        loadedEntity.Content.Body.Should().Be("Testing round-trip with Newtonsoft.Json");
    }

    [Fact]
    public void NewtonsoftJson_SerializesToJsonString()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithNewtonsoftJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "newtonsoft-test-2",
            Content = new DocumentContent
            {
                Title = "Test",
                Body = "Content"
            }
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        item.Should().ContainKey("content");
        item["content"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var json = item["content"].S;
        var deserialized = JsonConvert.DeserializeObject<DocumentContent>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Title.Should().Be("Test");
        deserialized.Body.Should().Be("Content");
    }

    #endregion

    #region Test: Custom options are respected

    [Fact]
    public void SystemTextJson_WithCamelCaseOptions_UsesCamelCase()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson(jsonOptions);
        var entity = new JsonBlobTestEntity
        {
            Id = "custom-stj-1",
            Content = new DocumentContent
            {
                Title = "CamelCase Test",
                Body = "Testing camelCase property names"
            }
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        var json = item["content"].S;
        json.Should().Contain("\"title\":", "should use camelCase for Title property");
        json.Should().Contain("\"body\":", "should use camelCase for Body property");
        json.Should().NotContain("\"Title\":", "should not use PascalCase");
        json.Should().NotContain("\"Body\":", "should not use PascalCase");
    }

    [Fact]
    public void NewtonsoftJson_WithCustomSettings_UsesCustomSettings()
    {
        // Arrange
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };
        var options = new FluentDynamoDbOptions().WithNewtonsoftJson(settings);
        var entity = new JsonBlobTestEntity
        {
            Id = "custom-newtonsoft-1",
            Content = new DocumentContent
            {
                Title = "Indented Test",
                Body = "Testing indented JSON"
            }
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        var json = item["content"].S;
        json.Should().Contain("\n", "should be indented with newlines");
        json.Should().Contain("  ", "should have indentation spaces");
    }

    [Fact]
    public async Task SystemTextJson_WithCustomOptions_RoundTripPreservesData()
    {
        // Arrange - Use camelCase for serialization
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true // Important for deserialization
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson(jsonOptions);
        var entity = new JsonBlobTestEntity
        {
            Id = "custom-roundtrip-1",
            Content = new DocumentContent
            {
                Title = "Custom Options Round-Trip",
                Body = "Testing that custom options work for both serialization and deserialization"
            }
        };

        // Act - Save to DynamoDB
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);
        await DynamoDb.PutItemAsync(TableName, item);

        // Load from DynamoDB
        var getResponse = await DynamoDb.GetItemAsync(TableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "custom-roundtrip-1" }
        });

        var loadedEntity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(getResponse.Item, options);

        // Assert
        loadedEntity.Should().NotBeNull();
        loadedEntity.Content.Should().NotBeNull();
        loadedEntity.Content!.Title.Should().Be("Custom Options Round-Trip");
        loadedEntity.Content.Body.Should().Be("Testing that custom options work for both serialization and deserialization");
    }

    #endregion

    #region Test: Null handling

    [Fact]
    public void SystemTextJson_WithNullContent_DoesNotIncludeAttribute()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "null-content-1",
            Content = null
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        item.Should().ContainKey("pk");
        item.Should().NotContainKey("content", "null JsonBlob properties should not be serialized");
    }

    [Fact]
    public void NewtonsoftJson_WithNullContent_DoesNotIncludeAttribute()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithNewtonsoftJson();
        var entity = new JsonBlobTestEntity
        {
            Id = "null-content-2",
            Content = null
        };

        // Act
        var item = JsonBlobTestEntity.ToDynamoDb(entity, options);

        // Assert
        item.Should().ContainKey("pk");
        item.Should().NotContainKey("content", "null JsonBlob properties should not be serialized");
    }

    [Fact]
    public void FromDynamoDb_WithMissingAttribute_ReturnsNullContent()
    {
        // Arrange
        var options = new FluentDynamoDbOptions().WithSystemTextJson();
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "missing-content-1" }
            // Note: "content" attribute is intentionally missing
        };

        // Act
        var entity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(item, options);

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be("missing-content-1");
        entity.Content.Should().BeNull();
    }

    #endregion
}

#region Test Entities

/// <summary>
/// Test entity with a [JsonBlob] property for integration testing.
/// </summary>
[DynamoDbTable("json-blob-test")]
public partial class JsonBlobTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("content")]
    [JsonBlob]
    public DocumentContent? Content { get; set; }
}

/// <summary>
/// Complex type to be serialized as JSON blob.
/// </summary>
public class DocumentContent
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

#endregion
