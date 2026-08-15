using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property-based tests for constant key non-const reference bugfix.
/// 
/// **Feature: constant-key-non-const-reference**
/// - Property 2: Preservation - Compile-Time Constant Keys Continue To Resolve
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
/// 
/// These tests confirm baseline behavior on UNFIXED code for non-buggy inputs.
/// All tests MUST PASS on the current unfixed code to establish a regression baseline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
[Trait("Category", "Preservation")]
public class ConstantKeyNonConstReferencePreservationTests
{
    /// <summary>
    /// Generates non-empty strings that are valid C# string literal content.
    /// Avoids characters that would break string literal parsing.
    /// </summary>
    private static Arbitrary<string> ValidStringLiteralArb()
    {
        var gen = Gen.Elements(
            "PROFILE", "USER", "ORDER", "META", "CONFIG", "SETTINGS",
            "TYPE#CUSTOMER", "v1", "entity-type", "sk_constant",
            "Hello World", "test value 123", "foo bar baz",
            "ALPHA", "BETA", "GAMMA", "DELTA", "EPSILON",
            "item_type", "RECORD", "DATA", "STATUS#ACTIVE",
            "prefix:value", "a", "AB", "XYZ", "constant_key_value",
            "MY_PARTITION", "SORT_KEY_VALUE", "fixed-sk", "pk-constant");

        return gen.ToArbitrary();
    }

    #region Property 2.1: Expression-body string literal preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.1**
    /// 
    /// Property: For any random non-empty string V, an expression-body key `=> "V"`
    /// must resolve ConstantKeyValue = V and IsConstantKey = true.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpressionBody_StringLiteral_ResolvesConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodyStringLiteralSource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            var constantKeyCorrect = keyProperty.ConstantKeyValue == value;
            var isConstantKey = keyProperty.IsConstantKey;

