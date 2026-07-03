using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Unit tests for FDDB120–FDDB123 constant key conflict diagnostics.
/// Validates Requirements 9.1, 9.2, 9.3, 9.4.
/// </summary>
[Trait("Category", "Unit")]
public class ConstantKeyDiagnosticTests
{
    #region FDDB120 — Constant Key + [Computed] Conflict

    [Fact]
    public void ConstantKey_WithComputedAttribute_ShouldEmitFDDB120()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""Field1"", Separator = ""#"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB120",
            "Should emit FDDB120 when a constant key also has [Computed]");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB120");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Sk");
    }

    [Fact]
    public void ConstantKey_WithComputedAttribute_ShouldHaltCodeGeneration()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""Field1"", Separator = ""#"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no generated code for the entity
        result.GeneratedSources.Should().BeEmpty(
            "Code generation should be halted for entity with FDDB120 error");
    }

    #endregion

    #region FDDB121 — Constant Key + Prefix Conflict

    [Fact]
    public void ConstantKey_WithPrefix_ShouldEmitFDDB121()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey(Prefix = ""USER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""CONSTANT_VALUE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB121",
            "Should emit FDDB121 when a constant key has a Prefix configured");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB121");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void ConstantKey_WithPrefix_ShouldHaltCodeGeneration()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey(Prefix = ""USER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""CONSTANT_VALUE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no generated code for the entity
        result.GeneratedSources.Should().BeEmpty(
            "Code generation should be halted for entity with FDDB121 error");
    }

    [Fact]
    public void ConstantSortKey_WithPrefix_ShouldEmitFDDB121()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""TYPE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB121",
            "Should emit FDDB121 when a constant sort key has a Prefix configured");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB121");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Sk");
    }

    #endregion

    #region FDDB122 — [Extracted] Referencing Constant Key

    [Fact]
    public void ExtractedFromConstantKey_ShouldEmitFDDB122()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";

        [Extracted(""Sk"", 0)]
        [DynamoDbAttribute(""part1"")]
        public string Part1 { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB122",
            "Should emit FDDB122 when [Extracted] references a constant key property");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB122");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Part1");
        diagnostic.GetMessage().Should().Contain("Sk");
    }

    [Fact]
    public void ExtractedFromConstantKey_ShouldHaltCodeGeneration()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";

        [Extracted(""Sk"", 0)]
        [DynamoDbAttribute(""part1"")]
        public string Part1 { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no generated code for the entity
        result.GeneratedSources.Should().BeEmpty(
            "Code generation should be halted for entity with FDDB122 error");
    }

    [Fact]
    public void ExtractedFromConstantPartitionKey_ShouldEmitFDDB122()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""TENANT_FIXED"";

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""tenant"")]
        public string Tenant { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB122",
            "Should emit FDDB122 when [Extracted] references a constant partition key");
    }

    #endregion

    #region FDDB123 — Empty/Whitespace Constant Key Value

    [Fact]
    public void ConstantKey_WithEmptyValue_ShouldEmitFDDB123()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => """";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB123",
            "Should emit FDDB123 when constant key value is an empty string");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB123");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void ConstantKey_WithWhitespaceOnlyValue_ShouldEmitFDDB123()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""   "";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB123",
            "Should emit FDDB123 when constant key value is whitespace only");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB123");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ConstantKey_WithEmptyValue_ShouldHaltCodeGeneration()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => """";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no generated code for the entity
        result.GeneratedSources.Should().BeEmpty(
            "Code generation should be halted for entity with FDDB123 error");
    }

    [Fact]
    public void ConstantSortKey_WithEmptyValue_ShouldEmitFDDB123()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => """";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB123",
            "Should emit FDDB123 for empty constant sort key value");
    }

    #endregion

    #region Negative Tests — Valid Cases Should NOT Emit Diagnostics

    [Fact]
    public void ValidConstantKey_ShouldNotEmitAnyConstantKeyDiagnostics()
    {
        // Arrange — a valid constant sort key with no conflicts
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB120");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB121");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB122");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB123");
    }

    [Fact]
    public void NonConstantKey_WithComputed_ShouldNotEmitFDDB120()
    {
        // Arrange — a normal (non-constant) sort key with [Computed] is valid
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB120",
            "Non-constant key with [Computed] is a valid configuration");
    }

    [Fact]
    public void NonConstantKey_WithPrefix_ShouldNotEmitFDDB121()
    {
        // Arrange — a normal (non-constant) key with Prefix is valid
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey(Prefix = ""USER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB121",
            "Non-constant key with Prefix is a valid configuration");
    }

    #endregion

    #region Descriptor Property Tests

    [Fact]
    public void FDDB120_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.ConstantKeyComputedConflict;

        descriptor.Id.Should().Be("FDDB120");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    [Fact]
    public void FDDB121_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.ConstantKeyPrefixConflict;

        descriptor.Id.Should().Be("FDDB121");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    [Fact]
    public void FDDB122_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.ConstantKeyExtractedConflict;

        descriptor.Id.Should().Be("FDDB122");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    [Fact]
    public void FDDB123_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.ConstantKeyEmptyValue;

        descriptor.Id.Should().Be("FDDB123");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    #endregion

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
