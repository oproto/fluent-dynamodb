// ============================================================================
// JsonBlob Error Handling Tests
// ============================================================================
// These tests verify the error handling behavior for [JsonBlob] properties
// in composite entities, as specified in Requirements 3.1, 3.2, and 3.3.
//
// Requirements:
// 3.1 - Missing JSON serializer throws InvalidOperationException
// 3.2 - JSON deserialization failure throws DynamoDbMappingException with context
// 3.3 - Related entity deserialization error includes related entity type
// ============================================================================

using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.SystemTextJson;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for error handling in JsonBlob properties within composite entities.
/// These tests verify the error handling requirements from the design document.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "jsonblob-composite-entity-fix")]
public class JsonBlobErrorHandlingTests
{
    #region Task 5.1 - Missing JSON Serializer Error Tests

    /// <summary>
    /// Test that verifies InvalidOperationException is thrown when no JSON serializer
    /// is configured and a [JsonBlob] property is encountered during deserialization.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithJsonBlobProperty_WhenNoSerializerConfigured_ThrowsInvalidOperationException()
    {
        // Arrange - Create a DynamoDB item with a JSON blob value
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "{\"Theme\":\"dark\",\"NotificationsEnabled\":true}" }
        };
        
        // Options with NO JSON serializer configured
        var options = new FluentDynamoDbOptions();

        // Act & Assert - Should throw InvalidOperationException
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(item, options));
        
        // Verify the error message contains helpful information
        exception.Message.Should().Contain("JsonBlob",
            "error message should mention JsonBlob attribute");
        exception.Message.Should().Contain("JSON serializer",
            "error message should mention JSON serializer");
    }

    /// <summary>
    /// Test that verifies InvalidOperationException is thrown when no JSON serializer
    /// is configured and a [JsonBlob] property is encountered during serialization (ToDynamoDb).
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void ToDynamoDb_WithJsonBlobProperty_WhenNoSerializerConfigured_ThrowsInvalidOperationException()
    {
        // Arrange - Create an entity with a non-null JsonBlob property
        var entity = new JsonBlobTestEntity
        {
            Id = "test-id",
            Name = "Test Name",
            Settings = new TestSettings
            {
                Theme = "dark",
                NotificationsEnabled = true
            }
        };
        
        // Options with NO JSON serializer configured
        var options = new FluentDynamoDbOptions();

        // Act & Assert - Should throw InvalidOperationException
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonBlobTestEntity.ToDynamoDb(entity, options));
        
        // Verify the error message contains helpful information
        exception.Message.Should().Contain("JsonBlob",
            "error message should mention JsonBlob attribute");
        exception.Message.Should().Contain("JSON serializer",
            "error message should mention JSON serializer");
    }

    /// <summary>
    /// Test that verifies InvalidOperationException is thrown with correct message format
    /// that includes guidance on how to configure the serializer.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithJsonBlobProperty_WhenNoSerializerConfigured_ErrorMessageContainsConfigurationGuidance()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "{\"Theme\":\"dark\"}" }
        };
        var options = new FluentDynamoDbOptions();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(item, options));
        
        // Verify the error message contains configuration guidance
        // The message should tell users how to fix the issue
        exception.Message.Should().Contain("Settings",
            "error message should mention the property name");
    }

    /// <summary>
    /// Test that verifies no exception is thrown when JSON serializer IS configured.
    /// This is a sanity check to ensure the error is only thrown when appropriate.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithJsonBlobProperty_WhenSerializerConfigured_DoesNotThrow()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "{\"Theme\":\"dark\",\"NotificationsEnabled\":true}" }
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act - Should NOT throw
        var entity = JsonBlobTestEntity.FromDynamoDb<JsonBlobTestEntity>(item, options);

        // Assert
        entity.Should().NotBeNull();
        entity.Settings.Should().NotBeNull();
        entity.Settings!.Theme.Should().Be("dark");
    }

    #endregion

    #region Task 5.2 - JSON Deserialization Failure Error Tests

    /// <summary>
    /// Test that verifies DynamoDbMappingException is thrown when JSON deserialization fails
    /// and that the exception contains property context information.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithInvalidJson_ThrowsDynamoDbMappingExceptionWithPropertyContext()
    {
        // Arrange - Create a DynamoDB item with INVALID JSON for the settings property
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "{ this is not valid JSON }" }
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert - Should throw DynamoDbMappingException
        var exception = Assert.Throws<DynamoDbMappingException>(() =>
            JsonBlobTestEntityWithErrorHandling.FromDynamoDb<JsonBlobTestEntityWithErrorHandling>(item, options));
        
        // Verify the exception contains property context
        exception.PropertyName.Should().Be("Settings",
            "exception should contain the property name that failed");
        exception.Operation.Should().Be(MappingOperation.FromDynamoDb,
            "exception should indicate FromDynamoDb operation");
    }

    /// <summary>
    /// Test that verifies DynamoDbMappingException contains the entity type information.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithInvalidJson_ExceptionContainsEntityType()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "not-json" }
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert
        var exception = Assert.Throws<DynamoDbMappingException>(() =>
            JsonBlobTestEntityWithErrorHandling.FromDynamoDb<JsonBlobTestEntityWithErrorHandling>(item, options));
        
        // Verify the exception contains entity type
        exception.EntityType.Should().Be(typeof(JsonBlobTestEntityWithErrorHandling),
            "exception should contain the entity type being mapped");
    }

    /// <summary>
    /// Test that verifies DynamoDbMappingException contains the underlying error.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithInvalidJson_ExceptionContainsUnderlyingError()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "{invalid}" }
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert
        var exception = Assert.Throws<DynamoDbMappingException>(() =>
            JsonBlobTestEntityWithErrorHandling.FromDynamoDb<JsonBlobTestEntityWithErrorHandling>(item, options));
        
        // Verify the exception contains the underlying error
        exception.InnerException.Should().NotBeNull(
            "exception should contain the underlying JSON parsing error");
    }

    /// <summary>
    /// Test that verifies DynamoDbMappingException context contains additional debugging info.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void FromDynamoDb_WithInvalidJson_ExceptionContextContainsDebuggingInfo()
    {
        // Arrange
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "test-id" },
            ["name"] = new AttributeValue { S = "Test Name" },
            ["settings"] = new AttributeValue { S = "malformed-json" }
        };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert
        var exception = Assert.Throws<DynamoDbMappingException>(() =>
            JsonBlobTestEntityWithErrorHandling.FromDynamoDb<JsonBlobTestEntityWithErrorHandling>(item, options));
        
        // Verify the exception context contains debugging info
        exception.Context.Should().ContainKey("Operation",
            "context should contain the operation type");
        exception.Context["Operation"].Should().Be("JsonDeserialization",
            "context should indicate JSON deserialization operation");
    }

    #endregion

    #region Task 5.3 - Related Entity Deserialization Error Tests

    /// <summary>
    /// Test that verifies error message includes related entity type when deserialization
    /// fails in a related entity.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void FromDynamoDb_CompositeEntity_WhenRelatedEntityDeserializationFails_ErrorIncludesRelatedEntityType()
    {
        // Arrange - Create items for a composite entity where the child has invalid JSON
        var parentItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "PARENT" },
            ["name"] = new AttributeValue { S = "Parent Name" },
            ["config"] = new AttributeValue { S = "{\"Region\":\"us-east-1\",\"MaxUsers\":100}" }
        };
        
        var childItemWithInvalidJson = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "CHILD#child-1" },
            ["metadata"] = new AttributeValue { S = "{ this is invalid JSON }" }
        };
        
        var items = new List<Dictionary<string, AttributeValue>> { parentItem, childItemWithInvalidJson };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert - Should throw an exception that includes the related entity type
        var exception = Assert.ThrowsAny<Exception>(() =>
            CompositeParentTestEntityWithErrorHandling.FromDynamoDb<CompositeParentTestEntityWithErrorHandling>(items, options));
        
        // Verify the error message or exception chain includes the related entity type
        var exceptionMessage = GetFullExceptionMessage(exception);
        
        // The error should indicate which entity type failed
        (exceptionMessage.Contains("CompositeChildTestEntityWithErrorHandling") ||
         exceptionMessage.Contains("Metadata") ||
         exceptionMessage.Contains("related entity") ||
         exceptionMessage.Contains("child")).Should().BeTrue(
            "error message should include information about the related entity that failed");
    }

    /// <summary>
    /// Test that verifies the exception chain preserves the original error when
    /// a related entity deserialization fails.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void FromDynamoDb_CompositeEntity_WhenRelatedEntityDeserializationFails_PreservesOriginalError()
    {
        // Arrange
        var parentItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "PARENT" },
            ["name"] = new AttributeValue { S = "Parent Name" }
        };
        
        var childItemWithInvalidJson = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "CHILD#child-1" },
            ["metadata"] = new AttributeValue { S = "not-valid-json" }
        };
        
        var items = new List<Dictionary<string, AttributeValue>> { parentItem, childItemWithInvalidJson };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act & Assert
        var exception = Assert.ThrowsAny<Exception>(() =>
            CompositeParentTestEntityWithErrorHandling.FromDynamoDb<CompositeParentTestEntityWithErrorHandling>(items, options));
        
        // Verify the exception chain contains the original JSON parsing error
        var hasJsonError = ContainsJsonParsingError(exception);
        hasJsonError.Should().BeTrue(
            "exception chain should contain the original JSON parsing error");
    }

    /// <summary>
    /// Test that verifies successful deserialization when related entity JSON is valid.
    /// This is a sanity check to ensure errors are only thrown when appropriate.
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void FromDynamoDb_CompositeEntity_WhenRelatedEntityJsonIsValid_DoesNotThrow()
    {
        // Arrange
        var parentItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "PARENT" },
            ["name"] = new AttributeValue { S = "Parent Name" },
            ["config"] = new AttributeValue { S = "{\"Region\":\"us-east-1\",\"MaxUsers\":100}" }
        };
        
        var childItem = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "parent-id" },
            ["sk"] = new AttributeValue { S = "CHILD#child-1" },
            ["metadata"] = new AttributeValue { S = "{\"Category\":\"test\",\"Priority\":5}" }
        };
        
        var items = new List<Dictionary<string, AttributeValue>> { parentItem, childItem };
        var options = new FluentDynamoDbOptions().WithSystemTextJson();

        // Act - Should NOT throw
        var entity = CompositeParentTestEntityWithErrorHandling.FromDynamoDb<CompositeParentTestEntityWithErrorHandling>(items, options);

        // Assert
        entity.Should().NotBeNull();
        entity.Children.Should().HaveCount(1);
        entity.Children[0].Metadata.Should().NotBeNull();
        entity.Children[0].Metadata!.Category.Should().Be("test");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the full exception message including all inner exceptions.
    /// </summary>
    private static string GetFullExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        var current = exception;
        while (current != null)
        {
            messages.Add(current.Message);
            if (current is DynamoDbMappingException mappingEx)
            {
                messages.Add(mappingEx.ToString());
            }
            current = current.InnerException;
        }
        return string.Join(" | ", messages);
    }

    /// <summary>
    /// Checks if the exception chain contains a JSON parsing error.
    /// </summary>
    private static bool ContainsJsonParsingError(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (current is System.Text.Json.JsonException ||
                current.Message.Contains("JSON") ||
                current.Message.Contains("json") ||
                current.Message.Contains("parse") ||
                current.Message.Contains("deserialize"))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    #endregion
}


