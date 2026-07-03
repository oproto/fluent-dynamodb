using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for update model exclusion of constant key properties.
/// 
/// **Feature: constant-key-detection**
/// - Property 10: Update model excludes constant key properties (Validates: Requirements 8.1, 8.2, 8.3)
/// 
/// These tests verify that the source generator excludes constant key properties from
/// generated update model classes, regardless of whether the property uses expression-body
/// or read-only auto-property syntax.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyUpdateModelPropertyTests
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

    #region Property 10: Update model excludes constant key properties

    /// <summary>
    /// **Feature: constant-key-detection, Property 10: Update model excludes constant key properties**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// 
    /// Property: For any constant sort key property using expression-body syntax with value V,
    /// the generated update model class SHALL NOT include that property as a settable property.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ExpressionBody_ExcludedFromUpdateModel()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyExpressionBodySource(value);
            var (updateModelCode, entityCode) = RunSourceGenerator(source);

            if (entityCode == null)
                return false.Label($"No entity generated code found. Value='{value}'");

            if (updateModelCode == null)
                return false.Label($"No update model generated code found. Value='{value}'");

            // The constant sort key property (Sk) must NOT appear in the update model
            var skInUpdateModel = updateModelCode.Contains("Sk { get; set; }");

            // Non-key, non-constant properties (Name) MUST appear in the update model
            var nameInUpdateModel = updateModelCode.Contains("Name { get; set; }");

            return (!skInUpdateModel && nameInUpdateModel)
                .Label($"skInUpdateModel={skInUpdateModel}, nameInUpdateModel={nameInUpdateModel}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 10: Update model excludes constant key properties**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// 
    /// Property: For any constant sort key property using read-only auto-property syntax with value V,
    /// the generated update model class SHALL NOT include that property as a settable property.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_ReadOnlyAutoProperty_ExcludedFromUpdateModel()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantSortKeyReadOnlyAutoPropertySource(value);
            var (updateModelCode, entityCode) = RunSourceGenerator(source);

            if (entityCode == null)
                return false.Label($"No entity generated code found. Value='{value}'");

            if (updateModelCode == null)
                return false.Label($"No update model generated code found. Value='{value}'");

            // The constant sort key property (Sk) must NOT appear in the update model
            var skInUpdateModel = updateModelCode.Contains("Sk { get; set; }");

            // Non-key, non-constant properties (Name) MUST appear in the update model
            var nameInUpdateModel = updateModelCode.Contains("Name { get; set; }");

            return (!skInUpdateModel && nameInUpdateModel)
                .Label($"skInUpdateModel={skInUpdateModel}, nameInUpdateModel={nameInUpdateModel}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 10: Update model excludes constant key properties**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// 
    /// Property: For any constant partition key property using expression-body syntax with value V
    /// (PK-only entity), the generated update model class SHALL NOT include that property.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantPartitionKey_ExpressionBody_ExcludedFromUpdateModel()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateConstantPartitionKeyExpressionBodySource(value);
            var (updateModelCode, entityCode) = RunSourceGenerator(source);

            if (entityCode == null)
                return false.Label($"No entity generated code found. Value='{value}'");

            if (updateModelCode == null)
                return false.Label($"No update model generated code found. Value='{value}'");

            // The constant partition key property (Pk) must NOT appear in the update model
            var pkInUpdateModel = updateModelCode.Contains("Pk { get; set; }");

            // Non-key, non-constant properties (Name) MUST appear in the update model
            var nameInUpdateModel = updateModelCode.Contains("Name { get; set; }");

            return (!pkInUpdateModel && nameInUpdateModel)
                .Label($"pkInUpdateModel={pkInUpdateModel}, nameInUpdateModel={nameInUpdateModel}. Value='{value}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 10: Update model excludes constant key properties**
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// 
    /// Property: For any entity with a constant sort key (expression-body) and a variable partition
    /// key, the generated update model class SHALL NOT include the constant sort key property
    /// but SHALL include non-key properties. The exclusion is independent of existing key
    /// exclusion logic (belt-and-suspenders).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConstantSortKey_WithVariablePk_OnlyConstantKeyExcluded()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateCompositeConstantSortKeyWithMultiplePropsSource(value);
            var (updateModelCode, entityCode) = RunSourceGenerator(source);

            if (entityCode == null)
                return false.Label($"No entity generated code found. Value='{value}'");

            if (updateModelCode == null)
                return false.Label($"No update model generated code found. Value='{value}'");

            // The constant sort key property (Sk) must NOT appear in the update model
            var skInUpdateModel = updateModelCode.Contains("Sk { get; set; }");

            // The variable partition key (Pk) also must NOT appear (it's a key, excluded by standard key logic)
            var pkInUpdateModel = updateModelCode.Contains("Pk { get; set; }");

            // Non-key properties MUST appear in the update model
            var nameInUpdateModel = updateModelCode.Contains("Name { get; set; }");
            var emailInUpdateModel = updateModelCode.Contains("Email { get; set; }");

            return (!skInUpdateModel && !pkInUpdateModel && nameInUpdateModel && emailInUpdateModel)
                .Label($"skInUpdateModel={skInUpdateModel}, pkInUpdateModel={pkInUpdateModel}, " +
                       $"nameInUpdateModel={nameInUpdateModel}, emailInUpdateModel={emailInUpdateModel}. Value='{value}'");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with a constant sort key using expression-body syntax,
    /// a regular variable partition key, and a non-key property.
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

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant sort key using read-only auto-property syntax,
    /// a regular variable partition key, and a non-key property.
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

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a constant partition key using expression-body syntax.
    /// PK-only entity with a non-key property.
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

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Generates source code with a variable partition key, a constant sort key (expression-body),
    /// and multiple non-key properties to verify selective exclusion.
    /// </summary>
    private static string GenerateCompositeConstantSortKeyWithMultiplePropsSource(string constantValue)
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

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""email"")]
        public string Email {{ get; set; }} = string.Empty;
    }}
}}";
    }

    /// <summary>
    /// Runs the full source generator pipeline on the provided source and returns both
    /// the update model code and the main entity generated code.
    /// </summary>
    private static (string? UpdateModelCode, string? EntityCode) RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Length)
            .ToArray();

        // Find update model generated code
        var updateModelTree = generatedTrees
            .FirstOrDefault(t => t.FilePath.Contains("UpdateModel"));

        // Find the main entity generated code
        var entityTree = generatedTrees
            .FirstOrDefault(t => t.FilePath.Contains("TestEntity.g.cs"));

        return (updateModelTree?.GetText().ToString(), entityTree?.GetText().ToString());
    }

    #endregion
}
