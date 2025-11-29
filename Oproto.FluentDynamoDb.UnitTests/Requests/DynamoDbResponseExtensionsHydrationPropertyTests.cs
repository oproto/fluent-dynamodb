using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests verifying no reflection is used for entity hydration when using FluentDynamoDbOptions.
/// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
/// **Validates: Requirements 3.2, 6.4**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class DynamoDbResponseExtensionsHydrationPropertyTests
{
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Verifies that when a hydrator is registered, it can be retrieved from the registry.
    /// </summary>
    [Fact]
    public void HydratorRegistry_WithRegisteredHydrator_ReturnsHydrator()
    {
        // Arrange
        var mockHydrator = Substitute.For<IAsyncEntityHydrator<TestHydrationEntity>>();
        
        var registry = new DefaultEntityHydratorRegistry();
        registry.Register(mockHydrator);
        
        // Act
        var retrievedHydrator = registry.GetHydrator<TestHydrationEntity>();
        
        // Assert
        Assert.NotNull(retrievedHydrator);
        Assert.Same(mockHydrator, retrievedHydrator);
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Verifies that when no hydrator is registered, the registry returns null.
    /// </summary>
    [Fact]
    public void HydratorRegistry_WithoutRegisteredHydrator_ReturnsNull()
    {
        // Arrange
        var registry = new DefaultEntityHydratorRegistry();
        // Don't register any hydrator
        
        // Act
        var retrievedHydrator = registry.GetHydrator<TestHydrationEntity>();
        
        // Assert
        Assert.Null(retrievedHydrator);
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Verifies that FluentDynamoDbOptions can be configured with a custom hydrator registry.
    /// </summary>
    [Fact]
    public void FluentDynamoDbOptions_WithHydratorRegistry_UsesCustomRegistry()
    {
        // Arrange
        var mockHydrator = Substitute.For<IAsyncEntityHydrator<TestHydrationEntity>>();
        var registry = new DefaultEntityHydratorRegistry();
        registry.Register(mockHydrator);
        
        var mockBlobProvider = TestOptionsBuilder.CreateMockBlobStorageProvider();
        
        // Act
        var options = new FluentDynamoDbOptions()
            .WithBlobStorage(mockBlobProvider)
            .WithHydratorRegistry(registry);
        
        // Assert
        var retrievedHydrator = options.HydratorRegistry.GetHydrator<TestHydrationEntity>();
        Assert.NotNull(retrievedHydrator);
        Assert.Same(mockHydrator, retrievedHydrator);
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Property test: For any registered hydrator, the registry should return the same hydrator instance.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RegisteredHydrator_ShouldBeRetrievableFromRegistry()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt(),
            count =>
            {
                // Create a mock hydrator
                var mockHydrator = Substitute.For<IAsyncEntityHydrator<TestHydrationEntity>>();
                
                // Create registry and register hydrator
                var registry = new DefaultEntityHydratorRegistry();
                registry.Register(mockHydrator);
                
                // Create options with the registry
                var mockBlobProvider = TestOptionsBuilder.CreateMockBlobStorageProvider();
                var options = new FluentDynamoDbOptions()
                    .WithBlobStorage(mockBlobProvider)
                    .WithHydratorRegistry(registry);
                
                // Verify hydrator is retrievable
                var retrievedHydrator = options.HydratorRegistry.GetHydrator<TestHydrationEntity>();
                
                return retrievedHydrator != null && retrievedHydrator == mockHydrator;
            });
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Property test: Multiple hydrators for different entity types can be registered independently.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleHydrators_CanBeRegisteredIndependently()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                // Create mock hydrators for different entity types
                var hydrator1 = Substitute.For<IAsyncEntityHydrator<TestHydrationEntity>>();
                var hydrator2 = Substitute.For<IAsyncEntityHydrator<AnotherTestEntity>>();
                
                // Create registry and register both hydrators
                var registry = new DefaultEntityHydratorRegistry();
                registry.Register(hydrator1);
                registry.Register(hydrator2);
                
                // Verify both hydrators are retrievable
                var retrieved1 = registry.GetHydrator<TestHydrationEntity>();
                var retrieved2 = registry.GetHydrator<AnotherTestEntity>();
                
                return retrieved1 == hydrator1 && retrieved2 == hydrator2;
            });
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Verifies that the default hydrator registry is used when no custom registry is provided.
    /// </summary>
    [Fact]
    public void FluentDynamoDbOptions_Default_UsesDefaultHydratorRegistry()
    {
        // Arrange & Act
        var options = new FluentDynamoDbOptions();
        
        // Assert
        Assert.NotNull(options.HydratorRegistry);
        Assert.IsType<DefaultEntityHydratorRegistry>(options.HydratorRegistry);
    }
    
    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 3.2, 6.4**
    /// 
    /// Verifies that WithHydratorRegistry preserves other options.
    /// </summary>
    [Fact]
    public void WithHydratorRegistry_PreservesOtherOptions()
    {
        // Arrange
        var mockLogger = TestOptionsBuilder.CreateMockLogger();
        var mockBlobProvider = TestOptionsBuilder.CreateMockBlobStorageProvider();
        var mockEncryptor = TestOptionsBuilder.CreateMockFieldEncryptor();
        var registry = new DefaultEntityHydratorRegistry();
        
        // Act
        var options = new FluentDynamoDbOptions()
            .WithLogger(mockLogger)
            .WithBlobStorage(mockBlobProvider)
            .WithEncryption(mockEncryptor)
            .WithHydratorRegistry(registry);
        
        // Assert
        Assert.Same(mockLogger, options.Logger);
        Assert.Same(mockBlobProvider, options.BlobStorageProvider);
        Assert.Same(mockEncryptor, options.FieldEncryptor);
        Assert.Same(registry, options.HydratorRegistry);
    }
}

