using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests that verify the full source generation pipeline correctly handles
/// compound key discrimination when two entities share the same SK prefix but have
/// different PK prefixes on the same table.
/// Validates: Requirements 3.1, 3.3, 6.1, 6.2, 6.3, 6.4
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator tests require dynamic assembly loading for verification")]
public class CompoundKeyDiscriminationIntegrationTests
{
    /// <summary>
    /// Verifies the full source generation pipeline for two entities with same SK prefix
    /// but different PK prefixes:
    /// - No FDDB102 diagnostics emitted (suppressed by compound promotion)
    /// - No DISC004 diagnostics emitted (suppressed)
    /// - FDDB104 info diagnostics ARE emitted (confirming compound promotion worked)
    /// - Generated code contains correct compound checks
    /// - Generated code compiles without errors
    /// </summary>
    [Fact]
    public void SameSkPrefix_DifferentPkPrefix_CompoundPromotionResolvesOverlap()
    {
        // Arrange - Two entities sharing same table with same SK prefix but different PK prefixes
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""capabilities"", IsDefault = true)]
    public partial class PlatformCapability
    {
        [PartitionKey(Prefix = ""PLATFORM"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""CAP"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""capabilities"")]
    public partial class TenantCapability
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""CAP"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

        // Act - run source generator
        var result = GenerateCode(source);

        // Assert 1: No FDDB102 diagnostics emitted (suppressed by compound promotion)
        var fddb102Diagnostics = result.Diagnostics.Where(d => d.Id == "FDDB102").ToList();
        fddb102Diagnostics.Should().BeEmpty(
            "FDDB102 diagnostics should be suppressed when compound promotion resolves the overlap");

        // Assert 2: No DISC004 diagnostics emitted (suppressed)
        var disc004Diagnostics = result.Diagnostics.Where(d => d.Id == "DISC004").ToList();
        disc004Diagnostics.Should().BeEmpty(
            "DISC004 diagnostics should be suppressed when compound promotion resolves the overlap");

        // Assert 3: FDDB104 info diagnostics ARE emitted (confirming compound promotion worked)
        var fddb104Diagnostics = result.Diagnostics.Where(d => d.Id == "FDDB104").ToList();
        fddb104Diagnostics.Should().NotBeEmpty(
            "FDDB104 info diagnostics should be emitted to confirm compound promotion resolved the overlap");
        fddb104Diagnostics.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Info,
            "FDDB104 diagnostics should have Info severity");

        // Assert 4: Generated code for PlatformCapability contains StartsWith("PLATFORM#") compound check
        var platformCode = GetGeneratedSource(result, "PlatformCapability.g.cs");
        platformCode.Should().Contain("StartsWith(\"PLATFORM#\")",
            "PlatformCapability should have a compound constraint checking pk StartsWith(\"PLATFORM#\")");

        // Assert 5: Generated code for TenantCapability contains StartsWith("TENANT#") compound check
        var tenantCode = GetGeneratedSource(result, "TenantCapability.g.cs");
        tenantCode.Should().Contain("StartsWith(\"TENANT#\")",
            "TenantCapability should have a compound constraint checking pk StartsWith(\"TENANT#\")");

        // Assert 6: Generated code compiles without errors (verified via Roslyn in-memory compilation)
        var compilationDiagnostics = GetCompilationErrors(source);
        compilationDiagnostics.Should().BeEmpty(
            "Generated code should compile without errors");
    }

    /// <summary>
    /// Verifies that generated MatchesEntity methods correctly discriminate items
    /// based on compound key checks.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Fact]
    public void SameSkPrefix_DifferentPkPrefix_MatchesEntityCorrectlyDiscriminates()
    {
        // Arrange - Same entities as above
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""capabilities"", IsDefault = true)]
    public partial class PlatformCapability
    {
        [PartitionKey(Prefix = ""PLATFORM"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""CAP"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""capabilities"")]
    public partial class TenantCapability
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""CAP"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

        // Act - compile with source generator and load dynamically
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var platformType = compilationResult.Assembly.GetType("TestNamespace.PlatformCapability")!;
        var tenantType = compilationResult.Assembly.GetType("TestNamespace.TenantCapability")!;

        // Get MatchesEntity methods
        var platformMatchesEntity = GetMatchesEntityMethod(platformType);
        var tenantMatchesEntity = GetMatchesEntityMethod(tenantType);

        // Assert: PlatformCapability matches items with PLATFORM# pk and CAP# sk
        platformMatchesEntity(CreateItem("PLATFORM#web", "CAP#read")).Should().BeTrue(
            "PlatformCapability should match items with pk starting with 'PLATFORM#' and sk starting with 'CAP#'");

        // Assert: PlatformCapability does NOT match items with TENANT# pk
        platformMatchesEntity(CreateItem("TENANT#acme", "CAP#read")).Should().BeFalse(
            "PlatformCapability should NOT match items with pk starting with 'TENANT#'");

        // Assert: TenantCapability matches items with TENANT# pk and CAP# sk
        tenantMatchesEntity(CreateItem("TENANT#acme", "CAP#write")).Should().BeTrue(
            "TenantCapability should match items with pk starting with 'TENANT#' and sk starting with 'CAP#'");

        // Assert: TenantCapability does NOT match items with PLATFORM# pk
        tenantMatchesEntity(CreateItem("PLATFORM#web", "CAP#write")).Should().BeFalse(
            "TenantCapability should NOT match items with pk starting with 'PLATFORM#'");

        // Assert: Neither matches items with unrelated pk prefix
        platformMatchesEntity(CreateItem("OTHER#xyz", "CAP#read")).Should().BeFalse(
            "PlatformCapability should NOT match items with unrelated pk prefix");
        tenantMatchesEntity(CreateItem("OTHER#xyz", "CAP#read")).Should().BeFalse(
            "TenantCapability should NOT match items with unrelated pk prefix");

        // Assert: Neither matches items with wrong sk prefix
        platformMatchesEntity(CreateItem("PLATFORM#web", "OTHER#value")).Should().BeFalse(
            "PlatformCapability should NOT match items with wrong sk prefix");
        tenantMatchesEntity(CreateItem("TENANT#acme", "OTHER#value")).Should().BeFalse(
            "TenantCapability should NOT match items with wrong sk prefix");
    }

    /// <summary>
    /// Helper to create a DynamoDB item dictionary with pk and sk attributes.
    /// </summary>
    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    /// <summary>
    /// Gets the MatchesEntity static method from a type and wraps it in a delegate.
    /// </summary>
    private static Func<Dictionary<string, AttributeValue>, bool> GetMatchesEntityMethod(Type type)
    {
        var method = type.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"MatchesEntity method not found on type '{type.Name}'");
        }

        return (item) => (bool)method.Invoke(null, new object[] { item })!;
    }

    /// <summary>
    /// Generates code using the source generator (without loading into assembly).
    /// Returns diagnostics and generated sources for inspection.
    /// </summary>
    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = generatedSources
        };
    }

    /// <summary>
    /// Gets compilation errors from the full pipeline (source + generated code).
    /// Used to verify that generated code compiles without errors.
    /// </summary>
    private static IEnumerable<Diagnostic> GetCompilationErrors(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        return outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }
}
