using AwesomeAssertions;
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

        // Verify compilation - include SecurityMetadata source since generated code references it
        var entitySource = CreateEntitySource(entity);
        var securityMetadata = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource, securityMetadata);

        // Assert - Verify both encryption and sensitive marking
        result.Should().Contain("await fieldEncryptor.EncryptAsync(",
            "should generate encryption code");
        result.Should().Contain("new AttributeValue { B = new MemoryStream(",
            "should store as Binary AttributeValue");
        
        // The sensitive field should be in the security metadata
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

    [Fact]
    public void GenerateEntityImplementation_FromDynamoDbAsync_ContainsDecryptionFailureModeCheck()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify generated FromDynamoDbAsync contains DecryptionFailureMode check
        result.Should().Contain("options?.DecryptionFailureMode == DecryptionFailureMode.SkipFields",
            "FromDynamoDbAsync should check DecryptionFailureMode for encrypted fields");
    }

    [Fact]
    public void GenerateEntityImplementation_FromDynamoDbAsync_ContainsIsIntegrityFailureCall()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify generated FromDynamoDbAsync contains EncryptionFailureClassifier.IsIntegrityFailure call
        result.Should().Contain("EncryptionFailureClassifier.IsIntegrityFailure(ex)",
            "FromDynamoDbAsync should call EncryptionFailureClassifier.IsIntegrityFailure to classify exceptions");
    }

    [Fact]
    public void GenerateEntityImplementation_FromDynamoDbAsync_ContainsLogWarningWithEncryptionFieldSkipped()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify generated FromDynamoDbAsync contains LogWarning call with LogEventIds.EncryptionFieldSkipped
        result.Should().Contain("LogWarning(LogEventIds.EncryptionFieldSkipped",
            "FromDynamoDbAsync should log a warning with LogEventIds.EncryptionFieldSkipped when skipping fields");
    }

    [Fact]
    public void GenerateEntityImplementation_ToDynamoDbAsync_DoesNotContainDecryptionFailureModeReferences()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Extract only the ToDynamoDbAsync method portion to verify it doesn't reference DecryptionFailureMode
        var toDynamoDbStart = result.IndexOf("public static async Task<Dictionary<string, AttributeValue>> ToDynamoDbAsync");
        var fromDynamoDbStart = result.IndexOf("public static async Task<");
        
        // Find the ToDynamoDbAsync method body - it starts at toDynamoDbStart
        // and ends before the next method (FromDynamoDbAsync single)
        toDynamoDbStart.Should().BeGreaterThan(-1, "should contain ToDynamoDbAsync method");
        
        // Find the end of ToDynamoDbAsync by looking for the next public static method after it
        var afterToDynamoDb = result.Substring(toDynamoDbStart + 10);
        var nextMethodIndex = afterToDynamoDb.IndexOf("public static");
        var toDynamoDbBody = nextMethodIndex > 0 
            ? afterToDynamoDb.Substring(0, nextMethodIndex) 
            : afterToDynamoDb;

        // Assert - ToDynamoDbAsync should NOT contain DecryptionFailureMode references
        toDynamoDbBody.Should().NotContain("DecryptionFailureMode",
            "ToDynamoDbAsync should not reference DecryptionFailureMode - writes always throw on failure");
        toDynamoDbBody.Should().NotContain("EncryptionFailureClassifier",
            "ToDynamoDbAsync should not reference EncryptionFailureClassifier - writes always throw on failure");
    }

    [Fact]
    public void GenerateEntityImplementation_FromDynamoDbAsync_LogsEntityTypeAndPropertyName()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify log message contains entity type name and property name
        result.Should().Contain("\"SecretData\"",
            "log message should contain the property name");
        result.Should().Contain("\"SecureEntity\"",
            "log message should contain the entity type name");
    }

    [Fact]
    public void GenerateEntityImplementation_FromDynamoDbAsync_SkipFieldsMode_NullEncryptor_LogsWarning()
    {
        // Arrange
        var entity = CreateEncryptedEntity();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify the null encryptor path also has SkipFields check and logging
        result.Should().Contain("\"No IFieldEncryptor configured\"",
            "should log reason when skipping due to null encryptor");
    }

    /// <summary>
    /// Helper to create a standard encrypted entity model for failure mode tests.
    /// </summary>
    private static EntityModel CreateEncryptedEntity()
    {
        return new EntityModel
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
        sb.AppendLine("using Oproto.FluentDynamoDb.Providers.Encryption;");
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
