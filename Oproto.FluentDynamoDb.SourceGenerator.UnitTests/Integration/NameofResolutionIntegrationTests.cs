using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests that verify the full source generator produces correct
/// code when nameof() and const expressions are used in [Computed] and [Extracted] attributes.
///
/// These tests go beyond the EntityAnalyzer unit tests — they run the complete generator pipeline
/// and verify the generated code compiles, references the correct properties, and produces
/// valid string.Format() calls.
/// </summary>
public class NameofResolutionIntegrationTests
{
    [Fact]
    public void SourceGenerator_ComputedWithNameof_GeneratesCorrectStringFormat()
    {
        // Arrange - entity using nameof() in [Computed] Format attribute
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(UserId), Format = ""USER#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "UserEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // Verify the computed key logic uses string.Format with the correct property reference
        // The mapper should generate: typedEntity.Pk = string.Format("USER#{0}", typedEntity.UserId);
        entityCode.Should().Contain("string.Format(\"USER#{0}\", typedEntity.UserId)",
            "Generated code should reference UserId in string.Format when nameof(UserId) is used");

        // Verify the Keys.BuildPk method is generated with the correct parameter
        entityCode.Should().Contain("BuildPk",
            "Generated code should include a BuildPk method for the computed key");
        entityCode.Should().Contain("string.Format(\"USER#{0}\", userId)",
            "BuildPk should use string.Format with the userId parameter");
    }

    [Fact]
    public void SourceGenerator_ComputedWithMultipleNameof_GeneratesSeparatorConcatenation()
    {
        // Arrange - entity using multiple nameof() in [Computed] with Separator (string types)
        // Using property names that are NOT DynamoDB reserved words
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(TenantId), nameof(EventId), nameof(OrderNum), Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventId"")]
        public string EventId { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNum"")]
        public string OrderNum { get; set; } = string.Empty;

        [DynamoDbAttribute(""title"")]
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "EventEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // The mapper should concatenate all three properties with separator
        entityCode.Should().Contain("typedEntity.TenantId",
            "Generated code should reference TenantId property");
        entityCode.Should().Contain("typedEntity.EventId",
            "Generated code should reference EventId property");
        entityCode.Should().Contain("typedEntity.OrderNum",
            "Generated code should reference OrderNum property");

        // Verify the Keys.BuildPk method is generated with correct parameters
        entityCode.Should().Contain("BuildPk(string tenantId, string eventId, string orderNum)",
            "BuildPk should accept all three source properties as parameters");
    }

    [Fact]
    public void SourceGenerator_ExtractedWithNameof_GeneratesCorrectExtraction()
    {
        // Arrange - entity using nameof() in [Extracted] attributes
        // Use separate source/extracted properties to avoid circular dependency
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""EventId"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventId"")]
        public string EventId { get; set; } = string.Empty;

        [DynamoDbAttribute(""extractedTenant"")]
        [Extracted(nameof(Pk), 0)]
        public string ExtractedTenant { get; set; } = string.Empty;

        [DynamoDbAttribute(""extractedEvent"")]
        [Extracted(nameof(Pk), 1)]
        public string ExtractedEvent { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "EventEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // Verify extraction helper is generated — nameof(Pk) resolved to "Pk"
        entityCode.Should().Contain("ExtractPkComponents",
            "Generated code should include ExtractPkComponents helper");
    }

    [Fact]
    public void SourceGenerator_ComputedWithConstString_GeneratesCorrectCode()
    {
        // Arrange - entity using const string in [Computed]
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users"")]
    public partial class UserEntity
    {
        private const string UserIdProperty = ""UserId"";

        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(UserIdProperty, Format = ""USER#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "UserEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // The const resolves to "UserId", so generated code should reference the UserId property
        entityCode.Should().Contain("string.Format(\"USER#{0}\", typedEntity.UserId)",
            "Generated code should resolve const string to actual property reference");
        entityCode.Should().Contain("BuildPk",
            "BuildPk method should be generated");
    }

    [Fact]
    public void SourceGenerator_ExtractedWithConstInt_GeneratesCorrectIndex()
    {
        // Arrange - entity using const int for index in [Extracted]
        // Use separate extracted properties to avoid circular dependency
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        private const int TenantIndex = 0;
        private const int EventIndex = 1;

        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""EventId"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventId"")]
        public string EventId { get; set; } = string.Empty;

        [DynamoDbAttribute(""extractedTenant"")]
        [Extracted(""Pk"", TenantIndex)]
        public string ExtractedTenant { get; set; } = string.Empty;

        [DynamoDbAttribute(""extractedEvent"")]
        [Extracted(""Pk"", EventIndex)]
        public string ExtractedEvent { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "EventEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // Verify extraction assigns to both properties from the split
        entityCode.Should().Contain("ExtractPkComponents",
            "Generated code should include extraction helper");
        entityCode.Should().Contain("ExtractedTenant",
            "Generated code should assign to ExtractedTenant property");
        entityCode.Should().Contain("ExtractedEvent",
            "Generated code should assign to ExtractedEvent property");
    }

    [Fact]
    public void SourceGenerator_MixedNameofAndLiterals_GeneratesCorrectCode()
    {
        // Arrange - entity mixing nameof() and string literals in same [Computed]
        // Using property names that are NOT DynamoDB reserved words
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(TenantId), ""EventId"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventId"")]
        public string EventId { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - should compile without errors
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty($"Expected no errors but got: {string.Join(", ", errors.Select(e => e.GetMessage()))}");

        // Verify entity code is generated
        var entityCode = GetGeneratedSource(result, "EventEntity.g.cs");
        entityCode.Should().NotBeNull("Entity source file should be generated");

        // Both properties should be referenced in the generated code
        entityCode.Should().Contain("typedEntity.TenantId",
            "Generated code should reference TenantId (resolved from nameof)");
        entityCode.Should().Contain("typedEntity.EventId",
            "Generated code should reference EventId (from string literal)");

        // BuildPk should accept both parameters
        entityCode.Should().Contain("BuildPk(string tenantId, string eventId)",
            "BuildPk should accept both source properties");
    }

    [Fact]
    public void SourceGenerator_ComputedWithNameof_GeneratedCodeCompiles()
    {
        // Arrange - full end-to-end: generate code and verify the output compilation has no errors
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users"")]
    public partial class UserEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(UserId), Format = ""USER#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act - run the generator and get the full output compilation
        var inputCompilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out _);

        // Assert - the output compilation (input + generated code) should have no errors
        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        compilationErrors.Should().BeEmpty(
            $"Generated code should compile without errors. Errors: {string.Join("\n", compilationErrors.Select(e => $"{e.Id}: {e.GetMessage()}"))}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

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

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics.ToImmutableArray(),
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }
}
