using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for EntityAnalyzer format specifier handling in DeriveDiscriminatorPattern and
/// ValidateComputedKeyFormat methods.
/// 
/// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 7.1, 7.2, 7.3, 7.4, 7.5
/// </summary>
[Trait("Category", "Unit")]
public class EntityAnalyzer_FormatSpecifierTests
{
    #region DeriveDiscriminatorPattern Tests

    [Fact]
    public void DeriveDiscriminatorPattern_WithDateFormatSpecifier_ReplacesPlaceholderWithWildcard()
    {
        // Arrange - {0:yyyy-MM-dd} should be replaced entirely with *
        var format = "{0:yyyy-MM-dd}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert - starts with * so should return null
        result.Should().BeNull(
            "pattern '*#*' starts with '*' so discrimination by prefix is not possible");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithIntegerFormatSpecifier_ReplacesPlaceholderWithWildcard()
    {
        // Arrange - {0:D4} should be replaced with *
        var format = "{0:D4}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert - starts with * so should return null
        result.Should().BeNull(
            "pattern '*#*' starts with '*' so discrimination by prefix is not possible");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithColonsInFormatSpecifier_ReplacesPlaceholderCorrectly()
    {
        // Arrange - {0:HH:mm:ss} contains colons within the specifier
        var format = "{0:HH:mm:ss}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert - starts with * so should return null
        result.Should().BeNull(
            "pattern '*#*' starts with '*' so discrimination by prefix is not possible");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithSimplePlaceholders_ProducesCorrectPattern()
    {
        // Arrange - backwards compatibility: {0}#{1} without format specifiers
        var format = "{0}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert - starts with * so should return null
        result.Should().BeNull(
            "pattern '*#*' starts with '*' so discrimination by prefix is not possible");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_ReturnsNull_WhenPatternStartsWithWildcard()
    {
        // Arrange - pattern that starts with a placeholder
        var format = "{0}#FIXED#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().BeNull(
            "pattern '*#FIXED#*' starts with '*' which provides no useful discrimination");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithPrefix_ReturnsPatternWithPrefix()
    {
        // Arrange - pattern with a fixed prefix before first placeholder
        var format = "ORDER#{0:yyyy-MM-dd}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("ORDER#*#*",
            "fixed prefix 'ORDER#' followed by replaced placeholders");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithPrefixAndSimplePlaceholders_ReturnsPattern()
    {
        // Arrange - backwards compatibility with prefix
        var format = "ORDER#{0}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("ORDER#*#*");
    }

    [Fact]
    public void DeriveDiscriminatorPattern_WithMixedPlaceholders_ReplacesAll()
    {
        // Arrange - mix of simple and format-specifier placeholders
        var format = "PREFIX#{0:D4}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("PREFIX#*#*");
    }

    #endregion

    #region ValidateComputedKeyFormat Tests (via source generator)

    [Fact]
    public void ValidateComputedKeyFormat_WithFormatSpecifiers_CorrectlyCountsPlaceholders()
    {
        // Arrange - {0:yyyy-MM-dd}#{1} has 2 distinct placeholders matching 2 source properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""EventDate"", ""Category"", Format = ""{0:yyyy-MM-dd}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public string EventDate { get; set; } = string.Empty;

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should NOT emit FDDB090 (placeholder count matches)
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "format '{0:yyyy-MM-dd}#{1}' has 2 placeholders matching 2 source properties");
    }

    [Fact]
    public void ValidateComputedKeyFormat_WithFormatSpecifierMismatch_EmitsFDDB090()
    {
        // Arrange - {0:D4} has 1 placeholder but 2 source properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Priority"", ""Name"", Format = ""{0:D4}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public string Priority { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB090",
            "format '{0:D4}' has 1 placeholder but 2 source properties");
    }

    [Fact]
    public void ValidateComputedKeyFormat_WithRepeatedIndices_CountsDistinctIndices()
    {
        // Arrange - {0:D4}#{0:G}#{1} uses index 0 twice but distinct count is 2
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Priority"", ""Name"", Format = ""{0:D4}#{0:G}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public string Priority { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - max index is 1, so placeholder count = 2, matching 2 source properties
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "repeated index {0} should not inflate the count; distinct indices are 0 and 1 = 2 source properties");
    }

    [Fact]
    public void ValidateComputedKeyFormat_WithInvalidPlaceholderIndex_EmitsDiagnostic()
    {
        // Arrange - {abc:format} has non-numeric index portion
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Name"", Format = ""{abc:format}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should emit DYNDB036 (InvalidComputedKeyFormat) for invalid placeholder
        result.Diagnostics.Should().Contain(d => d.Id == "DYNDB036",
            "placeholder '{abc:format}' has a non-numeric index and should trigger an invalid format diagnostic");
    }

    #endregion

    #region Helper Methods

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[]
            {
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
