using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class SecurityMetadataGeneratorTests
{
    [Fact]
    public void GenerateSecurityMetadata_WithSensitiveFields_GeneratesHashSet()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
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
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                },
                new PropertyModel
                {
                    PropertyName = "PhoneNumber",
                    AttributeName = "phone",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
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
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert - Verify HashSet generation
        result.Should().Contain("namespace TestNamespace", 
            "should generate code in the correct namespace");
        result.Should().Contain("internal static class TestEntitySecurityMetadata",
            "should generate security metadata class with correct name");
        result.Should().Contain("private static readonly HashSet<string> SensitiveFields = new()",
            "should generate HashSet for sensitive fields");
        result.Should().Contain("\"email\",",
            "should include email attribute in sensitive fields");
        result.Should().Contain("\"phone\",",
            "should include phone attribute in sensitive fields");
        result.Should().NotContain("\"pk\"",
            "should not include non-sensitive partition key");
        result.Should().NotContain("\"name\"",
            "should not include non-sensitive name field");
    }

    [Fact]
    public void GenerateSecurityMetadata_WithSensitiveFields_GeneratesIsSensitiveFieldMethod()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "UserEntity",
            Namespace = "TestNamespace",
            TableName = "users",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "UserId",
                    AttributeName = "user_id",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "SocialSecurityNumber",
                    AttributeName = "ssn",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                }
            }
        };

        // Act
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert - Verify IsSensitiveField method
        result.ShouldContainMethod("IsSensitiveField");
        result.Should().Contain("public static bool IsSensitiveField(string attributeName)",
            "should generate IsSensitiveField method with correct signature");
        result.Should().Contain("return SensitiveFields.Contains(attributeName);",
            "should check if attribute is in sensitive fields set");
        result.Should().Contain("/// <summary>",
            "should include XML documentation");
        result.Should().Contain("/// Checks if a DynamoDB attribute name is marked as sensitive.",
            "should document the method purpose");
    }

    [Fact]
    public void GenerateSecurityMetadata_WithNoSensitiveFields_ReturnsEmptyString()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
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
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Assert
        result.Should().BeEmpty("should not generate metadata when no sensitive fields exist");
    }

    [Fact]
    public void GenerateSecurityMetadata_WithMultipleSensitiveFields_IncludesAllInHashSet()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "CustomerEntity",
            Namespace = "TestNamespace",
            TableName = "customers",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "CustomerId",
                    AttributeName = "customer_id",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Email",
                    AttributeName = "email",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                },
                new PropertyModel
                {
                    PropertyName = "CreditCardNumber",
                    AttributeName = "cc_number",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                },
                new PropertyModel
                {
                    PropertyName = "SocialSecurityNumber",
                    AttributeName = "ssn",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                },
                new PropertyModel
                {
                    PropertyName = "Address",
                    AttributeName = "address",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                }
            }
        };

        // Act
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert - Verify all sensitive fields are included
        result.Should().Contain("\"email\",", "should include email");
        result.Should().Contain("\"cc_number\",", "should include credit card number");
        result.Should().Contain("\"ssn\",", "should include SSN");
        result.Should().Contain("\"address\",", "should include address");
        result.Should().NotContain("\"customer_id\"", "should not include non-sensitive customer ID");
    }

    [Fact]
    public void GenerateSecurityMetadata_WithSensitiveAndEncryptedFields_IncludesBothInHashSet()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-data",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "SensitiveData",
                    AttributeName = "sensitive_data",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                },
                new PropertyModel
                {
                    PropertyName = "EncryptedData",
                    AttributeName = "encrypted_data",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsSensitive = true,
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(result);

        // Assert - Both sensitive and encrypted fields should be in the HashSet
        result.Should().Contain("\"sensitive_data\",",
            "should include field marked only as sensitive");
        result.Should().Contain("\"encrypted_data\",",
            "should include field marked as both sensitive and encrypted");
    }

    [Fact]
    public void GenerateSecurityMetadata_GeneratesAutoGeneratedComment()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
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
                    PropertyName = "Secret",
                    AttributeName = "secret",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                }
            }
        };

        // Act
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Assert
        result.Should().Contain("// <auto-generated />",
            "should include auto-generated comment");
        result.Should().Contain("// This code was generated by the DynamoDB Source Generator.",
            "should include generator attribution");
        result.Should().Contain("// Changes to this file may be lost when the code is regenerated.",
            "should include warning about manual changes");
    }

    [Fact]
    public void GenerateSecurityMetadata_GeneratesXmlDocumentation()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "DocumentEntity",
            Namespace = "TestNamespace",
            TableName = "documents",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "DocumentId",
                    AttributeName = "doc_id",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Content",
                    AttributeName = "content",
                    PropertyType = "string",
                    Security = new SecurityInfo { IsSensitive = true }
                }
            }
        };

        // Act
        var result = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

        // Assert - Verify XML documentation
        result.Should().Contain("/// <summary>",
            "should include XML summary tags");
        result.Should().Contain("/// Security metadata for DocumentEntity.",
            "should document the entity name");
        result.Should().Contain("/// Contains information about sensitive fields for logging redaction.",
            "should document the purpose");
        result.Should().Contain("/// Set of DynamoDB attribute names that are marked as sensitive.",
            "should document the SensitiveFields set");
        result.Should().Contain("/// <param name=\"attributeName\">The DynamoDB attribute name to check.</param>",
            "should document method parameters");
        result.Should().Contain("/// <returns>True if the attribute is sensitive, false otherwise.</returns>",
            "should document return values");
    }
}
