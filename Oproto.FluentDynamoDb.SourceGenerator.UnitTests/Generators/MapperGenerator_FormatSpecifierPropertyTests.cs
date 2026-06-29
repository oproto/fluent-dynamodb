using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for MapperGenerator.ComputeFormatString source property Format injection.
///
/// Feature: computed-field-format-specifiers, Property 10: Source Property Format Injection
/// </summary>
[Trait("Feature", "computed-field-format-specifiers")]
[Trait("Property", "10")]
public class MapperGenerator_FormatSpecifierPropertyTests
{
    /// <summary>
    /// Generates a random format specifier string (e.g., "yyyy-MM-dd", "D4", "G", "HH:mm:ss").
    /// </summary>
    private static Gen<string> FormatSpecifierGen =>
        Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00",
            "dd/MM/yyyy", "F2", "X8", "C", "P0", "E2");

    /// <summary>
    /// Generates a separator string used between placeholders.
    /// </summary>
    private static Gen<string> SeparatorGen =>
        Gen.Elements("#", "-", "_", "|");

    /// <summary>
    /// Generates a source property with a random name, type, and optional Format.
    /// </summary>
    private static Gen<PropertyModel> SourcePropertyGen(int index, bool hasFormat) =>
        from format in FormatSpecifierGen
        select new PropertyModel
        {
            PropertyName = $"Prop{index}",
            PropertyType = "string",
            Format = hasFormat ? format : null
        };

    /// <summary>
    /// Generates a list of 1-4 source properties, each randomly having a Format or null.
    /// Returns the properties along with their format presence flags.
    /// </summary>
    private static Gen<(PropertyModel[] Properties, bool[] HasFormat)> SourcePropertiesGen =>
        from count in Gen.Choose(1, 4)
        from hasFormats in Gen.ListOf(count, Arb.Default.Bool().Generator)
        from properties in Gen.Sequence(
            hasFormats.Select((hasFormat, i) => SourcePropertyGen(i, hasFormat)))
        select (properties.ToArray(), hasFormats.ToArray());

    /// <summary>
    /// **Validates: Requirements 6.1, 6.2, 6.3, 6.6**
    ///
    /// Property 10: Source Property Format Injection
    ///
    /// For any computed format string without explicit format specifiers (Format=null)
    /// where a source property at index I has a non-null, non-empty Format value,
    /// ComputeFormatString SHALL inject that Format into the placeholder at index I,
    /// producing {I:format}. Source properties without Format leave placeholders as {I}.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_InjectsSourcePropertyFormats_WhenNoExplicitFormat()
    {
        var inputGen = from sourceData in SourcePropertiesGen
                       from separator in SeparatorGen
                       select (sourceData.Properties, sourceData.HasFormat, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (sourceProperties, hasFormats, separator) = input;

                // Create a ComputedKeyModel without explicit Format
                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = null,
                    Separator = separator
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // Verify each placeholder
                var allCorrect = true;
                for (int i = 0; i < sourceProperties.Length; i++)
                {
                    var sourceFormat = sourceProperties[i].Format;
                    if (!string.IsNullOrEmpty(sourceFormat))
                    {
                        // Should contain {i:format}
                        var expectedPlaceholder = $"{{{i}:{sourceFormat}}}";
                        if (!result.Contains(expectedPlaceholder))
                        {
                            allCorrect = false;
                            break;
                        }
                    }
                    else
                    {
                        // Should contain {i} (simple placeholder)
                        var expectedPlaceholder = $"{{{i}}}";
                        if (!result.Contains(expectedPlaceholder))
                        {
                            allCorrect = false;
                            break;
                        }
                    }
                }

                return allCorrect.ToProperty()
                    .Label($"Separator='{separator}', Result='{result}', Props=[{string.Join(", ", sourceProperties.Select(p => p.Format ?? "null"))}]");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.2, 6.3, 6.6**
    ///
    /// Property 10 (complement): When ComputedKeyModel HAS explicit Format,
    /// ComputeFormatString returns it unchanged regardless of source property Formats.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_ReturnsExplicitFormat_Unchanged_RegardlessOfSourceFormats()
    {
        var explicitFormatGen = from count in Gen.Choose(1, 4)
                                from specifiers in Gen.ListOf(count, Gen.OneOf(
                                    FormatSpecifierGen.Select(s => (string?)s),
                                    Gen.Constant((string?)null)))
                                select BuildExplicitFormat(specifiers.ToArray(), count);

        var inputGen = from explicitFormat in explicitFormatGen
                       from sourceData in SourcePropertiesGen
                       select (explicitFormat, sourceData.Properties);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (explicitFormat, sourceProperties) = input;

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = explicitFormat,
                    Separator = "#"
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // The result should be the explicit format unchanged
                return (result == explicitFormat).ToProperty()
                    .Label($"ExplicitFormat='{explicitFormat}', Result='{result}', Expected unchanged");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.6**
    ///
    /// The result format string uses the correct separator between placeholders.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_UsesSeparatorBetweenPlaceholders()
    {
        var inputGen = from sourceData in SourcePropertiesGen
                       from separator in SeparatorGen
                       select (sourceData.Properties, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (sourceProperties, separator) = input;

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = null,
                    Separator = separator
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // Build expected format string manually
                var expectedParts = new string[sourceProperties.Length];
                for (int i = 0; i < sourceProperties.Length; i++)
                {
                    var sourceFormat = sourceProperties[i].Format;
                    expectedParts[i] = !string.IsNullOrEmpty(sourceFormat)
                        ? $"{{{i}:{sourceFormat}}}"
                        : $"{{{i}}}";
                }
                var expected = string.Join(separator, expectedParts);

                return (result == expected).ToProperty()
                    .Label($"Separator='{separator}', Expected='{expected}', Actual='{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.6**
    ///
    /// When source property Format is empty string, it is treated as null (no injection).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_TreatsEmptyStringFormat_AsNull()
    {
        var inputGen = from count in Gen.Choose(1, 4)
                       from separator in SeparatorGen
                       select (count, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, separator) = input;

                // Create source properties all with empty string Format
                var sourceProperties = Enumerable.Range(0, count)
                    .Select(i => new PropertyModel
                    {
                        PropertyName = $"Prop{i}",
                        PropertyType = "string",
                        Format = ""
                    })
                    .ToArray();

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = null,
                    Separator = separator
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // Expected: all simple placeholders, no format specifiers injected
                var expectedParts = Enumerable.Range(0, count).Select(i => $"{{{i}}}");
                var expected = string.Join(separator, expectedParts);

                return (result == expected).ToProperty()
                    .Label($"Count={count}, Expected='{expected}', Actual='{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.5, 6.2**
    ///
    /// Property 11: Explicit Specifier Precedence Over Source Property Format
    ///
    /// For any computed format string where placeholder at index I already has an explicit
    /// format specifier, the source property Format at index I SHALL NOT be used, and the
    /// effective format string retains the explicit specifier unchanged.
    ///
    /// This test specifically generates format strings where ALL indices have explicit format
    /// specifiers, and then provides source properties with DIFFERENT Format values at those
    /// same indices — verifying that the explicit specifiers always win.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_ExplicitSpecifiers_TakePrecedence_OverSourcePropertyFormats()
    {
        // Generate explicit format specifiers for the computed format string
        var explicitSpecGen = Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00",
            "dd/MM/yyyy", "F2", "X8", "C", "P0", "E2");

        // Generate DIFFERENT source property formats (must differ from explicit)
        var sourceFormatGen = Gen.Elements(
            "MM/dd/yyyy", "D8", "F", "N0", "mm:ss", "0.000",
            "yyyy/MM/dd", "F4", "X4", "C2", "P2", "E4");

        var inputGen = from count in Gen.Choose(1, 4)
                       from explicitSpecs in Gen.ListOf(count, explicitSpecGen)
                       from sourceFormats in Gen.ListOf(count, sourceFormatGen)
                       from separator in SeparatorGen
                       select (count, explicitSpecs.ToArray(), sourceFormats.ToArray(), separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, explicitSpecs, sourceFormats, separator) = input;

                // Build an explicit format string with specifiers at ALL indices
                var formatParts = new string[count];
                for (int i = 0; i < count; i++)
                {
                    formatParts[i] = $"{{{i}:{explicitSpecs[i]}}}";
                }
                var explicitFormat = string.Join(separator, formatParts);

                // Create source properties with DIFFERENT Format values at each index
                var sourceProperties = new PropertyModel[count];
                for (int i = 0; i < count; i++)
                {
                    sourceProperties[i] = new PropertyModel
                    {
                        PropertyName = $"Prop{i}",
                        PropertyType = "string",
                        Format = sourceFormats[i] // Different from explicitSpecs[i]
                    };
                }

                // Create ComputedKeyModel with explicit Format set
                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = explicitFormat,
                    Separator = separator
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // Assert: The result MUST be the explicit format unchanged —
                // source property Format values are completely ignored
                return (result == explicitFormat).ToProperty()
                    .Label($"ExplicitFormat='{explicitFormat}', " +
                           $"SourceFormats=[{string.Join(", ", sourceFormats)}], " +
                           $"Result='{result}', " +
                           $"Expected='{explicitFormat}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.5, 6.2**
    ///
    /// Property 11 (mixed indices): Explicit Specifier Precedence Over Source Property Format
    ///
    /// For format strings where SOME indices have explicit specifiers and some do not,
    /// the explicit specifiers at those indices still take precedence. The entire explicit
    /// format string is returned unchanged when HasCustomFormat is true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeFormatString_MixedExplicitSpecifiers_StillReturnExplicitFormat_WhenHasCustomFormat()
    {
        var specifierGen = Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00");

        var sourceFormatGen = Gen.Elements(
            "MM/dd/yyyy", "D8", "F", "N0", "mm:ss", "0.000");

        var inputGen = from count in Gen.Choose(2, 4)
                       from hasExplicitAtIndex in Gen.ListOf(count, Arb.Default.Bool().Generator)
                       from specifiers in Gen.ListOf(count, specifierGen)
                       from sourceFormats in Gen.ListOf(count, sourceFormatGen)
                       from separator in SeparatorGen
                       select (count, hasExplicitAtIndex.ToArray(), specifiers.ToArray(), sourceFormats.ToArray(), separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (count, hasExplicitAtIndex, specifiers, sourceFormats, separator) = input;

                // Ensure at least one index has an explicit specifier so HasCustomFormat is true
                // (a format string with at least one specifier syntax is still an explicit format)
                var atLeastOneExplicit = hasExplicitAtIndex.Any(x => x);
                if (!atLeastOneExplicit)
                    hasExplicitAtIndex[0] = true;

                // Build a mixed explicit format string (some indices with specifiers, some without)
                var formatParts = new string[count];
                for (int i = 0; i < count; i++)
                {
                    formatParts[i] = hasExplicitAtIndex[i]
                        ? $"{{{i}:{specifiers[i]}}}"
                        : $"{{{i}}}";
                }
                var explicitFormat = string.Join(separator, formatParts);

                // Create source properties with Format values at ALL indices
                var sourceProperties = new PropertyModel[count];
                for (int i = 0; i < count; i++)
                {
                    sourceProperties[i] = new PropertyModel
                    {
                        PropertyName = $"Prop{i}",
                        PropertyType = "string",
                        Format = sourceFormats[i]
                    };
                }

                // Create ComputedKeyModel with the explicit format (HasCustomFormat = true)
                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(p => p.PropertyName).ToArray(),
                    Format = explicitFormat,
                    Separator = separator
                };

                // Act
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

                // Assert: Even with source properties having Format values,
                // the explicit format is returned unchanged (source formats are NOT injected)
                return (result == explicitFormat).ToProperty()
                    .Label($"ExplicitFormat='{explicitFormat}', " +
                           $"SourceFormats=[{string.Join(", ", sourceFormats)}], " +
                           $"Result='{result}', " +
                           $"Expected explicit format unchanged");
            });
    }

    /// <summary>
    /// Builds an explicit format string with a mix of specifiers and simple placeholders.
    /// </summary>
    private static string BuildExplicitFormat(string?[] specifiers, int count)
    {
        var parts = new string[count];
        for (int i = 0; i < count; i++)
        {
            var spec = i < specifiers.Length ? specifiers[i] : null;
            parts[i] = spec != null ? $"{{{i}:{spec}}}" : $"{{{i}}}";
        }
        return string.Join("#", parts);
    }
}
