using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Bug condition exploration property-based tests for constant key non-const reference detection.
/// 
/// **Feature: constant-key-non-const-reference**
/// - Property 1: Bug Condition - Read-Only Key With Non-Const Reference Emits FDDB126
/// 
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3**
/// 
/// These tests assert the EXPECTED (fixed) behavior:
/// - FDDB126 diagnostic emitted for read-only key properties with non-const references
/// - No property assignment generated in FromDynamoDb for read-only key properties
/// 
/// ON UNFIXED CODE, these tests are EXPECTED TO FAIL, confirming the bug exists.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
[Trait("Category", "BugExploration")]
public class ConstantKeyNonConstReferenceBugExplorationTests
{
    /// <summary>
    /// Generates the non-const reference scenarios to test.
    /// Each scenario represents a different way a key property can reference a non-compile-time-constant.
    /// </summary>
    private static Arbitrary<NonConstKeyScenario> NonConstKeyScenarioArb()
    {
        var gen = Gen.Elements(
            new NonConstKeyScenario(
                "ExpressionBody_StaticReadonlyField",
                GenerateExpressionBodyStaticReadonlySource()),
            new NonConstKeyScenario(
                "ReadOnlyAutoProperty_StaticReadonlyInitializer",
                GenerateReadOnlyAutoPropertyStaticReadonlySource()),
            new NonConstKeyScenario(
                "ExpressionBody_MethodCall",
                GenerateExpressionBodyMethodCallSource()),
            new NonConstKeyScenario(
                "ExpressionBody_PropertyAccess",
                GenerateExpressionBodyPropertyAccessSource())
        );

        return gen.ToArbitrary();
    }

    #region Property 1: Bug Condition - Read-Only Key With Non-Const Reference Emits FDDB126

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 1: Bug Condition**
    /// **Validates: Requirements 2.1, 2.2**
    /// 
    /// Property: For any key property using expression-body or read-only auto-property syntax
    /// with a non-compile-time-constant value, the source generator SHALL emit diagnostic
    /// FDDB126 with severity Error.
    /// 
    /// ON UNFIXED CODE: This test will FAIL because no FDDB126 diagnostic is emitted.
    /// This failure CONFIRMS the bug exists.
    /// </summary>
    [Property(MaxTest = 4)]
    public Property NonConstKeyReference_ShallEmitFDDB126Diagnostic()
    {
        return Prop.ForAll(NonConstKeyScenarioArb(), scenario =>
        {
            var result = RunSourceGenerator(scenario.Source);

            // Expected behavior (after fix): FDDB126 should be emitted
            var hasFddb126 = result.AllDiagnostics.Any(d => d.Id == "FDDB126");

            return hasFddb126
                .Label($"Scenario '{scenario.Name}': Expected FDDB126 diagnostic but none was emitted. " +
                       $"Diagnostics found: [{string.Join(", ", result.AllDiagnostics.Select(d => $"{d.Id}:{d.Severity}"))}]");
        });
    }

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 1: Bug Condition**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: For any key property using expression-body or read-only auto-property syntax
    /// with a non-compile-time-constant value, the generated FromDynamoDb() code SHALL NOT
    /// contain a property assignment (entity.Sk = ...) for the read-only property.
    /// 
    /// ON UNFIXED CODE: This test will FAIL because the generator produces
    /// entity.Sk = attrValue.S which is uncompilable (property has no setter).
    /// This failure CONFIRMS the bug exists.
    /// </summary>
    [Property(MaxTest = 4)]
    public Property NonConstKeyReference_ShallNotGeneratePropertyAssignment()
    {
        return Prop.ForAll(NonConstKeyScenarioArb(), scenario =>
        {
            var result = RunSourceGenerator(scenario.Source);

            var generatedCode = result.EntityGeneratedCode ?? "";

            // Expected behavior (after fix): No assignment to Sk property in FromDynamoDb
            // The bug manifests as generating "entity.Sk = " for a property with no setter
            var containsSkAssignment = generatedCode.Contains("entity.Sk =") ||
                                       generatedCode.Contains("entity.Sk=");

            return (!containsSkAssignment)
                .Label($"Scenario '{scenario.Name}': Generated code contains 'entity.Sk =' assignment " +
                       $"for read-only property. This will produce CS0200 at compile time. " +
                       $"Generated code snippet: {ExtractFromDynamoDbSnippet(generatedCode)}");
        });
    }

    #endregion

    #region Source Generation Helpers

    /// <summary>
    /// Expression-body with static readonly field reference.
    /// e.g., [SortKey] public string Sk => StaticFields.Value;
    /// </summary>
    private static string GenerateExpressionBodyStaticReadonlySource()
    {
        return @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public static class StaticFields
    {
        public static readonly string Value = ""SORT_KEY_VALUE"";
    }

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => StaticFields.Value;
    }
}";
    }

    /// <summary>
    /// Read-only auto-property with static readonly initializer.
    /// e.g., [SortKey] public string Sk { get; } = StaticFields.Value;
    /// </summary>
    private static string GenerateReadOnlyAutoPropertyStaticReadonlySource()
    {
        return @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public static class StaticFields
    {
        public static readonly string Value = ""SORT_KEY_VALUE"";
    }

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; } = StaticFields.Value;
    }
}";
    }

    /// <summary>
    /// Expression-body with method call.
    /// e.g., [SortKey] public string Sk => GetKey();
    /// </summary>
    private static string GenerateExpressionBodyMethodCallSource()
    {
        return @"
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
        public string Sk => GetKey();

        private static string GetKey() => ""DYNAMIC_KEY"";
    }
}";
    }

    /// <summary>
    /// Expression-body with property access.
    /// e.g., [SortKey] public string Sk => Config.DefaultKey;
    /// </summary>
    private static string GenerateExpressionBodyPropertyAccessSource()
    {
        return @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public static class Config
    {
        public static string DefaultKey => ""CONFIG_KEY"";
    }

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => Config.DefaultKey;
    }
}";
    }

    #endregion

    #region Infrastructure

    /// <summary>
    /// Runs the full source generator pipeline on the provided source and returns the result.
    /// </summary>
    private static GeneratorResult RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Find the generated source for TestEntity
        var generatedTree = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Length)
            .FirstOrDefault(t => t.FilePath.Contains("TestEntity.g.cs"));

        return new GeneratorResult
        {
            EntityGeneratedCode = generatedTree?.GetText().ToString(),
            AllDiagnostics = diagnostics
        };
    }

    /// <summary>
    /// Extracts a snippet around FromDynamoDb for diagnostic purposes.
    /// </summary>
    private static string ExtractFromDynamoDbSnippet(string generatedCode)
    {
        if (string.IsNullOrEmpty(generatedCode))
            return "(no generated code)";

        var fromDynamoDbIndex = generatedCode.IndexOf("FromDynamoDb", StringComparison.Ordinal);
        if (fromDynamoDbIndex < 0)
            return "(no FromDynamoDb method found)";

        var start = Math.Max(0, fromDynamoDbIndex - 20);
        var length = Math.Min(300, generatedCode.Length - start);
        return generatedCode.Substring(start, length) + "...";
    }

    private class GeneratorResult
    {
        public string? EntityGeneratedCode { get; set; }
        public IReadOnlyList<Diagnostic> AllDiagnostics { get; set; } = Array.Empty<Diagnostic>();
    }

    /// <summary>
    /// Represents a test scenario for non-const key property references.
    /// </summary>
    private record NonConstKeyScenario(string Name, string Source)
    {
        public override string ToString() => Name;
    }

    #endregion
}
