using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for unsigned integer type support in the source generator.
/// Feature: unsigned-integer-types
/// </summary>
public class UnsignedIntegerTypeTests
{
    [Theory]
    [InlineData("ulong", "Version")]
    [InlineData("uint", "Counter")]
    [InlineData("ushort", "SmallValue")]
    [InlineData("byte", "ByteValue")]
    [InlineData("sbyte", "SignedByteValue")]
    [InlineData("short", "ShortValue")]
    public void AnalyzeEntity_WithUnsignedIntegerProperty_AcceptsWithoutError(string typeName, string propertyName)
    {
        // Arrange
        var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""{propertyName.ToLower()}"")]
        public {typeName} {propertyName} {{ get; set; }}
    }}
}}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == propertyName);
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Theory]
    [InlineData("ulong?", "NullableVersion")]
    [InlineData("uint?", "NullableCounter")]
    [InlineData("ushort?", "NullableSmallValue")]
    [InlineData("byte?", "NullableByteValue")]
    [InlineData("sbyte?", "NullableSignedByteValue")]
    [InlineData("short?", "NullableShortValue")]
    public void AnalyzeEntity_WithNullableUnsignedIntegerProperty_AcceptsWithoutError(string typeName, string propertyName)
    {
        // Arrange
        var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""{propertyName.ToLower()}"")]
        public {typeName} {propertyName} {{ get; set; }}
    }}
}}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == propertyName);
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    [Theory]
    [InlineData("List<ulong>", "VersionHistory")]
    [InlineData("List<uint>", "Counters")]
    [InlineData("List<byte>", "ByteValues")]
    [InlineData("HashSet<ushort>", "UniqueSmallValues")]
    public void AnalyzeEntity_WithUnsignedIntegerCollectionProperty_AcceptsWithoutError(string typeName, string propertyName)
    {
        // Arrange
        var source = $@"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id {{ get; set; }} = string.Empty;
        
        [DynamoDbAttribute(""{propertyName.ToLower()}"")]
        public {typeName} {propertyName} {{ get; set; }} = new();
    }}
}}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Properties.Should().Contain(p => p.PropertyName == propertyName);
        analyzer.Diagnostics.Should().NotContain(d => d.Id == "DYNDB009");
    }

    private static (ClassDeclarationSyntax, SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = DynamicCompilationHelper.GetStandardReferences()
            .Concat(DynamicCompilationHelper.GetFluentDynamoDbReferences());

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        return (classDecl, semanticModel);
    }
}