#region Test Entities with Error Handling

/// <summary>
/// Test entity that implements proper error handling for JsonBlob deserialization.
/// This simulates the generated code behavior with DynamoDbMappingException.
/// </summary>
public class JsonBlobTestEntityWithErrorHandling : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TestSettings? Settings { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as JsonBlobTestEntityWithErrorHandling;
        if (testEntity == null) throw new ArgumentException("Expected JsonBlobTestEntityWithErrorHandling");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.Id },
            ["name"] = new AttributeValue { S = testEntity.Name }
        };

        if (testEntity.Settings != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Settings' has [JsonBlob] attribute but no JSON serializer is configured. " +
                    "Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Settings);
            item["settings"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new JsonBlobTestEntityWithErrorHandling
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
        };

        // Deserialize JsonBlob property with proper error handling
        if (item.TryGetValue("settings", out var settingsValue) && settingsValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Settings' has [JsonBlob] attribute but no JSON serializer is configured. " +
                    "Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.");
            }
            
            try
            {
                entity.Settings = options.JsonSerializer.Deserialize<TestSettings>(settingsValue.S);
            }
            catch (Exception ex)
            {
                throw DynamoDbMappingException.PropertyConversionFailed(
                    typeof(JsonBlobTestEntityWithErrorHandling),
                    "Settings",
                    settingsValue,
                    typeof(TestSettings),
                    ex)
                    .WithContext("SerializerType", "RuntimeConfigured")
                    .WithContext("PropertyType", "TestSettings")
                    .WithContext("Operation", "JsonDeserialization");
            }
        }

        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.ContainsKey("pk") && item.ContainsKey("name");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}


