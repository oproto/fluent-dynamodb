using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Property-based tests for constant key diagnostic emission.
/// 
/// **Feature: constant-key-detection**
/// - Property 11: Empty or whitespace constant key value produces error diagnostic (Validates: Requirements 9.4)
/// 
/// These tests verify that the source generator emits FDDB123 with severity Error
/// when a constant key property has an empty or whitespace-only string value.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyDiagnosticPropertyTests
{
    /// <summary>
    /// Generates strings composed entirely of whitespace characters (spaces, tabs) and the empty string.
    /// These are all invalid constant key values that should trigger FDDB123.
    /// </summary>
    private static Arbitrary<string> WhitespaceOnlyStringArb()
    {
        var gen = Gen.OneOf(
            Gen.Constant(""),
            Gen.Choose(1, 10).Select(count => new string(' ', count)),
            Gen.Choose(1, 5).Select(count => new string('\t', count)),
            Gen.Choose(1, 8).SelectMany(count =>
                Gen.ArrayOf(count, Gen.Elements(' ', '\t'))
                    .Select(chars => new string(chars)))
        );

        return gen.ToArbitrary();
    }

    #region Property 11: Empty or whitespace constant key value produces error diagnostic

    /// <summary>
    /// **Feature: constant-key-detection, Property 11: Empty or whitespace constant key value produces error diagnostic**
    /// **Validates: Requirements 9.4**
    /// 
    /// Property: For any string composed entirely of whitespace characters (including the empty string)
    /// used as a constant key value via expression-body syntax, the source generator SHALL emit
    /// diagnostic FDDB123 with severity Error.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpressionBody_WithEmptyOrWhitespaceValue_EmitsFDDB123()
    {
        return Prop.ForAll(WhitespaceOnlyStringArb(), value =>
        {
            var source = GenerateExpressionBodySource(value);
            var diagnostics = RunSourceGenerator(source);

            var fddb123 = diagnostics.FirstOrDefault(d => d.Id == "FDDB123");

            if (fddb123 == null)
                return false.Label($"FDDB123 not emitted for whitespace value '{EscapeForDisplay(value)}'");

            return (fddb123.Severity == DiagnosticSeverity.Error)
                .Label($"Expected Error severity, got {fddb123.Severity} for value '{EscapeForDisplay(value)}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 11: Empty or whitespace constant key value produces error diagnostic**
    /// **Validates: Requirements 9.4**
    /// 
    /// Property: For any string composed entirely of whitespace characters (including the empty string)
    /// used as a constant key value via read-only auto-property syntax, the source generator SHALL emit
    /// diagnostic FDDB123 with severity Error.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAutoProperty_WithEmptyOrWhitespaceValue_EmitsFDDB123()
    {
        return Prop.ForAll(WhitespaceOnlyStringArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertySource(value);
            var diagnostics = RunSourceGenerator(source);

            var fddb123 = diagnostics.FirstOrDefault(d => d.Id == "FDDB123");

            if (fddb123 == null)
                return false.Label($"FDDB123 not emitted for whitespace value '{EscapeForDisplay(value)}'");

            return (fddb123.Severity == DiagnosticSeverity.Error)
                .Label($"Expected Error severity, got {fddb123.Severity} for value '{EscapeForDisplay(value)}'");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with an expression-body partition key returning the given value.
    /// </summary>
    private static string GenerateExpressionBodySource(string constantValue)
    {
        var escapedValue = constantValue
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a read-only auto-property partition key with the given initializer value.
    /// </summary>
    private static string GenerateReadOnlyAutoPropertySource(string constantValue)
    {
        var escapedValue = constantValue
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; }} = ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Runs the source generator on the provided source and returns the generator diagnostics.
    /// </summary>
    private static IReadOnlyList<Diagnostic> RunSourceGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics);

        return generatorDiagnostics;
    }

    /// <summary>
    /// Escapes a string value for display in test labels.
    /// </summary>
    private static string EscapeForDisplay(string value)
    {
        if (value.Length == 0)
            return "<empty>";

        return value
            .Replace("\t", "\\t")
            .Replace(" ", "·");
    }

    #endregion
}
