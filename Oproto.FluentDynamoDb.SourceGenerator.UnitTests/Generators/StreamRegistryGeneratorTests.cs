using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class StreamRegistryGeneratorTests
{
    [Fact]
    public void GenerateOnStreamMethod_WithoutStreamConversion_ReturnsEmptyString()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "TestEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = false,
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().BeEmpty("OnStream should not be generated when no entities have stream conversion enabled");
    }

    [Fact]
    public void GenerateOnStreamMethod_WithStreamConversion_GeneratesOnStreamMethod()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "EntityType",
                    ExactValue = "User",
                    Strategy = DiscriminatorStrategy.ExactMatch
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");
        result.Should().Contain("public partial class TestTable", "should generate partial class");
        result.Should().Contain("OnStream", "should generate OnStream method");
        result.Should().Contain("StreamDiscriminatorRegistry", "should generate registry class");
        result.Should().Contain("Amazon.Lambda.DynamoDBEvents", "should use Lambda types");
        result.Should().Contain("Oproto.FluentDynamoDb.Streams.Processing", "should use Streams.Processing namespace");
    }

    [Fact]
    public void GenerateOnStreamMethod_WithMultipleEntities_GeneratesRegistryWithAllEntities()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "EntityType",
                    ExactValue = "User",
                    Strategy = DiscriminatorStrategy.ExactMatch
                },
                Properties = Array.Empty<PropertyModel>()
            },
            new EntityModel
            {
                ClassName = "OrderEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "EntityType",
                    ExactValue = "Order",
                    Strategy = DiscriminatorStrategy.ExactMatch
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain("typeof(UserEntity)", "should include UserEntity in registry");
        result.Should().Contain("typeof(OrderEntity)", "should include OrderEntity in registry");
        result.Should().Contain("Property = \"EntityType\"", "should use correct discriminator property");
        result.Should().Contain("Value = \"User\"", "should include User discriminator value");
        result.Should().Contain("Value = \"Order\"", "should include Order discriminator value");
    }

    [Fact]
    public void GenerateOnStreamMethod_WithPrefixPattern_GeneratesStartsWithStrategy()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "SK",
                    Pattern = "USER#*",
                    Strategy = DiscriminatorStrategy.StartsWith
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain("Property = \"SK\"", "should use SK as discriminator property");
        result.Should().Contain("Pattern = \"USER#*\"", "should include pattern with wildcard");
        result.Should().Contain("Strategy = Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorStrategy.StartsWith", 
            "should use StartsWith strategy");
        result.Should().Contain("Value = \"USER#\"", "should include value without wildcard");
    }

    [Fact]
    public void GenerateOnStreamMethod_WithSuffixPattern_GeneratesEndsWithStrategy()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "SK",
                    Pattern = "*#USER",
                    Strategy = DiscriminatorStrategy.EndsWith
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain("Pattern = \"*#USER\"", "should include pattern with leading wildcard");
        result.Should().Contain("Strategy = Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorStrategy.EndsWith", 
            "should use EndsWith strategy");
        result.Should().Contain("Value = \"#USER\"", "should include value without wildcard");
    }

    [Fact]
    public void GenerateOnStreamMethod_WithContainsPattern_GeneratesContainsStrategy()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "SK",
                    Pattern = "*#USER#*",
                    Strategy = DiscriminatorStrategy.Contains
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain("Pattern = \"*#USER#*\"", "should include pattern with wildcards");
        result.Should().Contain("Strategy = Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorStrategy.Contains", 
            "should use Contains strategy");
        result.Should().Contain("Value = \"#USER#\"", "should include value without wildcards");
    }

    [Fact]
    public void GenerateOnStreamMethod_UsesDiscriminatorPropertyFromFirstEntity()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                ClassName = "UserEntity",
                Namespace = "TestNamespace",
                TableName = "test-table",
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig
                {
                    PropertyName = "EntityType",
                    ExactValue = "User",
                    Strategy = DiscriminatorStrategy.ExactMatch
                },
                Properties = Array.Empty<PropertyModel>()
            }
        };

        // Act
        var result = StreamRegistryGenerator.GenerateOnStreamMethod(
            "test-table",
            entities,
            "TestTable",
            "TestNamespace");

        // Assert
        result.Should().Contain(".WithDiscriminator(\"EntityType\")", 
            "should use discriminator property from first entity in OnStream method");
    }

    [Fact]
    public void ValidateConsistentDiscriminatorProperty_WithSameProperty_ReturnsTrue()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "EntityType" }
            },
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "EntityType" }
            }
        };

        // Act
        var result = StreamRegistryGenerator.ValidateConsistentDiscriminatorProperty(entities);

        // Assert
        result.Should().BeTrue("all entities use the same discriminator property");
    }

    [Fact]
    public void ValidateConsistentDiscriminatorProperty_WithDifferentProperties_ReturnsFalse()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "EntityType" }
            },
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "SK" }
            }
        };

        // Act
        var result = StreamRegistryGenerator.ValidateConsistentDiscriminatorProperty(entities);

        // Assert
        result.Should().BeFalse("entities use different discriminator properties");
    }

    [Fact]
    public void GetDistinctDiscriminatorProperties_ReturnsAllUniqueProperties()
    {
        // Arrange
        var entities = new List<EntityModel>
        {
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "EntityType" }
            },
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "SK" }
            },
            new EntityModel
            {
                GenerateStreamConversion = true,
                Discriminator = new DiscriminatorConfig { PropertyName = "EntityType" }
            }
        };

        // Act
        var result = StreamRegistryGenerator.GetDistinctDiscriminatorProperties(entities);

        // Assert
        result.Should().HaveCount(2, "there are two distinct discriminator properties");
        result.Should().Contain("EntityType", "EntityType is one of the properties");
        result.Should().Contain("SK", "SK is one of the properties");
    }
}