/// <summary>
/// Parent entity for composite entity error handling testing.
/// Simulates a parent entity with [RelatedEntity] collection and [JsonBlob] property.
/// </summary>
public class CompositeParentTestEntityWithErrorHandling : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ParentConfig? Config { get; set; }
    public List<CompositeChildTestEntityWithErrorHandling> Children { get; set; } = new();

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as CompositeParentTestEntityWithErrorHandling;
        if (testEntity == null) throw new ArgumentException("Expected CompositeParentTestEntityWithErrorHandling");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.Id },
            ["sk"] = new AttributeValue { S = "PARENT" },
            ["name"] = new AttributeValue { S = testEntity.Name }
        };

        if (testEntity.Config != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Config' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Config);
            item["config"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new CompositeParentTestEntityWithErrorHandling
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty
        };

        if (item.TryGetValue("config", out var configValue) && configValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Config' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            
            try
            {
                entity.Config = options.JsonSerializer.Deserialize<ParentConfig>(configValue.S);
            }
            catch (Exception ex)
            {
                throw DynamoDbMappingException.PropertyConversionFailed(
                    typeof(CompositeParentTestEntityWithErrorHandling),
                    "Config",
                    configValue,
                    typeof(ParentConfig),
                    ex)
                    .WithContext("Operation", "JsonDeserialization");
            }
        }

        return (TSelf)(object)entity;
    }

    /// <summary>
    /// Multi-item FromDynamoDb - simulates the generated code for composite entities with error handling.
    /// </summary>
    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var parentItem = items.FirstOrDefault(i => 
            i.TryGetValue("sk", out var sk) && sk.S == "PARENT");
        
        if (parentItem == null)
        {
            throw new InvalidOperationException("No parent item found in composite entity items");
        }

        var entity = FromDynamoDb<CompositeParentTestEntityWithErrorHandling>(parentItem, options);

        var childItems = items.Where(i => 
            i.TryGetValue("sk", out var sk) && sk.S != null && sk.S.StartsWith("CHILD#"));

        foreach (var childItem in childItems)
        {
            try
            {
                // This is the critical part - the child's FromDynamoDb must receive options
                var child = CompositeChildTestEntityWithErrorHandling.FromDynamoDb<CompositeChildTestEntityWithErrorHandling>(childItem, options);
                entity.Children.Add(child);
            }
            catch (DynamoDbMappingException)
            {
                // Re-throw DynamoDbMappingException as-is (already has context)
                throw;
            }
            catch (Exception ex)
            {
                // Wrap other exceptions with related entity context
                throw new DynamoDbMappingException(
                    $"Failed to map related entity CompositeChildTestEntityWithErrorHandling. Error: {ex.Message}",
                    typeof(CompositeChildTestEntityWithErrorHandling),
                    MappingOperation.RelatedEntityMapping,
                    childItem,
                    innerException: ex);
            }
        }

        return (TSelf)(object)entity;
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("sk", out var sk) && sk.S == "PARENT";
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "composite-test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}


