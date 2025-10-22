// MIGRATION STATUS: Migrated to use CompilationVerifier and SemanticAssertions
// - Added compilation verification to all tests
// - Replaced structural string checks with semantic assertions
// - Preserved DynamoDB-specific value checks with descriptive messages

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

[Trait("Category", "Unit")]
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
        // Should generate code with DYNDB021 warning for reserved word "name"
        result.Diagnostics.Should().NotBeEmpty();
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB021"); // Reserved word warning for "name"
        result.GeneratedSources.Should().HaveCount(3); // Entity + Fields + Keys

        // Check entity implementation
        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntity.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("TestEntity");
        entityCode.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");

        // Check fields class
        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        fieldsCode.ShouldContainClass("TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        fieldsCode.Should().Contain("public const string Name = \"name\";", "should map Name property to name attribute");

        // Check keys class
        var keysCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityKeys.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(keysCode, source);
        keysCode.ShouldContainClass("TestEntityKeys");
        keysCode.ShouldContainMethod("Pk");
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
        // Should generate code without any diagnostics for GSI entity
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().HaveCount(3); // Entity + Fields + Keys

        var fieldsCode = result.GeneratedSources.First(s => s.FileName.Contains("TestEntityFields.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(fieldsCode, source);
        fieldsCode.ShouldContainClass("TestEntityFields");
        fieldsCode.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        fieldsCode.Should().Contain("public const string GsiKey = \"gsi_pk\";", "should map GsiKey property to gsi_pk attribute");
        fieldsCode.Should().Contain("public const string GsiSort = \"gsi_sk\";", "should map GsiSort property to gsi_sk attribute");
        fieldsCode.ShouldContainClass("TestGSIFields");
        fieldsCode.Should().Contain("public const string PartitionKey = \"gsi_pk\";", "should define GSI partition key constant");
        fieldsCode.Should().Contain("public const string SortKey = \"gsi_sk\";", "should define GSI sort key constant");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            new[] {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location),
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