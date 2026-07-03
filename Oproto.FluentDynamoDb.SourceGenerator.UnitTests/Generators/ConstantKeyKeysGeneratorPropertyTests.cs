using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for Keys class generation with constant keys.
/// 
/// **Feature: constant-key-detection**
/// - Property 5: Keys class provides parameterless accessor for constant keys (Validates: Requirements 4.1, 4.4)
/// - Property 6: Composite Key() method accepts only variable parameters (Validates: Requirements 4.2)
/// 
/// These tests verify that the source generator produces a parameterless static property
/// in the Keys class for constant keys and does NOT produce a parameterized method.
/// They also verify that the composite Key() method only accepts variable parameters.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyKeysGeneratorPropertyTests
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

    #region Property 5: Keys class provides parameterless accessor for constant keys

    /// <summary>
    /// **Feature: constant-key-detection, Property 5: Keys class provides parameterless accessor for constant keys**
    /// **Validates: Requirements 4.1, 4.4**
    /// 
    /// Property: For any constant sort key property with value V (expression-body syntax),
    /// the generated Keys class SHALL contain a parameterless static property returning V
    /// and SHALL NOT contain a parameterized method accepting a value for that key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ExpressionBody_GeneratesParameterlessAccessor()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify parameterless property exists: public static string Sk => "VALUE";
            var hasParameterlessProperty = keysCode.Contains($"public static string Sk => \"{escapedValue}\";");

            // Verify NO parameterized method exists for Sk: public static string Sk(string sk)
            var hasParameterizedMethod = keysCode.Contains("public static string Sk(string sk)");

            return (hasParameterlessProperty && !hasParameterizedMethod)
                .Label($"hasParameterlessProperty={hasParameterlessProperty}, hasParameterizedMethod={hasParameterizedMethod}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 5: Keys class provides parameterless accessor for constant keys**
    /// **Validates: Requirements 4.1, 4.4**
    /// 
    /// Property: For any constant partition key property with value V (expression-body syntax),
    /// the generated Keys class SHALL contain a parameterless static property returning V
    /// and SHALL NOT contain a parameterized method accepting a value for that key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_ExpressionBody_GeneratesParameterlessAccessor()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyExpressionBodySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify parameterless property exists: public static string Pk => "VALUE";
            var hasParameterlessProperty = keysCode.Contains($"public static string Pk => \"{escapedValue}\";");

            // Verify NO parameterized method exists for Pk: public static string Pk(string pk)
            var hasParameterizedMethod = keysCode.Contains("public static string Pk(string pk)");

            return (hasParameterlessProperty && !hasParameterizedMethod)
                .Label($"hasParameterlessProperty={hasParameterlessProperty}, hasParameterizedMethod={hasParameterizedMethod}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 5: Keys class provides parameterless accessor for constant keys**
    /// **Validates: Requirements 4.1, 4.4**
    /// 
    /// Property: For any constant sort key property with value V (read-only auto-property syntax),
    /// the generated Keys class SHALL contain a parameterless static property returning V
    /// and SHALL NOT contain a parameterized method accepting a value for that key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ReadOnlyAutoProperty_GeneratesParameterlessAccessor()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyReadOnlyAutoPropertySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify parameterless property exists: public static string Sk => "VALUE";
            var hasParameterlessProperty = keysCode.Contains($"public static string Sk => \"{escapedValue}\";");

            // Verify NO parameterized method exists for Sk: public static string Sk(string sk)
            var hasParameterizedMethod = keysCode.Contains("public static string Sk(string sk)");

            return (hasParameterlessProperty && !hasParameterizedMethod)
                .Label($"hasParameterlessProperty={hasParameterlessProperty}, hasParameterizedMethod={hasParameterizedMethod}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    #endregion

    #region Property 6: Composite Key() method accepts only variable parameters

    /// <summary>
    /// **Feature: constant-key-detection, Property 6: Composite Key() method accepts only variable parameters**
    /// **Validates: Requirements 4.2**
    /// 
    /// Property: For any entity with one constant sort key (value C) and one variable partition key
    /// (with prefix), the generated Key() method SHALL accept exactly one parameter (for the variable
    /// partition key) and SHALL return a tuple containing both the variable key result and constant value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeKey_ConstantSortKey_AcceptsOnlyVariablePartitionKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateCompositeConstantSortKeySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            // The Key() method should accept exactly ONE parameter (for the variable PK)
            // Signature: public static (string PartitionKey, string SortKey) Key(string tenantId)
            var hasOneParamKey = keysCode.Contains("(string PartitionKey, string SortKey) Key(string tenantId)");
            if (!hasOneParamKey)
                return false.Label($"Key() method does not have single-parameter signature 'Key(string tenantId)'. " +
                                   $"Value='{value}'");

            // The Key() method should NOT be parameterless (that's for both-constant case)
            var hasParameterlessKey = keysCode.Contains("(string PartitionKey, string SortKey) Key()");
            if (hasParameterlessKey)
                return false.Label($"Key() method is incorrectly parameterless when only SK is constant");

            // The Key() method body should call Pk(param) and reference the Sk constant property
            var bodyReferencesCorrectly = keysCode.Contains("(Pk(tenantId), Sk)");
            if (!bodyReferencesCorrectly)
                return false.Label($"Key() method body does not contain '(Pk(tenantId), Sk)'. Value='{value}'");

            return true.Label("Key() correctly accepts only variable PK parameter and references constant SK");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 6: Composite Key() method accepts only variable parameters**
    /// **Validates: Requirements 4.2**
    /// 
    /// Property: For any entity with one constant partition key (value C) and one variable sort key
    /// (with prefix), the generated Key() method SHALL accept exactly one parameter (for the variable
    /// sort key) and SHALL return a tuple containing both the constant PK value and the variable SK result.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeKey_ConstantPartitionKey_AcceptsOnlyVariableSortKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateCompositeConstantPartitionKeySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            // The Key() method should accept exactly ONE parameter (for the variable SK)
            // Signature: public static (string PartitionKey, string SortKey) Key(string itemId)
            var hasOneParamKey = keysCode.Contains("(string PartitionKey, string SortKey) Key(string itemId)");
            if (!hasOneParamKey)
                return false.Label($"Key() method does not have single-parameter signature 'Key(string itemId)'. " +
                                   $"Value='{value}'");

            // The Key() method should NOT be parameterless (that's for both-constant case)
            var hasParameterlessKey = keysCode.Contains("(string PartitionKey, string SortKey) Key()");
            if (hasParameterlessKey)
                return false.Label($"Key() method is incorrectly parameterless when only PK is constant");

            // The Key() method body should reference the Pk constant property and call Sk(param)
            var bodyReferencesCorrectly = keysCode.Contains("(Pk, Sk(itemId))");
            if (!bodyReferencesCorrectly)
                return false.Label($"Key() method body does not contain '(Pk, Sk(itemId))'. Value='{value}'");

            return true.Label("Key() correctly accepts only variable SK parameter and references constant PK");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 6: Composite Key() method accepts only variable parameters**
    /// **Validates: Requirements 4.2**
    /// 
    /// Property: For any entity with one constant key and one variable key, the generated Key()
    /// method SHALL return a tuple of type (string PartitionKey, string SortKey).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompositeKey_WithOneConstantKey_ReturnsTupleWithBothKeys()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateCompositeConstantSortKeySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var keysCode = result.EntityGeneratedCode;
            if (keysCode == null)
                return false.Label("No generated code found for TestEntity");

            // The Key() method should return a (string PartitionKey, string SortKey) tuple
            var hasTupleReturn = keysCode.Contains("(string PartitionKey, string SortKey) Key(");
            if (!hasTupleReturn)
                return false.Label($"Key() method does not return (string PartitionKey, string SortKey) tuple. Value='{value}'");

            return true.Label("Key() method correctly returns (string PartitionKey, string SortKey) tuple");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with a constant sort key using expression-body syntax
    /// and a regular variable partition key.
    /// </summary>
    private static string GenerateConstantSortKeyExpressionBodySource(string constantValue)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
    /// Generates source code with a constant partition key using expression-body syntax.
    /// </summary>
    private static string GenerateConstantPartitionKeyExpressionBodySource(string constantValue)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
    /// Generates source code with a constant sort key using read-only auto-property syntax
    /// and a regular variable partition key.
    /// </summary>
    private static string GenerateConstantSortKeyReadOnlyAutoPropertySource(string constantValue)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
    /// Escapes a string the same way the source generator does for generated code output.
    /// </summary>
    private static string EscapeForGeneratedCode(string value)
    {
        // The generator uses MapperGenerator.EscapeString which escapes backslashes and quotes
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Generates source code with a variable partition key (with prefix) and a constant sort key
    /// (expression-body). Used for composite Key() method testing.
    /// </summary>
    private static string GenerateCompositeConstantSortKeySource(string constantSkValue)
    {
        var escapedValue = constantSkValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string TenantId {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""{escapedValue}"";
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant partition key (expression-body) and a variable sort key
    /// (with prefix). Used for composite Key() method testing.
    /// </summary>
    private static string GenerateCompositeConstantPartitionKeySource(string constantPkValue)
    {
        var escapedValue = constantPkValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

        [SortKey(Prefix = ""ITEM"")]
        [DynamoDbAttribute(""sk"")]
        public string ItemId {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Runs the full source generator pipeline on the provided source and returns the result.
    /// </summary>
    private static GeneratorResult RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Find the generated source for TestEntity
        var generatedTree = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .FirstOrDefault(t => t.FilePath.Contains("TestEntity.g.cs"));

        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        return new GeneratorResult
        {
            EntityGeneratedCode = generatedTree?.GetText().ToString(),
            HasErrors = errors.Length > 0,
            ErrorMessages = string.Join("; ", errors.Select(e => e.GetMessage()))
        };
    }

    private class GeneratorResult
    {
        public string? EntityGeneratedCode { get; set; }
        public bool HasErrors { get; set; }
        public string ErrorMessages { get; set; } = string.Empty;
    }

    #endregion
}
