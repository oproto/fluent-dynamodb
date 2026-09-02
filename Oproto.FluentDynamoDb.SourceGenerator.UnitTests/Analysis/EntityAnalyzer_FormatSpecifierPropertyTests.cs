using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for EntityAnalyzer format specifier handling.
/// 
/// **Feature: computed-field-format-specifiers**
/// - Property 3: Placeholder Count Extraction Correctness (Validates: Requirements 2.1, 2.4, 7.5)
/// - Property 5: Invalid Placeholder Index Detection (Validates: Requirements 2.5, 7.2)
/// 
/// These tests verify that ValidateComputedKeyFormat correctly counts placeholders even with
/// format specifiers, and emits diagnostics for invalid placeholder index portions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class EntityAnalyzer_FormatSpecifierPropertyTests
{
    private static readonly string[] SampleFormatSpecifiers =
    {
        "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00", "X8", "F3", "C2", "P1"
    };

    private static readonly string[] SampleSeparators =
    {
        "#", "_", "-", "|", "~", ".", "/", "@"
    };

    #region Property 3: Placeholder Count Extraction Correctness

    /// <summary>
    /// Test input combining all parameters for format string generation.
    /// </summary>
    private record FormatTestInput(int PlaceholderCount, int SpecifierMask, int SeparatorIndex, int SpecifierIndex);

    /// <summary>
    /// Generates a FormatTestInput with valid ranges for all parameters.
    /// </summary>
    private static Arbitrary<FormatTestInput> GenerateFormatTestInput(int maxPlaceholders = 5)
    {
        var gen = from placeholderCount in Gen.Choose(1, maxPlaceholders)
                  from specifierMask in Gen.Choose(0, 31)
                  from separatorIndex in Gen.Choose(0, SampleSeparators.Length - 1)
                  from specifierIndex in Gen.Choose(0, SampleFormatSpecifiers.Length - 1)
                  select new FormatTestInput(placeholderCount, specifierMask, separatorIndex, specifierIndex);

        return gen.ToArbitrary();
    }

    /// <summary>
    /// **Feature: computed-field-format-specifiers, Property 3: Placeholder Count Extraction Correctness**
    /// **Validates: Requirements 2.1, 2.4, 7.5**
    /// 
    /// Property: For any composite format string containing N sequential placeholders (0..N-1)
    /// with mixed format specifiers, ValidateComputedKeyFormat SHALL compute the placeholder count
    /// as N (= max(index) + 1) and SHALL NOT emit FDDB090 when source property count equals N.
    /// 
    /// This tests the match case: format string with N placeholders and N source properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaceholderCountExtraction_MatchingSourceProperties_NoDiagnostic()
    {
        return Prop.ForAll(GenerateFormatTestInput(), input =>
        {
            // Build a format string with input.PlaceholderCount sequential placeholders
            var separator = SampleSeparators[input.SeparatorIndex];
            var formatSpecifier = SampleFormatSpecifiers[input.SpecifierIndex];
            var formatString = BuildFormatString(input.PlaceholderCount, input.SpecifierMask, separator, formatSpecifier);

            // Generate entity source with matching source property count
            var source = GenerateEntitySource(formatString, input.PlaceholderCount);

            // Run through source generator
            var result = GenerateCode(source);

            // Should NOT emit FDDB090 because placeholder count matches source property count
            var hasFDDB090 = result.Diagnostics.Any(d => d.Id == "FDDB090");

            return (!hasFDDB090).Label(
                $"Format '{formatString}' with {input.PlaceholderCount} source properties should NOT emit FDDB090");
        });
    }

    /// <summary>
    /// **Feature: computed-field-format-specifiers, Property 3: Placeholder Count Extraction Correctness**
    /// **Validates: Requirements 2.1, 2.4, 7.5**
    /// 
    /// Property: For any composite format string containing N sequential placeholders (0..N-1)
    /// with mixed format specifiers, ValidateComputedKeyFormat SHALL emit FDDB090 when the
    /// source property count does not equal N.
    /// 
    /// This tests the mismatch case: format string with N placeholders but N+1 source properties.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlaceholderCountExtraction_MismatchedSourceProperties_EmitsFDDB090()
    {
        return Prop.ForAll(GenerateFormatTestInput(maxPlaceholders: 4), input =>
        {
            // Build a format string with input.PlaceholderCount sequential placeholders
            var separator = SampleSeparators[input.SeparatorIndex];
            var formatSpecifier = SampleFormatSpecifiers[input.SpecifierIndex];
            var formatString = BuildFormatString(input.PlaceholderCount, input.SpecifierMask, separator, formatSpecifier);

            // Generate entity source with ONE MORE source property than placeholders (mismatch)
            var sourcePropertyCount = input.PlaceholderCount + 1;
            var source = GenerateEntitySource(formatString, sourcePropertyCount);

            // Run through source generator
            var result = GenerateCode(source);

            // SHOULD emit FDDB090 because placeholder count doesn't match source property count
            var hasFDDB090 = result.Diagnostics.Any(d => d.Id == "FDDB090");

            return hasFDDB090.Label(
                $"Format '{formatString}' with {sourcePropertyCount} source properties (expected {input.PlaceholderCount}) should emit FDDB090");
        });
    }

    #endregion

    #region Property 5: Invalid Placeholder Index Detection

    /// <summary>
    /// **Feature: computed-field-format-specifiers, Property 5: Invalid Placeholder Index Detection**
    ///
    /// *For any* placeholder text where the portion before the first colon is an alphabetic string
    /// (e.g., {abc:format}, {name:D4}), the EntityAnalyzer SHALL emit diagnostic DYNDB036
    /// indicating an invalid placeholder format.
    ///
    /// **Validates: Requirements 2.5, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidPlaceholderIndex_AlphabeticIndex_EmitsDiagnostic()
    {
        var inputGen = from invalidIndex in InvalidAlphabeticIndexGen
                       from formatSpecifier in FormatSpecifierGen
                       select (invalidIndex, formatSpecifier);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (invalidIndex, formatSpecifier) = input;
                var formatString = $"{{{invalidIndex}:{formatSpecifier}}}";

                var source = GenerateEntitySource(formatString, sourcePropertyCount: 1);
                var result = GenerateCode(source);

                var hasDiagnostic = result.Diagnostics.Any(d => d.Id == "DYNDB036");

                return hasDiagnostic.ToProperty()
                    .Label($"Format='{formatString}', InvalidIndex='{invalidIndex}', " +
                           $"ExpectedDiagnostic=DYNDB036, Found={hasDiagnostic}");
            });
    }

    /// <summary>
    /// **Feature: computed-field-format-specifiers, Property 5: Invalid Placeholder Index Detection**
    ///
    /// *For any* placeholder with a negative integer index (e.g., {-1:format}, {-42:format}),
    /// the EntityAnalyzer SHALL emit diagnostic DYNDB036 indicating an invalid placeholder format
    /// because negative indices are not valid non-negative integers.
    ///
    /// **Validates: Requirements 2.5, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidPlaceholderIndex_NegativeIndex_EmitsDiagnostic()
    {
        var inputGen = from negativeIndex in NegativeIndexGen
                       from formatSpecifier in FormatSpecifierGen
                       select (negativeIndex, formatSpecifier);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (negativeIndex, formatSpecifier) = input;
                var formatString = $"{{{negativeIndex}:{formatSpecifier}}}";

                var source = GenerateEntitySource(formatString, sourcePropertyCount: 1);
                var result = GenerateCode(source);

                var hasDiagnostic = result.Diagnostics.Any(d => d.Id == "DYNDB036");

                return hasDiagnostic.ToProperty()
                    .Label($"Format='{formatString}', NegativeIndex='{negativeIndex}', " +
                           $"ExpectedDiagnostic=DYNDB036, Found={hasDiagnostic}");
            });
    }

    /// <summary>
    /// **Feature: computed-field-format-specifiers, Property 5: Invalid Placeholder Index Detection**
    ///
    /// *For any* placeholder with a mixed alphanumeric or special character index portion
    /// (e.g., {1.2:format}, {a0:format}, {0x1:format}), the EntityAnalyzer SHALL emit
    /// diagnostic DYNDB036 indicating an invalid placeholder format.
    ///
    /// **Validates: Requirements 2.5, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidPlaceholderIndex_MixedAlphanumericIndex_EmitsDiagnostic()
    {
        var inputGen = from specialIndex in MixedAlphanumericIndexGen
                       from formatSpecifier in FormatSpecifierGen
                       select (specialIndex, formatSpecifier);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (specialIndex, formatSpecifier) = input;
                var formatString = $"{{{specialIndex}:{formatSpecifier}}}";

                var source = GenerateEntitySource(formatString, sourcePropertyCount: 1);
                var result = GenerateCode(source);

                var hasDiagnostic = result.Diagnostics.Any(d => d.Id == "DYNDB036");

                return hasDiagnostic.ToProperty()
                    .Label($"Format='{formatString}', SpecialIndex='{specialIndex}', " +
                           $"ExpectedDiagnostic=DYNDB036, Found={hasDiagnostic}");
            });
    }

    #endregion

    #region Generators for Property 5

    /// <summary>
    /// Generates purely alphabetic strings to use as invalid placeholder indices.
    /// These are never valid non-negative integers.
    /// </summary>
    private static Gen<string> InvalidAlphabeticIndexGen =>
        Gen.Elements(
            "abc", "x", "DATE", "hello", "idx", "name", "key",
            "A", "zz", "test", "foo", "bar", "Index", "val",
            "one", "two", "three", "format", "type", "id",
            "XX", "ab", "cd", "ef", "gh", "ij", "kl", "mn",
            "op", "qr", "st", "uv", "wx", "yz", "AA", "BB",
            "CC", "DD", "EE", "FF", "GG", "HH", "II", "JJ",
            "KK", "LL", "MM", "NN", "OO", "PP", "QQ", "RR",
            "SS", "TT", "UU", "VV", "WW", "Abc", "Xyz", "FOO",
            "BAR", "BAZ", "qux", "quux", "corge", "grault",
            "garply", "waldo", "fred", "plugh", "xyzzy", "thud",
            "alpha", "beta", "gamma", "delta", "epsilon", "zeta",
            "eta", "theta", "iota", "kappa", "lambda", "mu",
            "nu", "xi", "omicron", "pi", "rho", "sigma", "tau",
            "upsilon", "phi", "chi", "psi", "omega", "prop",
            "attr", "field", "item", "value", "data", "src",
            "dst", "tmp", "obj", "ref", "ptr", "buf", "len",
            "max", "min", "sum", "avg", "cnt", "num", "str");

    /// <summary>
    /// Generates negative integer strings to use as invalid placeholder indices.
    /// Negative numbers are not valid non-negative integers.
    /// </summary>
    private static Gen<string> NegativeIndexGen =>
        Gen.Choose(-999, -1).Select(n => n.ToString());

    /// <summary>
    /// Generates mixed alphanumeric/special character strings that are not valid integers.
    /// These include decimal numbers, hex prefixes, leading/trailing characters, etc.
    /// Excludes control characters, characters that break C# string literal parsing,
    /// and whitespace-padded numbers (which int.TryParse accepts).
    /// </summary>
    private static Gen<string> MixedAlphanumericIndexGen =>
        Gen.Elements(
            "1.2", "a0", "0a", "1a", "a1", "0x1", "0b10",
            "1.0", "2.5", "3.14", "0xFF", "1_000",
            "1e2", "1e-5", "1E+3",
            "NaN", "Inf", "inf",
            "null", "true", "false",
            "++0", "--1", "0-", "1-",
            "1.", ".1", "0.", ".0",
            "a!", "0x0A", "1,0",
            "1+2", "2*3", "4/5", "6%7",
            "0.0", "00.1", "1..0", "1,,2",
            "abc123", "123abc", "12ab34",
            "0xFF", "0b101", "0o77");

    /// <summary>
    /// Generates valid format specifier strings for the placeholder portion after the colon.
    /// </summary>
    private static Gen<string> FormatSpecifierGen =>
        Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00",
            "dd/MM/yyyy", "F2", "X8", "C", "P0", "E2",
            "D", "d", "f", "g", "n", "r", "x", "X",
            "D2", "D6", "N0", "N4", "F0", "F4", "P2");

    #endregion

    #region Helper Methods

    /// <summary>
    /// Builds a format string with the given number of sequential placeholders.
    /// Uses specifierMask bits to determine which placeholders get format specifiers.
    /// </summary>
    private static string BuildFormatString(int placeholderCount, int specifierMask, string separator, string formatSpecifier)
    {
        var parts = new string[placeholderCount];
        for (int i = 0; i < placeholderCount; i++)
        {
            // Use bit i of specifierMask to decide if this placeholder gets a specifier
            bool hasSpecifier = (specifierMask & (1 << i)) != 0;
            parts[i] = hasSpecifier ? $"{{{i}:{formatSpecifier}}}" : $"{{{i}}}";
        }
        return string.Join(separator, parts);
    }

    /// <summary>
    /// Generates an entity source code string with a computed format and the specified number of source properties.
    /// </summary>
    private static string GenerateEntitySource(string formatString, int sourcePropertyCount)
    {
        // Generate source property names
        var sourcePropertyNames = Enumerable.Range(0, sourcePropertyCount)
            .Select(i => $"Prop{i}")
            .ToArray();

        // Build Computed attribute arguments: source property names + Format
        var computedArgs = string.Join(", ", sourcePropertyNames.Select(n => $"\"{n}\""));
        var escapedFormat = formatString.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // Build property declarations for each source property
        var propertyDeclarations = string.Join("\n", sourcePropertyNames.Select((name, i) =>
            $@"        [DynamoDbAttribute(""prop{i}"")]
        public string {name} {{ get; set; }} = string.Empty;"));

        return $@"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed({computedArgs}, Format = ""{escapedFormat}"")]
        public string Pk {{ get; set; }} = string.Empty;

{propertyDeclarations}
    }}
}}";
    }

    /// <summary>
    /// Runs the source generator on the given source and returns diagnostics and generated sources.
    /// </summary>
    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    #endregion
}
