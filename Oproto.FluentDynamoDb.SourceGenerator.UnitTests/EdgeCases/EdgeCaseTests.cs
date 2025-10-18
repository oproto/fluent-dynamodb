using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.EdgeCases;

/// <summary>
/// Tests for edge cases and unusual scenarios that the source generator should handle gracefully.
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var entityCode = GetGeneratedSource(result, "EmptyEntity.g.cs");
        entityCode.Should().Contain("public partial class EmptyEntity : IDynamoDbEntity");
        entityCode.Should().Contain("public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "SpecialCharsEntityFields.g.cs");
        fieldsCode.Should().Contain("public const string FieldWithDashes = \"field-with-dashes\";");
        fieldsCode.Should().Contain("public const string FieldWithUnderscores = \"field_with_underscores\";");
        fieldsCode.Should().Contain("public const string FieldWithDots = \"field.with.dots\";");
        fieldsCode.Should().Contain("public const string FieldWithNumbers = \"field123\";");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "ReservedKeywordsEntityFields.g.cs");
        fieldsCode.Should().Contain("public const string @class = \"class\";");
        fieldsCode.Should().Contain("public const string @namespace = \"namespace\";");
        fieldsCode.Should().Contain("public const string @public = \"public\";");
        fieldsCode.Should().Contain("public const string @COUNT = \"COUNT\";");
        fieldsCode.Should().Contain("public const string @SIZE = \"SIZE\";");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "LongNamesEntityFields.g.cs");
        fieldsCode.Should().Contain($"public const string {longPropertyName} = \"{longAttributeName}\";");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var entityCode = GetGeneratedSource(result, "NestedNamespaceEntity.g.cs");
        entityCode.Should().Contain("namespace Very.Deeply.Nested.Namespace.Structure");
        entityCode.Should().Contain("public partial class NestedNamespaceEntity : IDynamoDbEntity");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var entityCode = GetGeneratedSource(result, "GenericConstraintsEntity.g.cs");
        entityCode.Should().Contain("System.Text.Json.JsonSerializer.Serialize(typedEntity.GenericList)");
        entityCode.Should().Contain("System.Text.Json.JsonSerializer.Deserialize<List<string>>");
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
        // Should generate code but may have warnings about circular references
        result.GeneratedSources.Should().HaveCount(3);
        
        var entityCode = GetGeneratedSource(result, "CircularRefEntity.g.cs");
        entityCode.Should().Contain("public partial class CircularRefEntity : IDynamoDbEntity");
        entityCode.Should().Contain("Related entities: 2 relationship(s) defined.");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "UnicodeEntityFields.g.cs");
        fieldsCode.Should().Contain("public const string 名前 = \"名前\";");
        fieldsCode.Should().Contain("public const string Descripción = \"descripción\";");
        fieldsCode.Should().Contain("public const string 价格 = \"价格\";");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var keysCode = GetGeneratedSource(result, "ComplexKeyFormatsEntityKeys.g.cs");
        keysCode.Should().Contain("public static string Pk(string tenantId)");
        keysCode.Should().Contain("return \"tenant#\" + tenantId;");
        keysCode.Should().Contain("public static string Sk(string itemId)");
        keysCode.Should().Contain("return \"item#\" + itemId;");
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
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "EmptyAttributesEntityFields.g.cs");
        fieldsCode.Should().Contain("public const string Id = \"pk\";");
        fieldsCode.Should().NotContain("EmptyAttribute");
        fieldsCode.Should().NotContain("NoAttribute");
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
        // Should generate code but may have warnings about duplicate attribute names
        result.GeneratedSources.Should().HaveCount(3);
        
        var fieldsCode = GetGeneratedSource(result, "DuplicateAttributesEntityFields.g.cs");
        fieldsCode.Should().Contain("public const string FirstProperty = \"same_name\";");
        fieldsCode.Should().Contain("public const string SecondProperty = \"same_name\";");
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
        // May generate warnings about overly broad patterns
        result.GeneratedSources.Should().HaveCount(3);
        
        var entityCode = GetGeneratedSource(result, "ComplexPatternsEntity.g.cs");
        entityCode.Should().Contain("Related entities: 4 relationship(s) defined.");
        entityCode.Should().Contain("if (sortKey.StartsWith(\"audit#\"))");
        entityCode.Should().Contain("if (sortKey.StartsWith(\"prefix#middle#\"))");
        entityCode.Should().Contain("if (sortKey == \"exact#match#no#wildcards\"");
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
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location)
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