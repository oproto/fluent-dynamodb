using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

public class EntityAnalyzerTests
{
    [Fact]
    public void AnalyzeEntity_WithBasicEntity_ExtractsCorrectInformation()
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
        result!.ClassName.Should().Be("TestEntity");
        result.Namespace.Should().Be("TestNamespace");
        result.TableName.Should().Be("test-table");
        result.Properties.Should().HaveCount(2);
        
        var partitionKeyProperty = result.PartitionKeyProperty;
        partitionKeyProperty.Should().NotBeNull();
        partitionKeyProperty!.PropertyName.Should().Be("Id");
        partitionKeyProperty.AttributeName.Should().Be("pk");
        partitionKeyProperty.IsPartitionKey.Should().BeTrue();
        
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithMissingPartitionKey_ReportsDiagnostic()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().BeNull();
        analyzer.Diagnostics.Should().HaveCount(1);
        analyzer.Diagnostics[0].Id.Should().Be("DYNDB001");
        analyzer.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeEntity_WithMultiplePartitionKeys_ReportsDiagnostic()
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
        [DynamoDbAttribute(""pk1"")]
        public string Id1 { get; set; } = string.Empty;
        
        [PartitionKey]
        [DynamoDbAttribute(""pk2"")]
        public string Id2 { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().BeNull();
        analyzer.Diagnostics.Should().HaveCount(1);
        analyzer.Diagnostics[0].Id.Should().Be("DYNDB002");
        analyzer.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeEntity_WithNonPartialClass_ReportsDiagnostic()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().BeNull();
        analyzer.Diagnostics.Should().HaveCount(1);
        analyzer.Diagnostics[0].Id.Should().Be("DYNDB010");
        analyzer.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeEntity_WithGsiAttributes_ExtractsIndexInformation()
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
        
        [GlobalSecondaryIndex(""GSI1"", IsPartitionKey = true)]
        [DynamoDbAttribute(""gsi1pk"")]
        public string GsiPartitionKey { get; set; } = string.Empty;
        
        [GlobalSecondaryIndex(""GSI1"", IsSortKey = true)]
        [DynamoDbAttribute(""gsi1sk"")]
        public string GsiSortKey { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Indexes.Should().HaveCount(1);
        
        var gsi = result.Indexes[0];
        gsi.IndexName.Should().Be("GSI1");
        gsi.PartitionKeyProperty.Should().Be("GsiPartitionKey");
        gsi.SortKeyProperty.Should().Be("GsiSortKey");
        gsi.HasSortKey.Should().BeTrue();
        
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithRelatedEntities_ExtractsRelationshipInformation()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using System.Collections.Generic;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [RelatedEntity(""audit#*"")]
        public List<AuditEntry>? AuditEntries { get; set; }
        
        [RelatedEntity(""summary"")]
        public Summary? Summary { get; set; }
    }
    
    public class AuditEntry { }
    public class Summary { }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.Relationships.Should().HaveCount(2);
        
        var auditRelationship = result.Relationships.First(r => r.PropertyName == "AuditEntries");
        auditRelationship.SortKeyPattern.Should().Be("audit#*");
        auditRelationship.IsCollection.Should().BeTrue();
        auditRelationship.IsWildcardPattern.Should().BeTrue();
        
        var summaryRelationship = result.Relationships.First(r => r.PropertyName == "Summary");
        summaryRelationship.SortKeyPattern.Should().Be("summary");
        summaryRelationship.IsCollection.Should().BeFalse();
        summaryRelationship.IsWildcardPattern.Should().BeFalse();
        
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithKeyFormatting_ExtractsKeyFormatInformation()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey(Prefix = ""tenant"", Separator = ""#"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId { get; set; } = string.Empty;
        
        [SortKey(Prefix = ""item"", Separator = ""#"")]
        [DynamoDbAttribute(""sk"")]
        public string ItemId { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        
        var partitionKey = result!.PartitionKeyProperty;
        partitionKey.Should().NotBeNull();
        partitionKey!.KeyFormat.Should().NotBeNull();
        partitionKey.KeyFormat!.Prefix.Should().Be("tenant");
        partitionKey.KeyFormat.Separator.Should().Be("#");
        
        var sortKey = result.SortKeyProperty;
        sortKey.Should().NotBeNull();
        sortKey!.KeyFormat.Should().NotBeNull();
        sortKey.KeyFormat!.Prefix.Should().Be("item");
        sortKey.KeyFormat.Separator.Should().Be("#");
        
        analyzer.Diagnostics.Should().BeEmpty();
    }

    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] 
            { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}