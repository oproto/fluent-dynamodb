using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for Local Secondary Index (LSI) attribute detection in EntityAnalyzer.
/// Validates Requirements 4.1, 7.2 from the schema-validation spec.
/// </summary>
[Trait("Category", "Unit")]
public class LsiAttributeDetectionTests
{
    [Fact]
    public void AnalyzeEntity_WithLsiAttribute_ExtractsLsiInformation()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string CreatedAt { get; set; } = string.Empty;
        
        [LocalSecondaryIndex(""StatusIndex"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Indexes.Should().HaveCount(1);

        var lsi = result.Indexes[0];
        lsi.IndexName.Should().Be("StatusIndex");
        lsi.IndexType.Should().Be(IndexType.LocalSecondaryIndex);
        lsi.PartitionKeyProperty.Should().Be("TenantId", "LSI should inherit partition key from base table");
        lsi.SortKeyProperty.Should().Be("Status", "LSI sort key should be the property with [LocalSecondaryIndex]");
    }

    [Fact]
    public void AnalyzeEntity_WithMultipleLsiAttributes_ExtractsAllLsis()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string CreatedAt { get; set; } = string.Empty;
        
        [LocalSecondaryIndex(""StatusIndex"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
        
        [LocalSecondaryIndex(""PriorityIndex"")]
        [DynamoDbAttribute(""priority"")]
        public int Priority { get; set; }
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Indexes.Should().HaveCount(2);

        var statusLsi = result.Indexes.FirstOrDefault(i => i.IndexName == "StatusIndex");
        statusLsi.Should().NotBeNull();
        statusLsi!.IndexType.Should().Be(IndexType.LocalSecondaryIndex);
        statusLsi.SortKeyProperty.Should().Be("Status");

        var priorityLsi = result.Indexes.FirstOrDefault(i => i.IndexName == "PriorityIndex");
        priorityLsi.Should().NotBeNull();
        priorityLsi!.IndexType.Should().Be(IndexType.LocalSecondaryIndex);
        priorityLsi.SortKeyProperty.Should().Be("Priority");
    }

    [Fact]
    public void AnalyzeEntity_WithMixedGsiAndLsi_ExtractsBothIndexTypes()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string CreatedAt { get; set; } = string.Empty;
        
        [LocalSecondaryIndex(""StatusLSI"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""EmailGSI"", IsPartitionKey = true)]
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
        result!.Indexes.Should().HaveCount(2);

        var lsi = result.Indexes.FirstOrDefault(i => i.IndexName == "StatusLSI");
        lsi.Should().NotBeNull();
        lsi!.IndexType.Should().Be(IndexType.LocalSecondaryIndex);

        var gsi = result.Indexes.FirstOrDefault(i => i.IndexName == "EmailGSI");
        gsi.Should().NotBeNull();
        gsi!.IndexType.Should().Be(IndexType.GlobalSecondaryIndex);
    }

    [Fact]
    public void AnalyzeEntity_WithLsiOnProperty_SetsIsPartOfLsi()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string CreatedAt { get; set; } = string.Empty;
        
        [LocalSecondaryIndex(""StatusIndex"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        
        var statusProperty = result!.Properties.FirstOrDefault(p => p.PropertyName == "Status");
        statusProperty.Should().NotBeNull();
        statusProperty!.IsPartOfLsi.Should().BeTrue("property has [LocalSecondaryIndex] attribute");
        statusProperty.LocalSecondaryIndexes.Should().HaveCount(1);
        statusProperty.LocalSecondaryIndexes[0].IndexName.Should().Be("StatusIndex");
    }

    [Fact]
    public void AnalyzeEntity_WithoutLsi_HasNoLsiIndexes()
    {
        // Arrange
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
        
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Indexes.Should().BeEmpty();
        
        var nameProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Name");
        nameProperty.Should().NotBeNull();
        nameProperty!.IsPartOfLsi.Should().BeFalse();
        nameProperty.LocalSecondaryIndexes.Should().BeEmpty();
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
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}
