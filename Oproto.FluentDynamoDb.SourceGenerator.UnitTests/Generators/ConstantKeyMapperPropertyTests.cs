using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for MapperGenerator serialization/deserialization with constant keys.
/// 
/// **Feature: constant-key-detection**
/// - Property 8: Serialization emits constant value directly (Validates: Requirements 6.1, 6.2, 6.3)
/// - Property 9: Deserialization validates constant key value (Validates: Requirements 7.1)
/// 
/// These tests verify that the generated ToDynamoDb method emits constant key values directly
/// as AttributeValue entries without reading from the entity instance, and that the generated
/// FromDynamoDb method validates incoming constant key values and logs warnings on mismatch.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyMapperPropertyTests
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

    #region Property 8: Serialization emits constant value directly

    /// <summary>
    /// **Feature: constant-key-detection, Property 8: Serialization emits constant value directly**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// Property: For any constant sort key property with attribute name "sk" and constant value V
    /// (expression-body syntax), the generated ToDynamoDb method SHALL emit
    /// item["sk"] = new AttributeValue { S = "V" } directly and SHALL NOT read the property
    /// value from the entity instance (typedEntity.Sk).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ExpressionBody_ToDynamoDb_EmitsConstantDirectly()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify that the generated code emits the constant value directly
            // Expected pattern: item["sk"] = new AttributeValue { S = "VALUE" };
            var expectedEmission = $"item[\"sk\"] = new AttributeValue {{ S = \"{escapedValue}\" }};";
            var emitsConstantDirectly = generatedCode.Contains(expectedEmission);

            // Verify that the generated code does NOT read from entity instance for the SK property
            // It should NOT contain: typedEntity.Sk in the context of item["sk"] assignment
            var readsFromEntityInstance = generatedCode.Contains("typedEntity.Sk");

            return (emitsConstantDirectly && !readsFromEntityInstance)
                .Label($"emitsConstant={emitsConstantDirectly}, readsInstance={readsFromEntityInstance}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 8: Serialization emits constant value directly**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// Property: For any constant sort key property with attribute name "sk" and constant value V
    /// (read-only auto-property syntax), the generated ToDynamoDb method SHALL emit
    /// item["sk"] = new AttributeValue { S = "V" } directly and SHALL NOT read the property
    /// value from the entity instance (typedEntity.Sk).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ReadOnlyAutoProperty_ToDynamoDb_EmitsConstantDirectly()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyReadOnlyAutoPropertySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify constant emission
            var expectedEmission = $"item[\"sk\"] = new AttributeValue {{ S = \"{escapedValue}\" }};";
            var emitsConstantDirectly = generatedCode.Contains(expectedEmission);

            // Verify no entity instance read for SK
            var readsFromEntityInstance = generatedCode.Contains("typedEntity.Sk");

            return (emitsConstantDirectly && !readsFromEntityInstance)
                .Label($"emitsConstant={emitsConstantDirectly}, readsInstance={readsFromEntityInstance}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 8: Serialization emits constant value directly**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// Property: For any constant partition key property with attribute name "pk" and constant value V
    /// (expression-body syntax), the generated ToDynamoDb method SHALL emit
    /// item["pk"] = new AttributeValue { S = "V" } directly and SHALL NOT read the property
    /// value from the entity instance (typedEntity.Pk).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_ExpressionBody_ToDynamoDb_EmitsConstantDirectly()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyExpressionBodySource(value, "pk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify constant emission
            var expectedEmission = $"item[\"pk\"] = new AttributeValue {{ S = \"{escapedValue}\" }};";
            var emitsConstantDirectly = generatedCode.Contains(expectedEmission);

            // Verify no entity instance read for PK
            var readsFromEntityInstance = generatedCode.Contains("typedEntity.Pk");

            return (emitsConstantDirectly && !readsFromEntityInstance)
                .Label($"emitsConstant={emitsConstantDirectly}, readsInstance={readsFromEntityInstance}. " +
                       $"Value='{value}', escaped='{escapedValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 8: Serialization emits constant value directly**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// Property: For any constant sort key property with no prefix configured, the generated
    /// ToDynamoDb method SHALL use the constant value as-is without applying prefix logic
    /// or KeyInputMode logic for that key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ToDynamoDb_NoPrefix_UsesValueAsIs()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            // Use entity with NO prefix configured on the constant sort key
            var source = GenerateConstantSortKeyExpressionBodySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // The emitted value should be exactly the constant value — no prefix wrapping
            var expectedEmission = $"item[\"sk\"] = new AttributeValue {{ S = \"{escapedValue}\" }};";
            var emitsExactValue = generatedCode.Contains(expectedEmission);

            // Should NOT contain any prefix application logic for the SK (e.g., resolvedMode)
            // In the SK assignment line context. The resolvedMode variable exists for PK but
            // must NOT be used to modify the constant SK value.
            var readsFromEntityInstance = generatedCode.Contains("typedEntity.Sk");

            return (emitsExactValue && !readsFromEntityInstance)
                .Label($"emitsExactValue={emitsExactValue}, readsFromEntityInstance={readsFromEntityInstance}. " +
                       $"Value='{value}'");
        });
    }

    #endregion

    #region Property 9: Deserialization validates constant key value

    /// <summary>
    /// **Feature: constant-key-detection, Property 9: Deserialization validates constant key value**
    /// **Validates: Requirements 7.1**
    /// 
    /// Property: For any constant sort key with expected value V (expression-body syntax),
    /// the generated FromDynamoDb method SHALL use ordinal string comparison to validate
    /// the incoming value and invoke LogWarning (with ConstantKeyValidationMismatch event ID)
    /// when the value differs, including both expected and actual values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ExpressionBody_FromDynamoDb_ValidatesAndLogsWarningOnMismatch()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify ordinal string comparison against expected constant value
            var hasOrdinalComparison = generatedCode.Contains(
                $"!string.Equals(skAttr.S, \"{escapedValue}\", StringComparison.Ordinal)");

            // Verify LogWarning invocation with ConstantKeyValidationMismatch event ID
            var hasLogWarningMismatch = generatedCode.Contains(
                "options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.ConstantKeyValidationMismatch,");

            // Verify the warning message contains expected value and actual value
            var hasExpectedAndActualInMessage = generatedCode.Contains(
                $"\"Expected constant key '{{AttributeName}}' = \\\"{{ExpectedValue}}\\\" but got \\\"{{ActualValue}}\\\"\"");

            // Verify the message args include the attribute name, expected value, and actual value
            var hasMessageArgs = generatedCode.Contains(
                $"\"sk\", \"{escapedValue}\", skAttr.S");

            return (hasOrdinalComparison && hasLogWarningMismatch && hasExpectedAndActualInMessage && hasMessageArgs)
                .Label($"ordinalComparison={hasOrdinalComparison}, logWarning={hasLogWarningMismatch}, " +
                       $"messageFormat={hasExpectedAndActualInMessage}, messageArgs={hasMessageArgs}. " +
                       $"Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 9: Deserialization validates constant key value**
    /// **Validates: Requirements 7.1**
    /// 
    /// Property: For any constant sort key with expected value V (read-only auto-property syntax),
    /// the generated FromDynamoDb method SHALL use ordinal string comparison to validate
    /// the incoming value and invoke LogWarning when the value differs.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ReadOnlyAutoProperty_FromDynamoDb_ValidatesAndLogsWarningOnMismatch()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyReadOnlyAutoPropertySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Verify ordinal string comparison against expected constant value
            var hasOrdinalComparison = generatedCode.Contains(
                $"!string.Equals(skAttr.S, \"{escapedValue}\", StringComparison.Ordinal)");

            // Verify LogWarning invocation with ConstantKeyValidationMismatch event ID
            var hasLogWarningMismatch = generatedCode.Contains(
                "options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.ConstantKeyValidationMismatch,");

            // Verify the warning message contains expected and actual values
            var hasExpectedAndActualInMessage = generatedCode.Contains(
                $"\"Expected constant key '{{AttributeName}}' = \\\"{{ExpectedValue}}\\\" but got \\\"{{ActualValue}}\\\"\"");

            return (hasOrdinalComparison && hasLogWarningMismatch && hasExpectedAndActualInMessage)
                .Label($"ordinalComparison={hasOrdinalComparison}, logWarning={hasLogWarningMismatch}, " +
                       $"messageFormat={hasExpectedAndActualInMessage}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 9: Deserialization validates constant key value**
    /// **Validates: Requirements 7.1**
    /// 
    /// Property: For any constant sort key (expression-body syntax), the generated FromDynamoDb
    /// method SHALL NOT assign the property value (no setter call) when deserializing.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ExpressionBody_FromDynamoDb_SkipsPropertyAssignment()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            // The generated deserialization code should NOT assign entity.Sk = ... 
            // since expression-body properties have no setter.
            // Look for absence of property assignment pattern for Sk in FromDynamoDb context
            var hasSkAssignment = generatedCode.Contains("entity.Sk =") ||
                                  generatedCode.Contains("entity.Sk=");

            return (!hasSkAssignment)
                .Label($"hasSkAssignment={hasSkAssignment}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 9: Deserialization validates constant key value**
    /// **Validates: Requirements 7.1**
    /// 
    /// Property: For any constant sort key, the generated FromDynamoDb method SHALL log a
    /// warning via LogWarning (with ConstantKeyAttributeMissing event ID) when the constant key
    /// attribute is entirely absent from the incoming DynamoDB item dictionary.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_FromDynamoDb_LogsWarningWhenAttributeMissing()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value, "sk");
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            // Verify else branch with LogWarning for missing attribute
            var hasElseBranch = generatedCode.Contains("else");

            // Verify LogWarning invocation with ConstantKeyAttributeMissing event ID
            var hasLogWarningMissing = generatedCode.Contains(
                "options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.ConstantKeyAttributeMissing,");

            // Verify the missing-attribute message includes the attribute name
            var hasMissingMessage = generatedCode.Contains(
                "\"Expected constant key attribute '{AttributeName}' was missing from item\"");

            // Verify the attribute name argument
            var hasAttributeNameArg = generatedCode.Contains("\"sk\"");

            return (hasElseBranch && hasLogWarningMissing && hasMissingMessage && hasAttributeNameArg)
                .Label($"elseBranch={hasElseBranch}, logWarningMissing={hasLogWarningMissing}, " +
                       $"missingMessage={hasMissingMessage}, attrNameArg={hasAttributeNameArg}. " +
                       $"Value='{value}'");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with a constant sort key using expression-body syntax
    /// and a regular variable partition key.
    /// </summary>
    private static string GenerateConstantSortKeyExpressionBodySource(string constantValue, string attributeName)
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
        [DynamoDbAttribute(""{attributeName}"")]
        public string Sk => ""{escapedValue}"";

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant sort key using read-only auto-property syntax
    /// and a regular variable partition key.
    /// </summary>
    private static string GenerateConstantSortKeyReadOnlyAutoPropertySource(string constantValue, string attributeName)
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
        [DynamoDbAttribute(""{attributeName}"")]
        public string Sk {{ get; }} = ""{escapedValue}"";

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant partition key using expression-body syntax.
    /// PK-only entity (no sort key).
    /// </summary>
    private static string GenerateConstantPartitionKeyExpressionBodySource(string constantValue, string attributeName)
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
        [DynamoDbAttribute(""{attributeName}"")]
        public string Pk => ""{escapedValue}"";

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Escapes a string the same way the source generator does for generated code output.
    /// </summary>
    private static string EscapeForGeneratedCode(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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
