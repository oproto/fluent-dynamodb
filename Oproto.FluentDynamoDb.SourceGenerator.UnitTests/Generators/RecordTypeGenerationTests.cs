using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for record type support in the source generator.
/// Verifies that record declarations are properly handled and generate valid code.
/// _Requirements: 2.1_
/// </summary>
[Trait("Category", "Unit")]
public class RecordTypeGenerationTests
{
    [Fact]
    public void Generator_WithBasicRecord_ProducesCode()
    {
        // Arrange - using get/set properties for compatibility with current generator
        // Note: init-only properties require object initializer syntax in FromDynamoDb
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public partial record TestRecordEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""value"")]
        public int Value { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(5); // Entity, UpdateExpressions, UpdateModel, UpdateBuilder, Table

        // Check entity implementation
        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("TestRecordEntity.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("TestRecordEntity");
        entityCode.Should().Contain("namespace TestNamespace", "should generate code in the correct namespace");

        // Check nested fields class
        entityCode.ShouldContainClass("Fields");
        entityCode.Should().Contain("public const string Id = \"pk\";", "should map Id property to pk attribute");
        entityCode.Should().Contain("public const string Value = \"value\";", "should map Value property to value attribute");

        // Check nested keys class (bare keys have no Pk/Sk methods since there is no prefix or computed key)
        entityCode.ShouldContainClass("Keys");
    }

    [Fact]
    public void Generator_WithRecordWithSortKey_ProducesCode()
    {
        // Arrange - record with get/set properties (not init-only) for full compatibility
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public partial record TestRecordWithSortKey
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""data"")]
        public string Data { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(5);

        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("TestRecordWithSortKey.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        
        // Check keys class exists (bare keys have no Pk/Sk methods since there is no prefix or computed key)
        entityCode.ShouldContainClass("Keys");
    }

    [Fact]
    public void Generator_WithRecordClass_ProducesCode()
    {
        // Arrange - explicit "record class" syntax with get/set properties
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public partial record class ExplicitRecordClass
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
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(5);

        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("ExplicitRecordClass.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("ExplicitRecordClass");
    }

    [Fact]
    public void Generator_WithRecordWithMutableProperties_ProducesCode()
    {
        // Arrange - record with mutable properties (get/set) for full compatibility
        // Note: Records with init-only properties require object initializer syntax in FromDynamoDb
        // which is a known limitation. Using get/set properties works with the current generator.
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public partial record MutableRecord
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""description"")]
        public string? Description { get; set; }
        
        [DynamoDbAttribute(""is_active"")]
        public bool IsActive { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(5);

        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("MutableRecord.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("MutableRecord");
        entityCode.ShouldContainClass("Fields");
    }

    [Fact]
    public void Generator_WithRecordWithDateTimeOffset_ProducesCode()
    {
        // Arrange - record with DateTimeOffset property (using get/set for compatibility)
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public partial record RecordWithDateTime
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""created_at"")]
        public DateTimeOffset CreatedAt { get; set; }
        
        [DynamoDbAttribute(""updated_at"")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(5);

        var entityCode = result.GeneratedSources.First(s => s.FileName.Contains("RecordWithDateTime.g.cs")).SourceText.ToString();
        CompilationVerifier.AssertGeneratedCodeCompiles(entityCode, source);
        entityCode.ShouldContainClass("RecordWithDateTime");
    }

    [Fact]
    public void Generator_WithNonPartialRecord_EmitsDiagnostic()
    {
        // Arrange - non-partial record should emit diagnostic
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-records"")]
    public record NonPartialRecord
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should emit DYNDB010 diagnostic for non-partial type
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB010");
    }

    [Fact]
    public void Generator_WithMultipleRecordsInSameTable_ProducesCode()
    {
        // Arrange - multiple records sharing the same table
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""shared-table"", IsDefault = true)]
    public partial record Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; init; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; init; } = string.Empty;
    }

    [DynamoDbTable(""shared-table"")]
    public partial record OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string OrderId { get; init; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string LineId { get; init; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        result.GeneratedSources.Should().HaveCount(9); // 2 entities × 4 files each + 1 shared Table

        // Verify both records generated code
        result.GeneratedSources.Should().Contain(s => s.FileName.Contains("Order.g.cs"));
        result.GeneratedSources.Should().Contain(s => s.FileName.Contains("OrderLine.g.cs"));
        result.GeneratedSources.Should().Contain(s => s.FileName.Contains("SharedTableTable.g.cs"));
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
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
