using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for encrypted properties automatically being marked as sensitive.
/// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
/// **Validates: Requirements 1.1, 1.3**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class EncryptedSensitivePropertyTests
{
    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// For any entity with a property marked [Encrypted], the generated PropertyMetadata 
    /// SHALL have IsSensitive = true.
    /// 
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EncryptedProperty_IsAutomaticallySensitive()
    {
        return Prop.ForAll(
            GenerateValidPropertyName(),
            GenerateValidAttributeName(),
            (propertyName, attributeName) =>
            {
                // Arrange - Create an entity with an encrypted property
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
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = "string",
                            Security = new SecurityInfo 
                            { 
                                IsEncrypted = true,
                                // IsSensitive should be automatically set to true by SecurityAttributeAnalyzer
                                IsSensitive = true, // This simulates what the analyzer does
                                EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                            }
                        }
                    }
                };

                // Act - Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify that IsSensitive = true is emitted for the encrypted property
                var hasSensitiveFlag = result.Contains("IsSensitive = true");
                var hasEncryptedFlag = result.Contains("IsEncrypted = true");
                
                // Also verify the property is in the sensitive fields for logging redaction
                var securityMetadata = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);
                var isInSensitiveFields = securityMetadata.Contains($"\"{attributeName}\"");

                return (hasSensitiveFlag && hasEncryptedFlag && isInSensitiveFields).ToProperty()
                    .Label($"Encrypted property '{propertyName}' should be automatically sensitive. " +
                           $"HasSensitiveFlag: {hasSensitiveFlag}, HasEncryptedFlag: {hasEncryptedFlag}, " +
                           $"IsInSensitiveFields: {isInSensitiveFields}");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// For any entity with a property that has both [Encrypted] and [Sensitive] attributes,
    /// the generated PropertyMetadata SHALL have IsSensitive = true without duplication or conflict.
    /// 
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EncryptedAndSensitiveProperty_NoConflict()
    {
        return Prop.ForAll(
            GenerateValidPropertyName(),
            GenerateValidAttributeName(),
            (propertyName, attributeName) =>
            {
                // Arrange - Create an entity with a property that has both encrypted and sensitive
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
                            PropertyName = propertyName,
                            AttributeName = attributeName,
                            PropertyType = "string",
                            Security = new SecurityInfo 
                            { 
                                IsEncrypted = true,
                                IsSensitive = true, // Both flags set
                                EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                            }
                        }
                    }
                };

                // Act - Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);

                // Assert - Verify that IsSensitive = true appears exactly once per property
                // and there's no duplication or conflict
                var sensitiveCount = CountOccurrences(result, "IsSensitive = true");
                var encryptedCount = CountOccurrences(result, "IsEncrypted = true");
                
                // Should have exactly one IsSensitive = true for the encrypted property
                // (the Id property is not sensitive)
                var correctSensitiveCount = sensitiveCount == 1;
                var correctEncryptedCount = encryptedCount == 1;

                return (correctSensitiveCount && correctEncryptedCount).ToProperty()
                    .Label($"Property with both [Encrypted] and [Sensitive] should have no conflict. " +
                           $"SensitiveCount: {sensitiveCount} (expected 1), EncryptedCount: {encryptedCount} (expected 1)");
            });
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// For any entity with multiple encrypted properties, each encrypted property
    /// SHALL have IsSensitive = true in the generated PropertyMetadata.
    /// 
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleEncryptedProperties_AllAreSensitive()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt().Filter(n => n.Get >= 1 && n.Get <= 5),
            encryptedPropertyCount =>
            {
                // Arrange - Create an entity with multiple encrypted properties
                var properties = new List<PropertyModel>
                {
                    new PropertyModel
                    {
                        PropertyName = "Id",
                        AttributeName = "pk",
                        PropertyType = "string",
                        IsPartitionKey = true
                    }
                };

                for (int i = 0; i < encryptedPropertyCount.Get; i++)
                {
                    properties.Add(new PropertyModel
                    {
                        PropertyName = $"EncryptedField{i}",
                        AttributeName = $"encrypted_field_{i}",
                        PropertyType = "string",
                        Security = new SecurityInfo 
                        { 
                            IsEncrypted = true,
                            IsSensitive = true,
                            EncryptionConfig = new EncryptionConfig { CacheTtlSeconds = 300 }
                        }
                    });
                }

                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    Namespace = "TestNamespace",
                    TableName = "test-table",
                    Properties = properties.ToArray()
                };

                // Act - Generate the security metadata
                var securityMetadata = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);

                // Assert - Verify all encrypted properties are in the sensitive fields
                var allInSensitiveFields = true;
                for (int i = 0; i < encryptedPropertyCount.Get; i++)
                {
                    if (!securityMetadata.Contains($"\"encrypted_field_{i}\""))
                    {
                        allInSensitiveFields = false;
                        break;
                    }
                }

                return allInSensitiveFields.ToProperty()
                    .Label($"All {encryptedPropertyCount.Get} encrypted properties should be in sensitive fields. " +
                           $"AllInSensitiveFields: {allInSensitiveFields}");
            });
    }

    /// <summary>
    /// Generates valid C# property names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidPropertyName()
    {
        return Arb.From(
            from length in Gen.Choose(3, 20)
            from firstChar in Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 
                                           'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z')
            from restChars in Gen.ArrayOf(length - 1, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            select firstChar + new string(restChars)
        );
    }

    /// <summary>
    /// Generates valid DynamoDB attribute names for testing.
    /// </summary>
    private static Arbitrary<string> GenerateValidAttributeName()
    {
        return Arb.From(
            from length in Gen.Choose(3, 20)
            from chars in Gen.ArrayOf(length, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            let name = new string(chars)
            where !char.IsDigit(name[0]) // Attribute names shouldn't start with a digit
            select name
        );
    }

    /// <summary>
    /// Counts the number of occurrences of a substring in a string.
    /// </summary>
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// Direct test using EntityAnalyzer to verify that when analyzing source code with [Encrypted],
    /// the IsSensitive flag is automatically set to true.
    /// 
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithEncryptedProperty_SetsIsSensitiveTrue()
    {
        // Arrange - Source code with an encrypted property
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [Encrypted]
        [DynamoDbAttribute(""secret_data"")]
        public string SecretData { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        
        var encryptedProperty = result!.Properties.FirstOrDefault(p => p.PropertyName == "SecretData");
        encryptedProperty.Should().NotBeNull("encrypted property should be found");
        encryptedProperty!.Security.Should().NotBeNull("security info should be set");
        encryptedProperty.Security!.IsEncrypted.Should().BeTrue("property should be marked as encrypted");
        encryptedProperty.Security.IsSensitive.Should().BeTrue("encrypted property should automatically be marked as sensitive");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// Direct test using EntityAnalyzer to verify that when analyzing source code with both
    /// [Encrypted] and [Sensitive], there is no conflict and IsSensitive is true.
    /// 
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithEncryptedAndSensitiveProperty_NoConflict()
    {
        // Arrange - Source code with both [Encrypted] and [Sensitive] attributes
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [Encrypted]
        [Sensitive]
        [DynamoDbAttribute(""secret_data"")]
        public string SecretData { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        
        var encryptedProperty = result!.Properties.FirstOrDefault(p => p.PropertyName == "SecretData");
        encryptedProperty.Should().NotBeNull("encrypted property should be found");
        encryptedProperty!.Security.Should().NotBeNull("security info should be set");
        encryptedProperty.Security!.IsEncrypted.Should().BeTrue("property should be marked as encrypted");
        encryptedProperty.Security.IsSensitive.Should().BeTrue("property should be marked as sensitive");
    }

    /// <summary>
    /// **Feature: api-enhancements-v0.9, Property 1: Encrypted properties are automatically sensitive**
    /// 
    /// Direct test to verify that a property with only [Sensitive] (not [Encrypted]) 
    /// is sensitive but not encrypted.
    /// 
    /// **Validates: Requirements 1.1 (negative case)**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithOnlySensitiveProperty_IsNotEncrypted()
    {
        // Arrange - Source code with only [Sensitive] attribute
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [Sensitive]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        
        var sensitiveProperty = result!.Properties.FirstOrDefault(p => p.PropertyName == "Email");
        sensitiveProperty.Should().NotBeNull("sensitive property should be found");
        sensitiveProperty!.Security.Should().NotBeNull("security info should be set");
        sensitiveProperty.Security!.IsSensitive.Should().BeTrue("property should be marked as sensitive");
        sensitiveProperty.Security.IsEncrypted.Should().BeFalse("property should NOT be marked as encrypted");
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}
