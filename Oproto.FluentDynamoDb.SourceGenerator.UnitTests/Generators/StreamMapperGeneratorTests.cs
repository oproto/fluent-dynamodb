using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class StreamMapperGeneratorTests
{
    [Fact]
    public void GenerateStreamConversion_WithoutAttribute_ReturnsEmptyString()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            GenerateStreamConversion = false,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var result = StreamMapperGenerator.GenerateStreamConversion(entity);

        // Assert
        result.Should().BeEmpty("stream conversion should not be generated without the attribute");
    }

    [Fact]
    public void GenerateStreamConversion_WithAttribute_GeneratesFromDynamoDbStreamMethod()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            GenerateStreamConversion = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = StreamMapperGenerator.GenerateStreamConversion(entity);

        // Assert
        result.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");
        result.Should().Contain("public partial class TestEntity", "should generate partial class");
        result.Should().Contain("FromDynamoDbStream", "should generate FromDynamoDbStream method");
        result.Should().Contain("FromStreamImage", "should generate FromStreamImage helper method");
        result.Should().Contain("Amazon.Lambda.DynamoDBEvents", "should use Lambda AttributeValue types");
        result.Should().Contain("DynamoDBEvent.AttributeValue", "should use Lambda AttributeValue type");
    }

    [Fact]
    public void GenerateStreamConversion_WithEncryptedProperty_IncludesEncryptionSupport()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
            GenerateStreamConversion = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "SecretData",
                    AttributeName = "secret",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsEncrypted = true }
                }
            }
        };

        // Act
        var result = StreamMapperGenerator.GenerateStreamConversion(entity);

        // Assert
        result.Should().Contain("IFieldEncryptor", "should include field encryptor parameter");
        result.Should().Contain("fieldEncryptor", "should use field encryptor for decryption");
        result.Should().Contain("DynamoDbOperationContext.EncryptionContextId", "should use encryption context");
    }

    [Fact]
    public void GenerateStreamConversion_WithDiscriminator_IncludesValidation()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "UserEntity",
            Namespace = "TestNamespace",
            TableName = "multi-table",
            GenerateStreamConversion = true,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "entity_type",
                ExactValue = "USER",
                Strategy = DiscriminatorStrategy.ExactMatch
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "EntityType",
                    AttributeName = "entity_type",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = StreamMapperGenerator.GenerateStreamConversion(entity);

        // Assert
        result.Should().Contain("Validate discriminator", "should include discriminator validation");
        result.Should().Contain("DiscriminatorMismatchException", "should throw exception on mismatch");
        result.Should().Contain("entity_type", "should check discriminator property");
    }

    [Fact]
    public void GenerateStreamConversion_WithNullableProperties_HandlesNullCorrectly()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            GenerateStreamConversion = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "OptionalField",
                    AttributeName = "optional",
                    PropertyType = "string?",
                    IsNullable = true
                }
            }
        };

        // Act
        var result = StreamMapperGenerator.GenerateStreamConversion(entity);

        // Assert
        result.Should().Contain("if (item == null) return null", "should handle null input");
        result.Should().Contain("TryGetValue", "should check for property existence");
    }
}
