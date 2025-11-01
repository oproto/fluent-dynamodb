using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class EncryptionCodeGeneratorTests
{
    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_GeneratesEncryptAsyncCall()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    AttributeName = "secret_data",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify EncryptAsync call is generated
        result.Should().Contain("await fieldEncryptor.EncryptAsync(",
            "should generate async encryption call");
        result.Should().Contain("SecretDataPlaintext",
            "should create plaintext variable for encryption");
        result.Should().Contain("SecretDataCiphertext",
            "should create ciphertext variable for encrypted data");
        result.Should().Contain("System.Text.Encoding.UTF8.GetBytes(",
            "should convert property value to bytes for encryption");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_PassesFieldEncryptionContext()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "CreditCard",
                    AttributeName = "cc",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 600 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify FieldEncryptionContext is passed correctly
        result.Should().Contain("var encryptionContext = new FieldEncryptionContext",
            "should create FieldEncryptionContext");
        result.Should().Contain("ContextId = DynamoDbOperationContext.EncryptionContextId",
            "should set ContextId from ambient context");
        result.Should().Contain("CacheTtlSeconds = 600",
            "should set CacheTtlSeconds from attribute configuration");
        result.Should().Contain("encryptionContext,",
            "should pass encryption context to EncryptAsync");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_StoresAsBinaryAttributeValue()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "EncryptedField",
                    AttributeName = "encrypted_field",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify Binary AttributeValue storage
        result.Should().Contain("new AttributeValue { B = new MemoryStream(",
            "should store encrypted data as Binary (B) AttributeValue");
        result.Should().Contain("EncryptedFieldCiphertext",
            "should use ciphertext variable for Binary storage");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_GeneratesDecryptAsyncCall()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    AttributeName = "secret_data",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify DecryptAsync call is generated
        result.Should().Contain("await fieldEncryptor.DecryptAsync(",
            "should generate async decryption call");
        result.Should().Contain("SecretDataCiphertext",
            "should read ciphertext from Binary AttributeValue");
        result.Should().Contain("SecretDataPlaintext",
            "should create plaintext variable for decrypted data");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_ReadsBinaryAttributeValue()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "EncryptedField",
                    AttributeName = "encrypted_field",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify Binary AttributeValue reading
        result.Should().Contain("if (encryptedfieldValue.B != null)",
            "should check for Binary (B) AttributeValue");
        result.Should().Contain("byte[] EncryptedFieldCiphertext",
            "should declare byte array for ciphertext");
        result.Should().Contain(".ToArray()",
            "should convert MemoryStream to byte array");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEncryptedProperty_ThrowsWhenEncryptorMissing()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "EncryptedField",
                    AttributeName = "encrypted_field",
                    PropertyType = "string",
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify error handling when encryptor is null
        result.Should().Contain("if (fieldEncryptor != null)",
            "should check if field encryptor is available");
        result.Should().Contain("throw new InvalidOperationException(",
            "should throw exception when encryptor is missing");
        result.Should().Contain("is marked with [Encrypted] but no IFieldEncryptor is configured",
            "should provide helpful error message");
        result.Should().Contain("Add the Oproto.FluentDynamoDb.Encryption.Kms package",
            "should suggest adding encryption package");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCombinedSensitiveAndEncrypted_AppliesBothFeatures()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "SensitiveEncryptedData",
                    AttributeName = "sensitive_encrypted",
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
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify both encryption and sensitive marking
        result.Should().Contain("await fieldEncryptor.EncryptAsync(",
            "should generate encryption code");
        result.Should().Contain("new AttributeValue { B = new MemoryStream(",
            "should store as Binary AttributeValue");
        
        // The sensitive field should be in the security metadata
        var securityMetadata = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);
        securityMetadata.Should().Contain("\"sensitive_encrypted\"",
            "should include field in sensitive fields metadata for logging redaction");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableEncryptedProperty_HandlesNullValues()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "SecureEntity",
            Namespace = "TestNamespace",
            TableName = "secure-table",
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
                    PropertyName = "OptionalSecret",
                    AttributeName = "optional_secret",
                    PropertyType = "string?",
                    IsNullable = true,
                    Security = new SecurityInfo 
                    { 
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - Verify null handling
        result.Should().Contain("if (typedEntity.OptionalSecret != null)",
            "should check for null before encrypting nullable property");
    }

    /// <summary>
    /// Helper method to create entity source code from an EntityModel for compilation testing.
    /// </summary>
    private static string CreateEntitySource(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();
        
        // Add necessary using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Amazon.DynamoDBv2.Model;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Storage;");
        sb.AppendLine();
        
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {entity.ClassName}");
        sb.AppendLine("    {");
        
        foreach (var prop in entity.Properties)
        {
            // Handle nullable types properly
            var propertyType = prop.PropertyType;
            if (prop.IsNullable && !propertyType.EndsWith("?") && !propertyType.Contains("<"))
            {
                propertyType += "?";
            }
            sb.AppendLine($"        public {propertyType} {prop.PropertyName} {{ get; set; }}");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
}
