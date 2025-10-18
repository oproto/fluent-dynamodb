using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

public class DynamoDbSourceGeneratorTests
{
    [Fact]
    public void Generator_WithBasicEntity_ProducesCode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
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
        // Should generate warnings for scalability concerns but still produce code
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB027"); // Scalability warning
        result.GeneratedSources.Should().HaveCount(3); // Entity + Fields + Keys
        
        // Check entity implementation
        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntity.g.cs")).SourceText.ToString();
        entityCode.Should().Contain("public partial class TestEntity");
        entityCode.Should().Contain("namespace TestNamespace");
        
        // Check fields class
        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        fieldsCode.Should().Contain("public static partial class TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";");
        fieldsCode.Should().Contain("public const string Name = \"name\";");
        
        // Check keys class
        var keysCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityKeys.g.cs")).SourceText.ToString();
        keysCode.Should().Contain("public static partial class TestEntityKeys");
        keysCode.Should().Contain("public static string Pk(string id)");
    }

    [Fact]
    public void Generator_WithoutDynamoDbTableAttribute_ProducesNoCode()
    {
        // Arrange
        var source = @"
namespace TestNamespace
{
    public partial class RegularClass
    {
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void Generator_WithGsiEntity_GeneratesFieldsWithGsiClasses()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""TestGSI"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi_pk"")]
        public string GsiKey { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""TestGSI"", IsSortKey = true)]
        [DynamoDbAttribute(""gsi_sk"")]
        public string GsiSort { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        // Should generate warnings for scalability and reserved word usage
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB027"); // Scalability warning
        result.GeneratedSources.Should().HaveCount(3); // Entity + Fields + Keys
        
        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        fieldsCode.Should().Contain("public static partial class TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";");
        fieldsCode.Should().Contain("public const string GsiKey = \"gsi_pk\";");
        fieldsCode.Should().Contain("public const string GsiSort = \"gsi_sk\";");
        fieldsCode.Should().Contain("public static partial class TestGSIFields");
        fieldsCode.Should().Contain("public const string PartitionKey = \"gsi_pk\";");
        fieldsCode.Should().Contain("public const string SortKey = \"gsi_sk\";");
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

    [AttributeUsage(AttributeTargets.Property)]
    public class QueryableAttribute : Attribute
    {
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();
        public string[]? AvailableInIndexes { get; set; }
    }
}";

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { 
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText(attributeSource)
            },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
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
}

public class GeneratorTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required GeneratedSource[] GeneratedSources { get; set; }
}

public class GeneratedSource
{
    public GeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}