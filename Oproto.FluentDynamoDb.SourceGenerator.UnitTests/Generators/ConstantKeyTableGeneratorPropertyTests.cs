using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for TableGenerator convenience method simplification with constant keys.
/// 
/// **Feature: constant-key-detection**
/// - Property 7: Convenience methods omit constant key parameters (Validates: Requirements 5.1, 5.2, 5.3, 5.4)
/// 
/// These tests verify that when an entity has one constant key and one variable key,
/// the generated Get(), Delete(), and Update() convenience methods accept only the variable
/// key parameter and inject the constant key value internally via .WithKey().
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyTableGeneratorPropertyTests
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

    #region Property 7: Convenience methods omit constant key parameters — Constant Sort Key

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.1**
    /// 
    /// Property: For any entity with a constant sort key (value V) and a variable partition key,
    /// the generated Get() method SHALL accept only the partition key parameter and SHALL
    /// inject the constant sort key value internally via .WithKey("sk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_GetMethod_AcceptsOnlyPartitionKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Get() should accept a single string parameter (PK only)
            var hasGetWithSingleParam = generatedCode.Contains("GetItemRequestBuilder<TestEntity> Get(string pk)") ||
                                        generatedCode.Contains("GetItemRequestBuilder<TestEntity> Get(string pk,");

            // Get() should NOT accept TWO key parameters (pk AND sk)
            var hasGetWithTwoParams = generatedCode.Contains("Get(string pk, string sk)");

            // Get() should inject the constant SK value via .WithKey("sk", "VALUE")
            var injectsConstantSk = generatedCode.Contains($".WithKey(\"sk\", \"{escapedValue}\")");

            return (hasGetWithSingleParam && !hasGetWithTwoParams && injectsConstantSk)
                .Label($"hasGetSingleParam={hasGetWithSingleParam}, hasGetTwoParams={hasGetWithTwoParams}, " +
                       $"injectsConstantSk={injectsConstantSk}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.2**
    /// 
    /// Property: For any entity with a constant sort key (value V) and a variable partition key,
    /// the generated Delete() method SHALL accept only the partition key parameter and SHALL
    /// inject the constant sort key value internally via .WithKey("sk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_DeleteMethod_AcceptsOnlyPartitionKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Delete() should accept a single string parameter (PK only)
            var hasDeleteWithSingleParam = generatedCode.Contains("DeleteItemRequestBuilder<TestEntity> Delete(string pk)") ||
                                           generatedCode.Contains("DeleteItemRequestBuilder<TestEntity> Delete(string pk,");

            // Delete() should NOT accept TWO key parameters (pk AND sk)
            var hasDeleteWithTwoParams = generatedCode.Contains("Delete(string pk, string sk)");

            // Delete() should inject the constant SK value via .WithKey("sk", "VALUE")
            // Note: The Delete builder line uses the same .WithKey pattern
            var injectsConstantSk = generatedCode.Contains($"Delete<TestEntity>().WithKey(\"pk\", pk).WithKey(\"sk\", \"{escapedValue}\")");

            return (hasDeleteWithSingleParam && !hasDeleteWithTwoParams && injectsConstantSk)
                .Label($"hasDeleteSingleParam={hasDeleteWithSingleParam}, hasDeleteTwoParams={hasDeleteWithTwoParams}, " +
                       $"injectsConstantSk={injectsConstantSk}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.3**
    /// 
    /// Property: For any entity with a constant sort key (value V) and a variable partition key,
    /// the generated Update() method SHALL accept only the partition key parameter and SHALL
    /// inject the constant sort key value internally via .WithKey("sk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_UpdateMethod_AcceptsOnlyPartitionKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Update() should accept a single string parameter (PK only) + optional KeyCondition
            var hasUpdateWithSingleParam = generatedCode.Contains("Update(string pk, KeyCondition keyCondition") ||
                                           generatedCode.Contains("Update(string pk,");

            // Update() should NOT accept TWO key parameters (pk AND sk)
            var hasUpdateWithTwoParams = generatedCode.Contains("Update(string pk, string sk");

            // Update() should inject the constant SK value via .WithKey("sk", "VALUE")
            var injectsConstantSk = generatedCode.Contains($"WithKey(\"pk\", pk).WithKey(\"sk\", \"{escapedValue}\")") ||
                                    generatedCode.Contains($".WithKey(\"sk\", \"{escapedValue}\")");

            return (hasUpdateWithSingleParam && !hasUpdateWithTwoParams && injectsConstantSk)
                .Label($"hasUpdateSingleParam={hasUpdateWithSingleParam}, hasUpdateTwoParams={hasUpdateWithTwoParams}, " +
                       $"injectsConstantSk={injectsConstantSk}. Value='{value}'");
        });
    }

    #endregion

    #region Property 7: Convenience methods omit constant key parameters — Constant Partition Key

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.4**
    /// 
    /// Property: For any entity with a constant partition key (value V) and a variable sort key,
    /// the generated Get() method SHALL accept only the sort key parameter and SHALL
    /// inject the constant partition key value internally via .WithKey("pk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_GetMethod_AcceptsOnlySortKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Get() should accept a single string parameter (SK only)
            var hasGetWithSingleParam = generatedCode.Contains("GetItemRequestBuilder<TestEntity> Get(string sk)") ||
                                        generatedCode.Contains("GetItemRequestBuilder<TestEntity> Get(string sk,");

            // Get() should NOT accept TWO key parameters
            var hasGetWithTwoParams = generatedCode.Contains("Get(string pk, string sk)");

            // Get() should inject the constant PK value via .WithKey("pk", "VALUE")
            var injectsConstantPk = generatedCode.Contains($".WithKey(\"pk\", \"{escapedValue}\")");

            return (hasGetWithSingleParam && !hasGetWithTwoParams && injectsConstantPk)
                .Label($"hasGetSingleParam={hasGetWithSingleParam}, hasGetTwoParams={hasGetWithTwoParams}, " +
                       $"injectsConstantPk={injectsConstantPk}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.4**
    /// 
    /// Property: For any entity with a constant partition key (value V) and a variable sort key,
    /// the generated Delete() method SHALL accept only the sort key parameter and SHALL
    /// inject the constant partition key value internally via .WithKey("pk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_DeleteMethod_AcceptsOnlySortKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Delete() should accept a single string parameter (SK only)
            var hasDeleteWithSingleParam = generatedCode.Contains("DeleteItemRequestBuilder<TestEntity> Delete(string sk)") ||
                                           generatedCode.Contains("DeleteItemRequestBuilder<TestEntity> Delete(string sk,");

            // Delete() should NOT accept TWO key parameters
            var hasDeleteWithTwoParams = generatedCode.Contains("Delete(string pk, string sk)");

            // Delete() should inject the constant PK value via .WithKey("pk", "VALUE")
            var injectsConstantPk = generatedCode.Contains($"Delete<TestEntity>().WithKey(\"pk\", \"{escapedValue}\").WithKey(\"sk\", sk)");

            return (hasDeleteWithSingleParam && !hasDeleteWithTwoParams && injectsConstantPk)
                .Label($"hasDeleteSingleParam={hasDeleteWithSingleParam}, hasDeleteTwoParams={hasDeleteWithTwoParams}, " +
                       $"injectsConstantPk={injectsConstantPk}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 7: Convenience methods omit constant key parameters**
    /// **Validates: Requirements 5.4**
    /// 
    /// Property: For any entity with a constant partition key (value V) and a variable sort key,
    /// the generated Update() method SHALL accept only the sort key parameter and SHALL
    /// inject the constant partition key value internally via .WithKey("pk", "V").
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_UpdateMethod_AcceptsOnlySortKeyParameter()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyEntitySource(value);
            var result = RunSourceGenerator(source);

            if (result.HasErrors)
                return false.Label($"Generator produced errors: {result.ErrorMessages}");

            var generatedCode = result.EntityGeneratedCode;
            if (generatedCode == null)
                return false.Label("No generated code found for TestEntity");

            var escapedValue = EscapeForGeneratedCode(value);

            // Update() should accept a single string parameter (SK only) + optional KeyCondition
            var hasUpdateWithSingleParam = generatedCode.Contains("Update(string sk, KeyCondition keyCondition") ||
                                           generatedCode.Contains("Update(string sk,");

            // Update() should NOT accept TWO key parameters
            var hasUpdateWithTwoParams = generatedCode.Contains("Update(string pk, string sk");

            // Update() should inject the constant PK value via .WithKey("pk", "VALUE")
            var injectsConstantPk = generatedCode.Contains($"WithKey(\"pk\", \"{escapedValue}\").WithKey(\"sk\", sk)") ||
                                    generatedCode.Contains($".WithKey(\"pk\", \"{escapedValue}\")");

            return (hasUpdateWithSingleParam && !hasUpdateWithTwoParams && injectsConstantPk)
                .Label($"hasUpdateSingleParam={hasUpdateWithSingleParam}, hasUpdateTwoParams={hasUpdateWithTwoParams}, " +
                       $"injectsConstantPk={injectsConstantPk}. Value='{value}'");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with a variable partition key and a constant sort key
    /// (expression-body syntax). This is the most common pattern: entity with fixed SK type.
    /// </summary>
    private static string GenerateConstantSortKeyEntitySource(string constantValue)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"", IsDefault = true)]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""{escapedValue}"";

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant partition key (expression-body syntax) and a
    /// variable sort key. This tests the inverse scenario.
    /// </summary>
    private static string GenerateConstantPartitionKeyEntitySource(string constantValue)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"", IsDefault = true)]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk => ""{escapedValue}"";

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

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
    /// Collects BOTH the entity generated code (TestEntity.g.cs) AND the table generated code
    /// (TestTableTable.g.cs) since convenience methods live in the table class.
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

        // Collect all generated trees (skip the original source)
        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Length)
            .ToArray();

        // The table class file contains the accessor with Get/Delete/Update methods
        var tableTree = generatedTrees.FirstOrDefault(t => t.FilePath.Contains("Table.g.cs"));
        var entityTree = generatedTrees.FirstOrDefault(t => t.FilePath.Contains("TestEntity.g.cs"));

        // Combine all generated code for searching convenience methods
        var allGeneratedCode = string.Join("\n",
            generatedTrees.Select(t => t.GetText().ToString()));

        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        return new GeneratorResult
        {
            EntityGeneratedCode = allGeneratedCode,
            TableGeneratedCode = tableTree?.GetText().ToString(),
            HasErrors = errors.Length > 0,
            ErrorMessages = string.Join("; ", errors.Select(e => e.GetMessage())),
            GeneratedFiles = generatedTrees.Select(t => t.FilePath).ToArray()
        };
    }

    private class GeneratorResult
    {
        public string? EntityGeneratedCode { get; set; }
        public string? TableGeneratedCode { get; set; }
        public bool HasErrors { get; set; }
        public string ErrorMessages { get; set; } = string.Empty;
        public string[] GeneratedFiles { get; set; } = [];
    }

    #endregion
}
