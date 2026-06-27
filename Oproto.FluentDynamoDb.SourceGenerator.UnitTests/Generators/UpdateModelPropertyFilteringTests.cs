using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for source generator filtering edge cases in update model property generation.
/// Tests cover PK-only exclusion, PK+SK exclusion, extracted-from-key exclusion,
/// and diagnostic emission for invalid extracted property references.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Generator", "UpdateExpressions")]
[Trait("Feature", "update-model-computed-field-redesign")]
public class UpdateModelPropertyFilteringTests
{
    /// <summary>
    /// Entity with PK only — update model excludes PK but includes all other properties.
    /// Validates: Requirement 1.4
    /// </summary>
    [Fact]
    public void Generator_WithPkOnly_ExcludesPkFromUpdateModel()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class PkOnlyEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [DynamoDbAttribute(""age"")]
        public int Age { get; set; }

        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("PkOnlyEntityUpdateModel.g.cs"));

        updateModelFile.Should().NotBeNull("should generate UpdateModel class");

        var code = updateModelFile!.SourceText.ToString();

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(code, source);

        // PK should be excluded (Req 1.4)
        code.Should().NotContain("string? Id", "should exclude partition key property from UpdateModel");

        // All other non-key properties should be included
        code.Should().Contain("string? Name", "should include non-key string property");
        code.Should().Contain("int? Age", "should include non-key int property");
        code.Should().Contain("string? Email", "should include non-key string property");
    }

    /// <summary>
    /// Entity with PK+SK — update model excludes both key properties.
    /// Validates: Requirement 1.3
    /// </summary>
    [Fact]
    public void Generator_WithPkAndSk_ExcludesBothFromUpdateModel()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class PkSkEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("PkSkEntityUpdateModel.g.cs"));

        updateModelFile.Should().NotBeNull("should generate UpdateModel class");

        var code = updateModelFile!.SourceText.ToString();

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(code, source);

        // Both PK and SK should be excluded (Req 1.3)
        code.Should().NotContain("string? Pk", "should exclude partition key from UpdateModel");
        code.Should().NotContain("string? Sk", "should exclude sort key from UpdateModel");

        // All other non-key properties should be included
        code.Should().Contain("string? Status", "should include non-key string property");
        code.Should().Contain("int? Count", "should include non-key int property");
        code.Should().Contain("decimal? Amount", "should include non-key decimal property");
    }

    /// <summary>
    /// Entity with [Extracted("Pk", 0)] — extracted property derived from PK is excluded.
    /// Validates: Requirement 2.1
    /// </summary>
    [Fact]
    public void Generator_WithExtractedFromPk_ExcludesExtractedProperty()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class ExtractedFromPkEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""UserId"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        [Extracted(""Pk"", 0)]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        [Extracted(""Pk"", 1)]
        public string UserId { get; set; } = string.Empty;

        [DynamoDbAttribute(""displayName"")]
        public string DisplayName { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("ExtractedFromPkEntityUpdateModel.g.cs"));

        updateModelFile.Should().NotBeNull("should generate UpdateModel class");

        var code = updateModelFile!.SourceText.ToString();

        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(code, source);

        // PK and SK should be excluded
        code.Should().NotContain("string? Pk", "should exclude partition key from UpdateModel");
        code.Should().NotContain("string? Sk", "should exclude sort key from UpdateModel");

        // Extracted properties of PK should be excluded (Req 2.1)
        code.Should().NotContain("string? TenantId", "should exclude extracted property of PK");
        code.Should().NotContain("string? UserId", "should exclude extracted property of PK");

        // Non-key, non-extracted properties should be included
        code.Should().Contain("string? DisplayName", "should include regular non-key property");
    }

    /// <summary>
    /// Entity with [Extracted("NonExistentProp", 0)] — diagnostic is emitted and property is excluded.
    /// Validates: Requirement 2.4
    /// </summary>
    [Fact]
    public void Generator_WithExtractedFromNonExistentProperty_EmitsDiagnostic()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class InvalidExtractedEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""value"")]
        [Extracted(""NonExistentProp"", 0)]
        public string Value { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - diagnostic should be emitted for invalid extracted source
        // The EntityAnalyzer emits DYNDB032 for extracted properties referencing non-existent source
        var invalidExtractedDiagnostics = result.Diagnostics
            .Where(d => d.Id == "DYNDB032")
            .ToList();

        invalidExtractedDiagnostics.Should().NotBeEmpty(
            "should emit DYNDB032 diagnostic for [Extracted] referencing non-existent property");

        // The invalid extracted property should be excluded from UpdateModel
        var updateModelFile = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains("InvalidExtractedEntityUpdateModel.g.cs"));

        updateModelFile.Should().NotBeNull("should still generate UpdateModel class");

        var code = updateModelFile!.SourceText.ToString();

        // The invalid extracted property should not appear in the update model
        code.Should().NotContain("string? Value", "should exclude extracted property with invalid source");

        // Valid non-key properties should still be included
        code.Should().Contain("string? Name", "should include valid non-key property");

        // PK should be excluded as well
        code.Should().NotContain("string? Pk", "should exclude partition key");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
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
