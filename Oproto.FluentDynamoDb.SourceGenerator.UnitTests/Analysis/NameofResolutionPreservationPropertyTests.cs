using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property tests for string literal and integer literal behavior
/// in [Computed] and [Extracted] attributes.
///
/// These tests confirm that the EXISTING behavior of literal arguments is correct
/// and must be preserved after the nameof() fix is applied. They encode baseline
/// behavior observed on UNFIXED code.
///
/// Property 2: Preservation - For all inputs where the bug condition does NOT hold
/// (i.e., inputs that ARE LiteralExpressionSyntax), behavior must remain unchanged.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
/// </summary>
[Trait("Category", "Preservation")]
public class NameofResolutionPreservationPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 2.1: String literal positional args in [Computed] produce correct SourceProperties
    // **Validates: Requirements 3.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For all valid C# identifier strings used as string literal positional arguments
    /// in [Computed], the SourceProperties array contains those exact values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Computed_StringLiteralPositionalArgs_ProducesCorrectSourceProperties()
    {
        var genIdentifier = GenValidCSharpIdentifier();

        return Prop.ForAll(
            genIdentifier.ToArbitrary(),
            genIdentifier.ToArbitrary(),
            (prop1, prop2) =>
            {
                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{prop1}"", ""{prop2}"", Separator = ""#"")]
        public string Pk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""prop1"")]
        public string {prop1} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""prop2"")]
        public string {prop2} {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var pkProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Pk");
                if (pkProperty?.ComputedKey == null) return false;

                return pkProperty.ComputedKey.SourceProperties.Length == 2
                    && pkProperty.ComputedKey.SourceProperties[0] == prop1
                    && pkProperty.ComputedKey.SourceProperties[1] == prop2;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2.2: Integer literal index in [Extracted] produces correct Index
    // **Validates: Requirements 3.3**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For all non-negative integer literals used as the index argument in [Extracted],
    /// the Index property equals that value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Extracted_IntegerLiteralIndex_ProducesCorrectIndex()
    {
        var genIndex = Gen.Choose(0, 9);

        return Prop.ForAll(
            genIndex.ToArbitrary(),
            index =>
            {
                // Build source properties for the Computed attribute to have enough source properties
                var sourceProps = string.Join(", ", Enumerable.Range(0, index + 1).Select(i => $@"""Prop{i}"""));
                var propDecls = string.Join("\n        ", Enumerable.Range(0, index + 1).Select(i =>
                    $@"[DynamoDbAttribute(""prop{i}"")]
        public string Prop{i} {{ get; set; }} = string.Empty;"));

                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed({sourceProps}, Separator = ""#"")]
        public string Pk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""extracted"")]
        [Extracted(""Pk"", {index})]
        public string Extracted {{ get; set; }} = string.Empty;

        {propDecls}
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var extractedProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Extracted");
                if (extractedProperty?.ExtractedKey == null) return false;

                return extractedProperty.ExtractedKey.Index == index;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2.3: String literal first arg in [Extracted] produces correct SourceProperty
    // **Validates: Requirements 3.2**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For all valid string literals used as the first argument in [Extracted],
    /// the SourceProperty equals that value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Extracted_StringLiteralFirstArg_ProducesCorrectSourceProperty()
    {
        var genIdentifier = GenValidCSharpIdentifier();

        return Prop.ForAll(
            genIdentifier.ToArbitrary(),
            sourcePropertyName =>
            {
                var source = $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string {sourcePropertyName} {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""extracted"")]
        [Extracted(""{sourcePropertyName}"", 0)]
        public string Extracted {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month {{ get; set; }} = string.Empty;
    }}
}}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var extractedProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Extracted");
                if (extractedProperty?.ExtractedKey == null) return false;

                return extractedProperty.ExtractedKey.SourceProperty == sourcePropertyName;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2.4: Named arguments (Format, Separator) in [Computed] are preserved
    // **Validates: Requirements 3.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For [Computed] attributes with Format named argument, the Format value is correctly extracted.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Computed_FormatNamedArgument_IsPreserved()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""UserId"", Format = ""USER#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var pkProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Pk");
                if (pkProperty?.ComputedKey == null) return false;

                return pkProperty.ComputedKey.SourceProperties.Length == 1
                    && pkProperty.ComputedKey.SourceProperties[0] == "UserId"
                    && pkProperty.ComputedKey.Format == "USER#{0}";
            });
    }

    /// <summary>
    /// For [Computed] attributes with Separator named argument, the Separator value is correctly extracted.
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Computed_SeparatorNamedArgument_IsPreserved()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""A"", ""B"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        public string B { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var pkProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Pk");
                if (pkProperty?.ComputedKey == null) return false;

                return pkProperty.ComputedKey.SourceProperties.Length == 2
                    && pkProperty.ComputedKey.SourceProperties[0] == "A"
                    && pkProperty.ComputedKey.SourceProperties[1] == "B"
                    && pkProperty.ComputedKey.Separator == "#";
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates valid C# identifiers: start with a letter, followed by letters/digits/underscores.
    /// Length between 2 and 20 characters.
    /// </summary>
    private static Gen<string> GenValidCSharpIdentifier()
    {
        var genFirstChar = Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z');

        var genRestChar = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        return from first in genFirstChar
               from restLength in Gen.Choose(1, 7)
               from rest in Gen.ArrayOf(restLength, genRestChar)
               select first + new string(rest);
    }

    private static EntityModel? AnalyzeSource(string source)
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
            .First();

        var analyzer = new EntityAnalyzer();
        return analyzer.AnalyzeEntity(classDecl, semanticModel);
    }
}