            return (constantKeyCorrect && isConstantKey)
                .Label($"Expected ConstantKeyValue='{value}' (got '{keyProperty.ConstantKeyValue}'), " +
                       $"IsConstantKey=true (got {keyProperty.IsConstantKey})");
        });
    }

    #endregion

    #region Property 2.2: Const field reference preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.2**
    /// 
    /// Property: For any `const string` field reference, GetConstantValue() must resolve
    /// the value and set IsConstantKey = true with ConstantKeyValue matching the const value.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpressionBody_ConstFieldReference_ResolvesConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodyConstFieldSource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            var constantKeyCorrect = keyProperty.ConstantKeyValue == value;
            var isConstantKey = keyProperty.IsConstantKey;

            return (constantKeyCorrect && isConstantKey)
                .Label($"Expected ConstantKeyValue='{value}' (got '{keyProperty.ConstantKeyValue}'), " +
                       $"IsConstantKey=true (got {keyProperty.IsConstantKey})");
        });
    }

    #endregion

    #region Property 2.3: Read-only auto-property string literal preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.3**
    /// 
    /// Property: For any random non-empty string V, a read-only auto-property key
    /// `{ get; } = "V"` must resolve ConstantKeyValue = V and IsConstantKey = true.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAutoProperty_StringLiteral_ResolvesConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertyStringLiteralSource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            var constantKeyCorrect = keyProperty.ConstantKeyValue == value;
            var isConstantKey = keyProperty.IsConstantKey;

            return (constantKeyCorrect && isConstantKey)
                .Label($"Expected ConstantKeyValue='{value}' (got '{keyProperty.ConstantKeyValue}'), " +
                       $"IsConstantKey=true (got {keyProperty.IsConstantKey})");
        });
    }

    #endregion

    #region Property 2.4: Read-only auto-property const field preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.4**
    /// 
    /// Property: For any `const string` field initializer in a read-only auto-property,
    /// GetConstantValue() must resolve the value and set IsConstantKey = true.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAutoProperty_ConstFieldReference_ResolvesConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertyConstFieldSource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            var constantKeyCorrect = keyProperty.ConstantKeyValue == value;
            var isConstantKey = keyProperty.IsConstantKey;

            return (constantKeyCorrect && isConstantKey)
                .Label($"Expected ConstantKeyValue='{value}' (got '{keyProperty.ConstantKeyValue}'), " +
                       $"IsConstantKey=true (got {keyProperty.IsConstantKey})");
        });
    }

    #endregion

    #region Property 2.5: Mutable key property preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.5**
    /// 
    /// Property: For any property with `{ get; set; }`, constant key detection must not
    /// be attempted and IsConstantKey must remain false, regardless of initializer value.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MutableKeyProperty_NoConstantKeyDetection()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateMutableKeyPropertySource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            return (!keyProperty.IsConstantKey)
                .Label($"Expected IsConstantKey=false for mutable key, " +
                       $"got ConstantKeyValue='{keyProperty.ConstantKeyValue}'");
        });
    }

    #endregion

    #region Property 2.6: Non-key property preservation

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.5, 3.6**
    /// 
    /// Property: For any property without [PartitionKey]/[SortKey], no constant key
    /// detection occurs regardless of property syntax.
    /// This behavior must be preserved after the fix is applied.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonKeyProperty_NoConstantKeyDetection()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateNonKeyPropertySource(value);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            // The non-key property "Description" should never have IsConstantKey set
            var nonKeyProperty = result.Properties.FirstOrDefault(p =>
                p.PropertyName == "Description");
            if (nonKeyProperty == null)
                return false.Label($"No 'Description' property found for value '{value}'");

            return (!nonKeyProperty.IsConstantKey)
                .Label($"Expected IsConstantKey=false for non-key property, " +
                       $"got ConstantKeyValue='{nonKeyProperty.ConstantKeyValue}'");
        });
    }

    #endregion

    #region Source Generator Pipeline Preservation Tests

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.1, 3.6**
    /// 
    /// Property: For any string literal constant key, the source generator DOES NOT
    /// emit FDDB126 diagnostic. Validates via the full source generator pipeline.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property StringLiteralConstantKey_DoesNotEmitFDDB126()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodyStringLiteralSource(value);
            var result = RunSourceGenerator(source);

            var hasFddb126 = result.AllDiagnostics.Any(d => d.Id == "FDDB126");

            return (!hasFddb126)
                .Label($"Unexpected FDDB126 diagnostic emitted for valid string literal key '{value}'. " +
                       $"Diagnostics: [{string.Join(", ", result.AllDiagnostics.Select(d => $"{d.Id}:{d.Severity}"))}]");
        });
    }

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.2, 3.6**
    /// 
    /// Property: For any const field reference key, the source generator DOES NOT
    /// emit FDDB126 diagnostic. Validates via the full source generator pipeline.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ConstFieldReferenceKey_DoesNotEmitFDDB126()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodyConstFieldSource(value);
            var result = RunSourceGenerator(source);

            var hasFddb126 = result.AllDiagnostics.Any(d => d.Id == "FDDB126");

            return (!hasFddb126)
                .Label($"Unexpected FDDB126 diagnostic emitted for const field reference key '{value}'. " +
                       $"Diagnostics: [{string.Join(", ", result.AllDiagnostics.Select(d => $"{d.Id}:{d.Severity}"))}]");
        });
    }

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.6**
    /// 
    /// Property: For any string literal constant key, the generated code does NOT
    /// contain property assignment for that key in FromDynamoDb (constant keys are
    /// skipped in deserialization since they are fixed values).
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ConstantKey_GeneratedCode_DoesNotContainKeyAssignment()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodyStringLiteralSource(value);
            var result = RunSourceGenerator(source);

            var generatedCode = result.EntityGeneratedCode ?? "";

            // Constant keys should NOT have property assignment in FromDynamoDb
            var containsSkAssignment = generatedCode.Contains("entity.Sk =") ||
                                       generatedCode.Contains("entity.Sk=");

            return (!containsSkAssignment)
                .Label($"Generated code contains 'entity.Sk =' for constant key '{value}'. " +
                       $"Constant keys should not have property assignment in FromDynamoDb.");
        });
    }

    /// <summary>
    /// **Feature: constant-key-non-const-reference, Property 2: Preservation**
    /// **Validates: Requirements 3.5**
    /// 
    /// Property: For any mutable key property ({ get; set; }), the generated code
    /// DOES contain property assignment in FromDynamoDb (normal deserialization behavior).
    /// </summary>
    [Property(MaxTest = 30)]
    public Property MutableKey_GeneratedCode_ContainsKeyAssignment()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateMutableKeyPropertySource(value);
            var result = RunSourceGenerator(source);

            var generatedCode = result.EntityGeneratedCode ?? "";

            // Mutable keys SHOULD have property assignment in FromDynamoDb
            var containsSkAssignment = generatedCode.Contains("entity.Sk =") ||
                                       generatedCode.Contains("entity.Sk=");

            return containsSkAssignment
                .Label($"Generated code does NOT contain 'entity.Sk =' for mutable key. " +
                       $"Mutable keys should have property assignment in FromDynamoDb.");
        });
    }

    #endregion

    #region Source Generation Helpers

    /// <summary>
    /// Expression-body with string literal.
    /// e.g., [SortKey] public string Sk => "VALUE";
    /// </summary>
    private static string GenerateExpressionBodyStringLiteralSource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Expression-body with const field reference.
    /// e.g., [SortKey] public string Sk => Constants.KeyValue;
    /// </summary>
    private static string GenerateExpressionBodyConstFieldSource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    public static class Constants
    {{
        public const string KeyValue = ""{escapedValue}"";
    }}

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => Constants.KeyValue;
    }}
}}";
    }

    /// <summary>
    /// Read-only auto-property with string literal initializer.
    /// e.g., [SortKey] public string Sk { get; } = "VALUE";
    /// </summary>
    private static string GenerateReadOnlyAutoPropertyStringLiteralSource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; }} = ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Read-only auto-property with const field initializer.
    /// e.g., [SortKey] public string Sk { get; } = Constants.KeyValue;
    /// </summary>
    private static string GenerateReadOnlyAutoPropertyConstFieldSource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    public static class Constants
    {{
        public const string KeyValue = ""{escapedValue}"";
    }}

    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; }} = Constants.KeyValue;
    }}
}}";
    }

    /// <summary>
    /// Mutable key property with { get; set; } and string initializer.
    /// Constant key detection must NOT be attempted.
    /// </summary>
    private static string GenerateMutableKeyPropertySource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Non-key property with expression-body syntax.
    /// No constant key detection should occur for non-key properties.
    /// </summary>
    private static string GenerateNonKeyPropertySource(string value)
    {
        var escapedValue = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""description"")]
        public string Description => ""{escapedValue}"";
    }}
}}";
    }

    #endregion

    #region Infrastructure

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "TestEntity");

        return (classDecl, semanticModel);
    }

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

    private class GeneratorResult
    {
        public string? EntityGeneratedCode { get; set; }
        public IReadOnlyList<Diagnostic> AllDiagnostics { get; set; } = Array.Empty<Diagnostic>();
    }

    #endregion
}