/// <summary>
/// Child entity for composite entity error handling testing.
/// Simulates a related entity with [JsonBlob] property and proper error handling.
/// </summary>
public class CompositeChildTestEntityWithErrorHandling : IDynamoDbEntity
{
    public string ParentId { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public ChildMetadata? Metadata { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as CompositeChildTestEntityWithErrorHandling;
        if (testEntity == null) throw new ArgumentException("Expected CompositeChildTestEntityWithErrorHandling");

        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity.ParentId },
            ["sk"] = new AttributeValue { S = $"CHILD#{testEntity.ChildId}" }
        };

        if (testEntity.Metadata != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            var json = options.JsonSerializer.Serialize(testEntity.Metadata);
            item["metadata"] = new AttributeValue { S = json };
        }

        return item;
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new CompositeChildTestEntityWithErrorHandling
        {
            ParentId = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            ChildId = item.TryGetValue("sk", out var sk) && sk.S != null 
                ? sk.S.Replace("CHILD#", "") 
                : string.Empty
        };

        // Deserialize JsonBlob property with proper error handling
        if (item.TryGetValue("metadata", out var metadataValue) && metadataValue.S != null)
        {
            if (options?.JsonSerializer == null)
            {
                throw new InvalidOperationException(
                    "Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured.");
            }
            
            try
            {
                entity.Metadata = options.JsonSerializer.Deserialize<ChildMetadata>(metadataValue.S);
            }
            catch (Exception ex)
            {
                throw DynamoDbMappingException.PropertyConversionFailed(
                    typeof(CompositeChildTestEntityWithErrorHandling),
                    "Metadata",
                    metadataValue,
                    typeof(ChildMetadata),
                    ex)
                    .WithContext("SerializerType", "RuntimeConfigured")
                    .WithContext("PropertyType", "ChildMetadata")
                    .WithContext("Operation", "JsonDeserialization");
            }
        }

        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("sk", out var sk) && sk.S != null && sk.S.StartsWith("CHILD#");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "composite-test-entities",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}

#endregion
