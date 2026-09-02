using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for constant key detection in EntityAnalyzer.
/// 
/// **Feature: constant-key-detection**
/// - Property 1: Expression-body constant key detection (Validates: Requirements 1.1)
/// - Property 2: Read-only auto-property constant key detection (Validates: Requirements 2.1)
/// - Property 3: Set/init accessor prevents constant key detection (Validates: Requirements 2.3)
/// 
/// These tests verify that the EntityAnalyzer correctly detects constant key values
/// from expression-body and read-only auto-property syntax, and rejects properties
/// with set/init accessors.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ConstantKeyDetectionPropertyTests
{
    /// <summary>
    /// Generates non-empty strings that are valid C# string literal content.
    /// Avoids characters that would break string literal parsing (quotes, backslashes, newlines).
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

    /// <summary>
    /// Generates which key attribute to use: PartitionKey or SortKey.
    /// </summary>
    private static Arbitrary<bool> KeyTypeArb() => Arb.From<bool>();

    #region Property 1: Expression-body constant key detection

    /// <summary>
    /// **Feature: constant-key-detection, Property 1: Expression-body constant key detection**
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: For any non-empty string literal S used as the return expression in an
    /// expression-body property marked with [PartitionKey], the EntityAnalyzer SHALL set
    /// PropertyModel.ConstantKeyValue to exactly S.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpressionBody_WithPartitionKey_DetectsConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodySource(value, isPartitionKey: true);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsPartitionKey);
            if (keyProperty == null)
                return false.Label($"No partition key property found for value '{value}'");

            return (keyProperty.ConstantKeyValue == value)
                .Label($"Expected ConstantKeyValue='{value}', got '{keyProperty.ConstantKeyValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 1: Expression-body constant key detection**
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: For any non-empty string literal S used as the return expression in an
    /// expression-body property marked with [SortKey], the EntityAnalyzer SHALL set
    /// PropertyModel.ConstantKeyValue to exactly S.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpressionBody_WithSortKey_DetectsConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodySource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            return (keyProperty.ConstantKeyValue == value)
                .Label($"Expected ConstantKeyValue='{value}', got '{keyProperty.ConstantKeyValue}'");
        });
    }

    #endregion

    #region Property 2: Read-only auto-property constant key detection

    /// <summary>
    /// **Feature: constant-key-detection, Property 2: Read-only auto-property constant key detection**
    /// **Validates: Requirements 2.1**
    /// 
    /// Property: For any non-empty string literal S used as the initializer of a get-only
    /// auto-property (no set/init accessor) marked with [PartitionKey] or [SortKey],
    /// the EntityAnalyzer SHALL set PropertyModel.ConstantKeyValue to exactly S.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAutoProperty_WithPartitionKey_DetectsConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertySource(value, isPartitionKey: true);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsPartitionKey);
            if (keyProperty == null)
                return false.Label($"No partition key property found for value '{value}'");

            return (keyProperty.ConstantKeyValue == value)
                .Label($"Expected ConstantKeyValue='{value}', got '{keyProperty.ConstantKeyValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 2: Read-only auto-property constant key detection**
    /// **Validates: Requirements 2.1**
    /// 
    /// Property: For any non-empty string literal S used as the initializer of a get-only
    /// auto-property (no set/init accessor) marked with [SortKey],
    /// the EntityAnalyzer SHALL set PropertyModel.ConstantKeyValue to exactly S.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAutoProperty_WithSortKey_DetectsConstantKeyValue()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertySource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            return (keyProperty.ConstantKeyValue == value)
                .Label($"Expected ConstantKeyValue='{value}', got '{keyProperty.ConstantKeyValue}'");
        });
    }

    #endregion

    #region Property 3: Set/init accessor prevents constant key detection

    /// <summary>
    /// **Feature: constant-key-detection, Property 3: Set/init accessor prevents constant key detection**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: For any property marked with [PartitionKey] or [SortKey] that has a set
    /// accessor, regardless of initializer value, PropertyModel.ConstantKeyValue SHALL remain null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SetAccessor_PreventsConstantKeyDetection_PartitionKey()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateSetAccessorSource(value, isPartitionKey: true);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsPartitionKey);
            if (keyProperty == null)
                return false.Label($"No partition key property found for value '{value}'");

            // ConstantKeyValue MUST remain null for properties with set accessor
            return (keyProperty.ConstantKeyValue == null)
                .Label($"Expected ConstantKeyValue=null for set accessor, got '{keyProperty.ConstantKeyValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 3: Set/init accessor prevents constant key detection**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: For any property marked with [SortKey] that has a set
    /// accessor, regardless of initializer value, PropertyModel.ConstantKeyValue SHALL remain null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SetAccessor_PreventsConstantKeyDetection_SortKey()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateSetAccessorSource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            // ConstantKeyValue MUST remain null for properties with set accessor
            return (keyProperty.ConstantKeyValue == null)
                .Label($"Expected ConstantKeyValue=null for set accessor, got '{keyProperty.ConstantKeyValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 3: Set/init accessor prevents constant key detection**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: For any property marked with [PartitionKey] that has an init
    /// accessor, regardless of initializer value, PropertyModel.ConstantKeyValue SHALL remain null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InitAccessor_PreventsConstantKeyDetection_PartitionKey()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateInitAccessorSource(value, isPartitionKey: true);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsPartitionKey);
            if (keyProperty == null)
                return false.Label($"No partition key property found for value '{value}'");

            // ConstantKeyValue MUST remain null for properties with init accessor
            return (keyProperty.ConstantKeyValue == null)
                .Label($"Expected ConstantKeyValue=null for init accessor, got '{keyProperty.ConstantKeyValue}'");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 3: Set/init accessor prevents constant key detection**
    /// **Validates: Requirements 2.3**
    /// 
    /// Property: For any property marked with [SortKey] that has an init
    /// accessor, regardless of initializer value, PropertyModel.ConstantKeyValue SHALL remain null.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InitAccessor_PreventsConstantKeyDetection_SortKey()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateInitAccessorSource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            var keyProperty = result.Properties.FirstOrDefault(p => p.IsSortKey);
            if (keyProperty == null)
                return false.Label($"No sort key property found for value '{value}'");

            // ConstantKeyValue MUST remain null for properties with init accessor
            return (keyProperty.ConstantKeyValue == null)
                .Label($"Expected ConstantKeyValue=null for init accessor, got '{keyProperty.ConstantKeyValue}'");
        });
    }

    #endregion

    #region Property 4: Discriminator derivation produces ExactMatch

    /// <summary>
    /// **Feature: constant-key-detection, Property 4: Discriminator derivation produces ExactMatch**
    /// **Validates: Requirements 3.1**
    /// 
    /// Property: For any detected constant key value V (non-null, non-whitespace), the auto-derived
    /// DiscriminatorConfig SHALL have Strategy == ExactMatch, ExactValue == V, and IsAutoDerived == true.
    /// Tests with expression-body sort key (sort key preferred as primary discriminator).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiscriminatorDerivation_ExpressionBodySortKey_ProducesExactMatch()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodySource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            if (result.Discriminator == null)
                return false.Label($"Discriminator is null for constant key value '{value}'");

            var strategyOk = result.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch;
            var exactValueOk = result.Discriminator.ExactValue == value;
            var isAutoDerivedOk = result.Discriminator.IsAutoDerived;

            return (strategyOk && exactValueOk && isAutoDerivedOk)
                .Label($"Expected Strategy=ExactMatch (got {result.Discriminator.Strategy}), " +
                       $"ExactValue='{value}' (got '{result.Discriminator.ExactValue}'), " +
                       $"IsAutoDerived=true (got {result.Discriminator.IsAutoDerived})");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 4: Discriminator derivation produces ExactMatch**
    /// **Validates: Requirements 3.1**
    /// 
    /// Property: For any detected constant key value V (non-null, non-whitespace), the auto-derived
    /// DiscriminatorConfig SHALL have Strategy == ExactMatch, ExactValue == V, and IsAutoDerived == true.
    /// Tests with expression-body partition key (when no sort key pattern exists, falls back to PK).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiscriminatorDerivation_ExpressionBodyPartitionKey_ProducesExactMatch()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateExpressionBodySource(value, isPartitionKey: true);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            if (result.Discriminator == null)
                return false.Label($"Discriminator is null for constant key value '{value}'");

            var strategyOk = result.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch;
            var exactValueOk = result.Discriminator.ExactValue == value;
            var isAutoDerivedOk = result.Discriminator.IsAutoDerived;

            return (strategyOk && exactValueOk && isAutoDerivedOk)
                .Label($"Expected Strategy=ExactMatch (got {result.Discriminator.Strategy}), " +
                       $"ExactValue='{value}' (got '{result.Discriminator.ExactValue}'), " +
                       $"IsAutoDerived=true (got {result.Discriminator.IsAutoDerived})");
        });
    }

    /// <summary>
    /// **Feature: constant-key-detection, Property 4: Discriminator derivation produces ExactMatch**
    /// **Validates: Requirements 3.1**
    /// 
    /// Property: For any detected constant key value V (non-null, non-whitespace), the auto-derived
    /// DiscriminatorConfig SHALL have Strategy == ExactMatch, ExactValue == V, and IsAutoDerived == true.
    /// Tests with read-only auto-property sort key to verify both syntax forms produce the same result.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiscriminatorDerivation_ReadOnlyAutoPropertySortKey_ProducesExactMatch()
    {
        return Prop.ForAll(ValidStringLiteralArb(), value =>
        {
            var source = GenerateReadOnlyAutoPropertySource(value, isPartitionKey: false);

            var (classDecl, semanticModel) = ParseSource(source);
            var analyzer = new EntityAnalyzer();
            var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

            if (result == null)
                return false.Label($"AnalyzeEntity returned null for value '{value}'");

            if (result.Discriminator == null)
                return false.Label($"Discriminator is null for constant key value '{value}'");

            var strategyOk = result.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch;
            var exactValueOk = result.Discriminator.ExactValue == value;
            var isAutoDerivedOk = result.Discriminator.IsAutoDerived;

            return (strategyOk && exactValueOk && isAutoDerivedOk)
                .Label($"Expected Strategy=ExactMatch (got {result.Discriminator.Strategy}), " +
                       $"ExactValue='{value}' (got '{result.Discriminator.ExactValue}'), " +
                       $"IsAutoDerived=true (got {result.Discriminator.IsAutoDerived})");
        });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates source code with an expression-body property constant key.
    /// When isPartitionKey is true, the constant is on the partition key.
    /// When isPartitionKey is false, the constant is on the sort key (with a regular PK provided).
    /// </summary>
    private static string GenerateExpressionBodySource(string constantValue, bool isPartitionKey)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (isPartitionKey)
        {
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

        // Sort key case: need a regular partition key plus expression-body constant sort key
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
    /// Generates source code with a read-only auto-property (get-only, no set/init) constant key.
    /// When isPartitionKey is true, the constant is on the partition key.
    /// When isPartitionKey is false, the constant is on the sort key (with a regular PK provided).
    /// </summary>
    private static string GenerateReadOnlyAutoPropertySource(string constantValue, bool isPartitionKey)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (isPartitionKey)
        {
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

        // Sort key case: need a regular partition key plus constant sort key
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
    /// Generates source code with a key property that has { get; set; } and a string initializer.
    /// This should NOT be detected as a constant key.
    /// </summary>
    private static string GenerateSetAccessorSource(string constantValue, bool isPartitionKey)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (isPartitionKey)
        {
            return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = ""{escapedValue}"";
    }}
}}";
        }

        // Sort key case: need a regular partition key plus sort key with set accessor
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
    /// Generates source code with a key property that has { get; init; } and a string initializer.
    /// This should NOT be detected as a constant key.
    /// </summary>
    private static string GenerateInitAccessorSource(string constantValue, bool isPartitionKey)
    {
        var escapedValue = constantValue.Replace("\\", "\\\\").Replace("\"", "\\\"");

        if (isPartitionKey)
        {
            return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; init; }} = ""{escapedValue}"";
    }}
}}";
        }

        // Sort key case: need a regular partition key plus sort key with init accessor
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
        public string Sk {{ get; init; }} = ""{escapedValue}"";
    }}
}}";
    }

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

    #endregion
}