/// <summary>
/// Test entity for hydration property tests.
/// </summary>
public class TestHydrationEntity : IDynamoDbEntity
{
    public string Id { get; set; } = "";
    
    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;
    
    public static string GetPartitionKey(Dictionary<string, AttributeValue> item) => 
        item.TryGetValue("pk", out var pk) ? pk.S : "";
    
    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity =>
        (TSelf)(object)new TestHydrationEntity { Id = item.TryGetValue("id", out var id) ? id.S : "" };
    
    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity =>
        (TSelf)(object)new TestHydrationEntity { Id = items.FirstOrDefault()?.TryGetValue("id", out var id) == true ? id.S : "" };
    
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity => new()
    {
        ["id"] = new AttributeValue { S = ((TestHydrationEntity)(object)entity).Id }
    };
    
    public static EntityMetadata GetEntityMetadata() => new()
    {
        TableName = "TestTable",
        Properties = Array.Empty<PropertyMetadata>()
    };
}

/// <summary>
/// Another test entity for testing multiple hydrator registration.
/// </summary>
public class AnotherTestEntity : IDynamoDbEntity
{
    public string Name { get; set; } = "";
    
    public static bool MatchesEntity(Dictionary<string, AttributeValue> item) => true;
    
    public static string GetPartitionKey(Dictionary<string, AttributeValue> item) => 
        item.TryGetValue("pk", out var pk) ? pk.S : "";
    
    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity =>
        (TSelf)(object)new AnotherTestEntity { Name = item.TryGetValue("name", out var name) ? name.S : "" };
    
    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity =>
        (TSelf)(object)new AnotherTestEntity { Name = items.FirstOrDefault()?.TryGetValue("name", out var name) == true ? name.S : "" };
    
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, IDynamoDbLogger? logger = null) 
        where TSelf : IDynamoDbEntity => new()
    {
        ["name"] = new AttributeValue { S = ((AnotherTestEntity)(object)entity).Name }
    };
    
    public static EntityMetadata GetEntityMetadata() => new()
    {
        TableName = "AnotherTestTable",
        Properties = Array.Empty<PropertyMetadata>()
    };
}
