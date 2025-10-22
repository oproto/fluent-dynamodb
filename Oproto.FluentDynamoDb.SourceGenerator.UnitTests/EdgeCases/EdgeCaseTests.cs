using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;
using System.IO;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.EdgeCases;

/// <summary>
/// Tests for edge cases and unusual scenarios that the source generator should handle gracefully.
/// MIGRATION STATUS: Migrated to use compilation verification and semantic assertions.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void SourceGenerator_WithEmptyEntity_GeneratesMinimalCode()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""empty-table"")]
    public partial class EmptyEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var entityCode = GetGeneratedSource(result, "EmptyEntity.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        entityCode.ShouldReferenceType("IDynamoDbEntity");
        entityCode.ShouldContainMethod("ToDynamoDb");
    }

    [Fact]
    public void SourceGenerator_WithSpecialCharactersInNames_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""special-chars-table"")]
    public partial class SpecialCharsEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""field-with-dashes"")]
        public string FieldWithDashes { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""field_with_underscores"")]
        public string FieldWithUnderscores { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""field.with.dots"")]
        public string FieldWithDots { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""field123"")]
        public string FieldWithNumbers { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "SpecialCharsEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific field constant value checks
        fieldsCode.Should().Contain("public const string FieldWithDashes = \"field-with-dashes\";",
            "should preserve special characters in DynamoDB attribute names");
        fieldsCode.Should().Contain("public const string FieldWithUnderscores = \"field_with_underscores\";",
            "should preserve underscores in DynamoDB attribute names");
        fieldsCode.Should().Contain("public const string FieldWithDots = \"field.with.dots\";",
            "should preserve dots in DynamoDB attribute names");
        fieldsCode.Should().Contain("public const string FieldWithNumbers = \"field123\";",
            "should preserve numbers in DynamoDB attribute names");
    }

    [Fact]
    public void SourceGenerator_WithReservedKeywords_EscapesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""reserved-keywords-table"")]
    public partial class ReservedKeywordsEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""class"")]
        public string @class { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""namespace"")]
        public string @namespace { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""public"")]
        public string @public { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""COUNT"")]
        public string COUNT { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""SIZE"")]
        public string SIZE { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate warnings for reserved words only
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warnings
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "ReservedKeywordsEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific checks for reserved keyword escaping
        fieldsCode.Should().Contain("public const string @class = \"class\";",
            "should escape C# reserved keyword 'class' with @ prefix");
        fieldsCode.Should().Contain("public const string @namespace = \"namespace\";",
            "should escape C# reserved keyword 'namespace' with @ prefix");
        fieldsCode.Should().Contain("public const string @public = \"public\";",
            "should escape C# reserved keyword 'public' with @ prefix");
        fieldsCode.Should().Contain("public const string @COUNT = \"COUNT\";",
            "should escape DynamoDB reserved word 'COUNT' with @ prefix");
        fieldsCode.Should().Contain("public const string @SIZE = \"SIZE\";",
            "should escape DynamoDB reserved word 'SIZE' with @ prefix");
    }

    [Fact]
    public void SourceGenerator_WithVeryLongNames_HandlesCorrectly()
    {
        // Arrange
        var longPropertyName = "VeryLongPropertyNameThatExceedsNormalLengthLimitsAndTestsHowTheGeneratorHandlesExtremelyLongIdentifiers";
        var longAttributeName = "very_long_attribute_name_that_exceeds_normal_length_limits_and_tests_how_the_generator_handles_extremely_long_attribute_names";

        var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""long-names-table"")]
    public partial class LongNamesEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""{longAttributeName}"")]
        public string {longPropertyName} {{ get; set; }} = string.Empty;
    }}
}}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "LongNamesEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific check for long attribute name mapping
        fieldsCode.Should().Contain($"public const string {longPropertyName} = \"{longAttributeName}\";",
            "should handle extremely long property and attribute names correctly");
    }

    [Fact]
    public void SourceGenerator_WithNestedNamespaces_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace Very.Deeply.Nested.Namespace.Structure
{
    [DynamoDbTable(""nested-namespace-table"")]
    public partial class NestedNamespaceEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate warnings for reserved word "name" only
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warning for "name"
        result.GeneratedSources.Should().HaveCount(3);

        var entityCode = GetGeneratedSource(result, "NestedNamespaceEntity.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        entityCode.Should().Contain("namespace Very.Deeply.Nested.Namespace.Structure",
            "should preserve deeply nested namespace structure");
        entityCode.ShouldReferenceType("IDynamoDbEntity");
    }

    [Fact]
    public void SourceGenerator_WithGenericTypeConstraints_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""generic-constraints-table"")]
    public partial class GenericConstraintsEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""generic_list"")]
        public List<string> GenericList { get; set; } = new();
        
        [DynamoDbAttribute(""generic_dict"")]
        public Dictionary<string, object> GenericDict { get; set; } = new();
        
        [DynamoDbAttribute(""nullable_generic"")]
        public List<int>? NullableGeneric { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate warnings for complex collection types
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB023"); // Performance warning for complex collections
        result.GeneratedSources.Should().HaveCount(3);

        var entityCode = GetGeneratedSource(result, "GenericConstraintsEntity.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // The generator should handle generic types, even if with performance warnings
        entityCode.ShouldReferenceType("IDynamoDbEntity");
        entityCode.ShouldContainMethod("ToDynamoDb");
        entityCode.ShouldContainMethod("FromDynamoDb");
    }

    [Fact]
    public void SourceGenerator_WithCircularReferences_HandlesGracefully()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""circular-ref-table"")]
    public partial class CircularRefEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
        
        [RelatedEntity(""child#*"")]
        public List<CircularRefEntity>? Children { get; set; }
        
        [RelatedEntity(""parent"")]
        public CircularRefEntity? Parent { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code gracefully even with circular references
        result.GeneratedSources.Should().HaveCount(3);

        var entityCode = GetGeneratedSource(result, "CircularRefEntity.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // The generator should handle circular references without crashing
        entityCode.ShouldReferenceType("IDynamoDbEntity");
        entityCode.ShouldContainMethod("ToDynamoDb");
        entityCode.ShouldContainMethod("FromDynamoDb");
    }

    [Fact]
    public void SourceGenerator_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""unicode-table"")]
    public partial class UnicodeEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""名前"")]
        public string 名前 { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""descripción"")]
        public string Descripción { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""价格"")]
        public decimal 价格 { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "UnicodeEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific checks for Unicode character handling
        fieldsCode.Should().Contain("public const string 名前 = \"名前\";",
            "should handle Japanese Unicode characters in property and attribute names");
        fieldsCode.Should().Contain("public const string Descripción = \"descripción\";",
            "should handle Spanish Unicode characters in property and attribute names");
        fieldsCode.Should().Contain("public const string 价格 = \"价格\";",
            "should handle Chinese Unicode characters in property and attribute names");
    }

    [Fact]
    public void SourceGenerator_WithComplexKeyFormats_ParsesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""complex-key-formats-table"")]
    public partial class ComplexKeyFormatsEntity
    {
        [PartitionKey(Prefix = ""tenant"", Separator = ""#"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey(Prefix = ""item"", Separator = ""#"")]
        [DynamoDbAttribute(""sk"")]
        public string ItemId { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""ComplexGSI"", IsPartitionKey = true, KeyFormat = ""status#{0}#region#{1}"")]
        [DynamoDbAttribute(""gsi_pk"")]
        public string Status { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""region"")]
        public string Region { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate warning for reserved word "region"
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warning for "region"
        result.GeneratedSources.Should().HaveCount(3);

        var keysCode = GetGeneratedSource(result, "ComplexKeyFormatsEntityKeys.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(keysCode, source);
        
        keysCode.ShouldContainMethod("Pk");
        keysCode.ShouldContainMethod("Sk");
        
        // Keep DynamoDB-specific checks for key format strings
        keysCode.Should().Contain("var keyValue = \"tenant#\" + tenantId;",
            "should generate correct partition key format with prefix and separator");
        keysCode.Should().Contain("var keyValue = \"item#\" + itemId;",
            "should generate correct sort key format with prefix and separator");
    }

    [Fact]
    public void SourceGenerator_WithEmptyAttributeNames_HandlesGracefully()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""empty-attributes-table"")]
    public partial class EmptyAttributesEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute("""")]
        public string EmptyAttribute { get; set; } = string.Empty;
        
        public string NoAttribute { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "EmptyAttributesEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific checks for attribute name handling
        fieldsCode.Should().Contain("public const string Id = \"pk\";",
            "should generate field constant for properties with valid attribute names");
        fieldsCode.Should().NotContain("public const string EmptyAttribute",
            "should not generate field constant for properties with empty attribute names");
        fieldsCode.Should().NotContain("public const string NoAttribute",
            "should not generate field constant for properties without DynamoDbAttribute");
    }

    [Fact]
    public void SourceGenerator_WithDuplicateAttributeNames_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""duplicate-attributes-table"")]
    public partial class DuplicateAttributesEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""same_name"")]
        public string FirstProperty { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""same_name"")]
        public string SecondProperty { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate code without any diagnostics for basic entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);

        var fieldsCode = GetGeneratedSource(result, "DuplicateAttributesEntityFields.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        
        // Keep DynamoDB-specific checks for duplicate attribute name handling
        fieldsCode.Should().Contain("public const string FirstProperty = \"same_name\";",
            "should generate field constant for first property with duplicate attribute name");
        fieldsCode.Should().Contain("public const string SecondProperty = \"same_name\";",
            "should generate field constant for second property with duplicate attribute name");
    }

    [Fact]
    public void SourceGenerator_WithVeryComplexRelationshipPatterns_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""complex-patterns-table"")]
    public partial class ComplexPatternsEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [RelatedEntity(""audit#*#*"")]
        public List<AuditEntry>? NestedWildcardAudit { get; set; }
        
        [RelatedEntity(""prefix#middle#*#suffix"")]
        public List<ComplexPatternEntry>? ComplexPattern { get; set; }
        
        [RelatedEntity(""*#*#*"")]
        public List<object>? VeryBroadPattern { get; set; }
        
        [RelatedEntity(""exact#match#no#wildcards"")]
        public ExactMatchEntry? ExactMatch { get; set; }
    }
    
    public class AuditEntry
    {
        public string Action { get; set; } = string.Empty;
    }
    
    public class ComplexPatternEntry
    {
        public string Data { get; set; } = string.Empty;
    }
    
    public class ExactMatchEntry
    {
        public string Value { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // May generate warnings about overly broad patterns and performance issues
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB023"); // Performance warnings for collections
        result.GeneratedSources.Should().HaveCount(3);

        var entityCode = GetGeneratedSource(result, "ComplexPatternsEntity.g.cs");
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        entityCode.ShouldReferenceType("IDynamoDbEntity");
        entityCode.ShouldContainMethod("ToDynamoDb");
        entityCode.ShouldContainMethod("FromDynamoDb");

        // Verify related entity metadata is captured (actual mapping happens at runtime in ToCompositeEntityAsync)
        entityCode.Should().Contain("Relationships = new RelationshipMetadata[]",
            "should generate relationship metadata array");
        entityCode.Should().Contain("PropertyName = \"NestedWildcardAudit\"",
            "should include nested wildcard relationship in metadata");
        entityCode.Should().Contain("PropertyName = \"ComplexPattern\"",
            "should include complex pattern relationship in metadata");
        entityCode.Should().Contain("PropertyName = \"VeryBroadPattern\"",
            "should include very broad pattern relationship in metadata");
        entityCode.Should().Contain("PropertyName = \"ExactMatch\"",
            "should include exact match relationship in metadata");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        // Include attribute definitions in the compilation
        var attributeSource = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public string? EntityDiscriminator { get; set; }
        public DynamoDbTableAttribute(string tableName) => TableName = tableName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string AttributeName { get; }
        public DynamoDbAttributeAttribute(string attributeName) => AttributeName = attributeName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class SortKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string? Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class GlobalSecondaryIndexAttribute : Attribute
    {
        public string IndexName { get; }
        public bool IsPartitionKey { get; set; }
        public bool IsSortKey { get; set; }
        public string? KeyFormat { get; set; }
        public GlobalSecondaryIndexAttribute(string indexName) => IndexName = indexName;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RelatedEntityAttribute : Attribute
    {
        public string SortKeyPattern { get; }
        public Type? EntityType { get; set; }
        public RelatedEntityAttribute(string sortKeyPattern) => SortKeyPattern = sortKeyPattern;
    }
}";

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText(attributeSource)
            },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                // Add AWS SDK references for generated code
                MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
                // Add main library reference for IDynamoDbEntity and other types
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location),
                // Add System.Linq reference
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                // Add System.IO reference  
                MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
                // Add netstandard reference for Attribute, Enum, and other base types
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")),
                // Add System.Collections reference for Dictionary<,> and List<>
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Collections.dll")),
                // Add System.Linq.Expressions reference
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Linq.Expressions.dll"))
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }
}