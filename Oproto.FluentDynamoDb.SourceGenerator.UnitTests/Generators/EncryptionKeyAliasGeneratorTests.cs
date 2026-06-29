using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests that the source generator correctly propagates KeyAlias from EncryptedAttribute
/// into the generated FieldEncryptionContext initializer.
/// Requirements: 6.1, 6.2, 6.3
/// </summary>
[Trait("Category", "Unit")]
public class EncryptionKeyAliasGeneratorTests
{
    [Fact]
    public void GenerateEntityImplementation_WithKeyAlias_EmitsKeyAliasInEncryptionContext()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "PiiEntity",
            Namespace = "TestNamespace",
            TableName = "pii-table",
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
                    PropertyName = "SocialSecurityNumber",
                    AttributeName = "ssn",
                    PropertyType = "string",
                    Security = new SecurityInfo
                    {
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig
                        {
                            CacheTtlSeconds = 300,
                            KeyAlias = "pii"
                        }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - KeyAlias = "pii" must be present in the generated code
        result.Should().Contain("KeyAlias = \"pii\"",
            "should emit KeyAlias in FieldEncryptionContext when specified on attribute");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutKeyAlias_OmitsKeyAliasFromEncryptionContext()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "BasicEncryptedEntity",
            Namespace = "TestNamespace",
            TableName = "basic-table",
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
                    Security = new SecurityInfo
                    {
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig
                        {
                            CacheTtlSeconds = 300
                            // KeyAlias is null by default
                        }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - KeyAlias must NOT be present in the generated code
        result.Should().NotContain("KeyAlias",
            "should omit KeyAlias from FieldEncryptionContext when not specified on attribute");
    }

    [Fact]
    public void GenerateEntityImplementation_WithEmptyKeyAlias_OmitsKeyAliasFromEncryptionContext()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "EmptyAliasEntity",
            Namespace = "TestNamespace",
            TableName = "empty-alias-table",
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
                    PropertyName = "EncryptedField",
                    AttributeName = "encrypted_field",
                    PropertyType = "string",
                    Security = new SecurityInfo
                    {
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig
                        {
                            CacheTtlSeconds = 300,
                            KeyAlias = ""
                        }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - KeyAlias must NOT be present when empty string
        result.Should().NotContain("KeyAlias",
            "should omit KeyAlias from FieldEncryptionContext when KeyAlias is empty string");
    }

    [Fact]
    public void GenerateEntityImplementation_WithWhitespaceKeyAlias_OmitsKeyAliasFromEncryptionContext()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "WhitespaceAliasEntity",
            Namespace = "TestNamespace",
            TableName = "whitespace-alias-table",
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
                    PropertyName = "EncryptedField",
                    AttributeName = "encrypted_field",
                    PropertyType = "string",
                    Security = new SecurityInfo
                    {
                        IsEncrypted = true,
                        EncryptionConfig = new EncryptionConfig
                        {
                            CacheTtlSeconds = 300,
                            KeyAlias = "   "
                        }
                    }
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Verify compilation
        var entitySource = CreateEntitySource(entity);
        CompilationVerifier.AssertGeneratedCodeCompiles(result, entitySource);

        // Assert - KeyAlias must NOT be present when whitespace-only
        result.Should().NotContain("KeyAlias",
            "should omit KeyAlias from FieldEncryptionContext when KeyAlias is whitespace-only");
    }

    /// <summary>
    /// Helper method to create entity source code from an EntityModel for compilation testing.
    /// </summary>
    private static string CreateEntitySource(EntityModel entity)
    {
        var sb = new System.Text.StringBuilder();

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
