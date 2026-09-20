using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Tests that [Extracted] attributes are completely excluded from named-placeholder normalization.
/// The sourceProperty argument is stored verbatim — no scanning for {PropertyName} tokens.
/// 
/// Validates: Requirements 8.1, 8.2, 8.3
/// </summary>
[Trait("Category", "Unit")]
public class ExtractedAttributeExclusionTests
{
    /// <summary>
    /// Requirement 8.2: [Extracted("Pk", 0)] stores "Pk" verbatim without scanning for named placeholders.
    /// No FDDB091-096 diagnostics should be emitted.
    /// </summary>
    [Fact]
    public void ExtractedWithPlainPropertyName_ShouldCompileWithNoDiagnostics()
    {
        // Arrange — standard [Extracted("Pk", 0)] referencing a computed key property
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no named-placeholder diagnostics emitted
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB091");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB092");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB093");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB094");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB095");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB096");
    }

    /// <summary>
    /// Requirement 8.3: [Extracted("{Pk}", 0)] stores "{Pk}" as a literal string.
    /// The braces must NOT trigger named-placeholder normalization or any FDDB diagnostics.
    /// </summary>
    [Fact]
    public void ExtractedWithBracedPropertyName_ShouldCompileWithNoDiagnostics()
    {
        // Arrange — sourceProperty argument coincidentally contains brace characters
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""{Pk}"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no named-placeholder diagnostics emitted despite braces in sourceProperty
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB091",
            "[Extracted] sourceProperty with braces must not trigger ambiguous placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB092",
            "[Extracted] sourceProperty with braces must not trigger unresolved placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB093",
            "[Extracted] sourceProperty with braces must not trigger mixed placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB094",
            "[Extracted] sourceProperty with braces must not trigger malformed placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB095",
            "[Extracted] sourceProperty with braces must not trigger empty placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB096",
            "[Extracted] sourceProperty with braces must not trigger ambiguous name/index diagnostic");
    }

    /// <summary>
    /// Requirement 8.3: [Extracted("{Pk:D4}", 0)] stores "{Pk:D4}" as a literal string.
    /// Brace-with-format-specifier syntax must NOT trigger normalization or diagnostics.
    /// </summary>
    [Fact]
    public void ExtractedWithBracedPropertyNameAndFormatSpecifier_ShouldCompileWithNoDiagnostics()
    {
        // Arrange — sourceProperty contains both braces and a format specifier
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""{Pk:D4}"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no named-placeholder diagnostics emitted despite format-specifier-like braces
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB091",
            "[Extracted] sourceProperty with format specifier braces must not trigger ambiguous placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB092",
            "[Extracted] sourceProperty with format specifier braces must not trigger unresolved placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB093",
            "[Extracted] sourceProperty with format specifier braces must not trigger mixed placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB094",
            "[Extracted] sourceProperty with format specifier braces must not trigger malformed placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB095",
            "[Extracted] sourceProperty with format specifier braces must not trigger empty placeholder diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB096",
            "[Extracted] sourceProperty with format specifier braces must not trigger ambiguous name/index diagnostic");
    }

    /// <summary>
    /// Combined scenario: entity uses named-placeholder [Computed] syntax alongside [Extracted]
    /// attributes with brace characters. The [Computed] normalization must not bleed into [Extracted].
    /// Validates that normalization is scoped only to [Computed] / [RelatedEntity] attributes.
    /// </summary>
    [Fact]
    public void ExtractedWithBraces_AlongsideNamedPlaceholderComputed_ShouldCompileSuccessfully()
    {
        // Arrange — [Computed] uses named placeholders, [Extracted] has brace characters
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Year}#MONTH#{Month}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""{Pk}"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""{Pk:D4}"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no named-placeholder diagnostics from the [Extracted] attributes
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB091");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB092");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB093");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB094");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB095");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB096");

        // The entity should generate code successfully (at least one generated source)
        result.GeneratedSources.Should().NotBeEmpty(
            "Entity with named-placeholder [Computed] and brace-containing [Extracted] should generate code");
    }

    #region Helper Methods

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

    #endregion
}
