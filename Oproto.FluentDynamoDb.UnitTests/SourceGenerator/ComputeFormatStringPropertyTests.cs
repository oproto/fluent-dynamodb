using System.Text;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for MapperGenerator.ComputeFormatString.
/// </summary>
public class ComputeFormatStringPropertyTests
{
    /// <summary>
    /// Generates separator strings that are valid in format string contexts.
    /// Excludes '{' and '}' which have special meaning in .NET format strings.
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "_", "-", ":", "|", ".", "~", "::", "##", "__", "/", "@", "||");
    }

    /// <summary>
    /// Generates source values that are safe for format string operations.
    /// Any string value is safe as an argument to string.Format — only the format template matters.
    /// </summary>
    private static Gen<string> GenSourceValue()
    {
        return Gen.Elements(
            "alpha", "beta", "gamma", "delta", "epsilon",
            "123", "ABC", "test-value", "hello world",
            "ORDER", "USER", "2024", "us-east-1",
            "", "a", "some longer value with spaces");
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 4.4, 7.1**
    /// For any separator and N source values (N ≥ 1), the format string generated
    /// for a Separator-only configuration (no prefix) satisfies:
    /// string.Format(generatedFormat, values) == string.Join(separator, values)
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "computed-field-format-normalization")]
    [Trait("Property", "1")]
    public Property FormatGenerationRoundTrip_NoPrefix()
    {
        var testCaseGen = from separator in GenSeparator()
                          from count in Gen.Choose(1, 5)
                          from values in Gen.ArrayOf(count, GenSourceValue())
                          select (separator, count, values);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (separator, count, values) = testCase;

                // Build a ComputedKeyModel with no custom format
                var sourceProperties = Enumerable.Range(0, count)
                    .Select(i => $"Prop{i}")
                    .ToArray();

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties,
                    Separator = separator,
                    Format = null // No custom format — use separator-based generation
                };

                // Act: generate the format string (no prefix case)
                var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, null);

                // Apply string.Format with the generated format
                var formatResult = string.Format(generatedFormat, values.Cast<object>().ToArray());

                // Expected: string.Join(separator, values)
                var joinResult = string.Join(separator, values);

                return (formatResult == joinResult).ToProperty()
                    .Label($"separator='{separator}', count={count}, " +
                           $"format='{generatedFormat}', " +
                           $"formatResult='{formatResult}', joinResult='{joinResult}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.3, 7.2**
    /// For any prefix, keySeparator, computedSeparator, and N source values (N >= 1),
    /// the format string generated for a configuration with key prefix satisfies:
    /// string.Format(generatedFormat, values) == prefix + keySeparator + string.Join(computedSeparator, values)
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "computed-field-format-normalization")]
    [Trait("Property", "2")]
    public Property FormatGenerationRoundTrip_WithPrefix()
    {
        var testCaseGen = from prefix in GenPrefix()
                          from keySeparator in GenSeparator()
                          from computedSeparator in GenSeparator()
                          from count in Gen.Choose(1, 5)
                          from values in Gen.ArrayOf(count, GenSourceValue())
                          select (prefix, keySeparator, computedSeparator, count, values);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (prefix, keySeparator, computedSeparator, count, values) = testCase;

                // Build a ComputedKeyModel with no custom format
                var sourceProperties = Enumerable.Range(0, count)
                    .Select(i => $"Prop{i}")
                    .ToArray();

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties,
                    Separator = computedSeparator,
                    Format = null // No custom format — use separator-based generation
                };

                // Build a KeyFormatModel with prefix and keySeparator
                var keyFormat = new KeyFormatModel
                {
                    Prefix = prefix,
                    Separator = keySeparator
                };

                // Act: generate the format string (with prefix case)
                var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);

                // Apply string.Format with the generated format
                var formatResult = string.Format(generatedFormat, values.Cast<object>().ToArray());

                // Expected: prefix + keySeparator + string.Join(computedSeparator, values)
                var joinResult = prefix + keySeparator + string.Join(computedSeparator, values);

                return (formatResult == joinResult).ToProperty()
                    .Label($"prefix='{prefix}', keySep='{keySeparator}', compSep='{computedSeparator}', " +
                           $"count={count}, format='{generatedFormat}', " +
                           $"formatResult='{formatResult}', joinResult='{joinResult}'");
            });
    }

    /// <summary>
    /// Generates non-empty prefix strings that are valid in format string contexts.
    /// Excludes '{' and '}' which have special meaning in .NET format strings.
    /// </summary>
    private static Gen<string> GenPrefix()
    {
        return Gen.Elements("ORDER", "USER", "CUSTOMER", "TENANT", "INVOICE", "PRODUCT", "EVENT", "SESSION", "ACCT", "META");
    }

    /// <summary>
    /// **Validates: Requirements 1.4, 1.5, 7.3**
    /// For any valid format string with exactly N placeholders ({0} through {N-1}),
    /// when specified via HasCustomFormat=true (explicit Format property set),
    /// the generator emits it unchanged regardless of Separator or keyFormat values.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "computed-field-format-normalization")]
    [Trait("Property", "3")]
    public Property ExplicitFormat_PassedThroughUnchanged()
    {
        var testCaseGen = from count in Gen.Choose(1, 8)
                          from separator in GenSeparator()
                          from hasPrefixFlag in Arb.Default.Bool().Generator
                          from prefix in GenPrefix()
                          from keySeparator in GenSeparator()
                          from formatSeparator in GenSeparator()
                          let formatString = string.Join(formatSeparator,
                              Enumerable.Range(0, count).Select(i => $"{{{i}}}"))
                          select (formatString, count, separator, hasPrefixFlag, prefix, keySeparator);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (formatString, count, separator, hasPrefixFlag, prefix, keySeparator) = testCase;

                // Create a ComputedKeyModel with HasCustomFormat=true (Format is set)
                var sourceProperties = Enumerable.Range(0, count)
                    .Select(i => $"Prop{i}")
                    .ToArray();

                var computedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties,
                    Separator = separator, // Should be ignored when HasCustomFormat is true
                    Format = formatString  // Explicit format — triggers HasCustomFormat=true
                };

                // Optionally provide a keyFormat with prefix (should also be ignored)
                KeyFormatModel? keyFormat = hasPrefixFlag
                    ? new KeyFormatModel { Prefix = prefix, Separator = keySeparator }
                    : null;

                // Act: generate the format string
                var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat);

                // Assert: the explicit format string is returned unchanged
                return (result == formatString).ToProperty()
                    .Label($"Expected format to pass through unchanged. " +
                           $"Input: '{formatString}', Output: '{result}', " +
                           $"Separator: '{separator}', HasPrefix: {hasPrefixFlag}, " +
                           $"Prefix: '{prefix}', KeySep: '{keySeparator}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6, 2.4**
    /// For any computed field configuration (whether Separator-based or explicit Format),
    /// the generated format string contains exactly N sequential positional placeholders
    /// {0} through {N-1} where N equals the count of source properties.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "computed-field-format-normalization")]
    [Trait("Property", "4")]
    public Property PlaceholderCount_EqualsSourcePropertyCount()
    {
        // Generator for separator-based configurations (no custom format)
        var separatorBasedGen = from separator in GenSeparator()
                                from count in Gen.Choose(1, 10)
                                from hasPrefix in Arb.Default.Bool().Generator
                                from prefix in GenPrefix()
                                from keySeparator in GenSeparator()
                                select (
                                    separator,
                                    count,
                                    hasPrefix,
                                    prefix,
                                    keySeparator,
                                    customFormat: (string?)null
                                );

        // Generator for explicit format configurations with exactly N placeholders
        var explicitFormatGen = from count in Gen.Choose(1, 10)
                                from separator in GenSeparator()
                                let format = string.Join(separator, Enumerable.Range(0, count).Select(i => $"{{{i}}}"))
                                select (
                                    separator,
                                    count,
                                    hasPrefix: false,
                                    prefix: "",
                                    keySeparator: "#",
                                    customFormat: (string?)format
                                );

        var combined = Gen.OneOf(separatorBasedGen, explicitFormatGen);

        return Prop.ForAll(combined.ToArbitrary(), tuple =>
        {
            var (separator, count, hasPrefix, prefix, keySeparator, customFormat) = tuple;

            var sourceProperties = Enumerable.Range(0, count).Select(i => $"Prop{i}").ToArray();

            var computedKey = new ComputedKeyModel
            {
                Separator = separator,
                SourceProperties = sourceProperties,
                Format = customFormat
            };

            KeyFormatModel? keyFormat = hasPrefix && customFormat == null
                ? new KeyFormatModel { Prefix = prefix, Separator = keySeparator }
                : null;

            var formatString = MapperGenerator.ComputeFormatString(computedKey, keyFormat);

            // Count placeholders using regex
            var matches = Regex.Matches(formatString, @"\{(\d+)\}");
            var placeholderCount = matches.Count;

            // Verify count equals N
            var countMatches = (placeholderCount == count).ToProperty()
                .Label($"Expected {count} placeholders, found {placeholderCount} in '{formatString}'");

            // Verify they are sequential: {0}, {1}, ..., {N-1}
            var indices = matches.Cast<Match>()
                .Select(m => int.Parse(m.Groups[1].Value))
                .OrderBy(i => i)
                .ToArray();

            var expectedIndices = Enumerable.Range(0, count).ToArray();
            var sequential = indices.SequenceEqual(expectedIndices).ToProperty()
                .Label($"Placeholders not sequential. Expected [{string.Join(",", expectedIndices)}], " +
                       $"got [{string.Join(",", indices)}] in '{formatString}'");

            return countMatches.And(sequential);
        });
    }

    /// <summary>
    /// Generates strings that contain characters requiring C# string literal escaping:
    /// backslash, double-quote, newline, carriage return, and tab.
    /// Also includes normal characters and format-string curly braces as text.
    /// </summary>
    private static Gen<string> GenStringWithEscapableChars()
    {
        // Characters that need escaping in C# string literals
        var escapableChars = Gen.Elements('\\', '"', '\n', '\r', '\t');

        // Normal characters
        var normalChars = Gen.Elements(
            'a', 'b', 'c', 'x', 'y', 'z', '0', '1', '2',
            '#', '_', '-', '.', ' ', '!', '@', '$', '%');

        // Mix of escapable and normal chars
        var charGen = Gen.Frequency(
            Tuple.Create(3, normalChars),
            Tuple.Create(2, escapableChars));

        return Gen.Choose(1, 20)
            .SelectMany(length => Gen.ArrayOf(length, charGen))
            .Select(chars => new string(chars));
    }

    /// <summary>
    /// Simulates "interpreting" an escaped C# string literal by reversing the escape transformations.
    /// This is the inverse of MapperGenerator.EscapeString:
    ///   \\  → \
    ///   \"  → "
    ///   \n  → newline
    ///   \r  → carriage return
    ///   \t  → tab
    /// </summary>
    private static string UnescapeCSharpStringLiteral(string escaped)
    {
        var sb = new StringBuilder(escaped.Length);
        for (var i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == '\\' && i + 1 < escaped.Length)
            {
                switch (escaped[i + 1])
                {
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    default:
                        sb.Append(escaped[i]);
                        break;
                }
            }
            else
            {
                sb.Append(escaped[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.3**
    /// For any format string containing backslash, double-quote, or other characters
    /// requiring C# string escaping, the escaped string literal produced by
    /// MapperGenerator.EscapeString evaluates back to the original string at runtime.
    /// This proves that escaping is round-trippable: unescape(escape(original)) == original.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "computed-field-format-normalization")]
    [Trait("Property", "6")]
    public Property StringEscaping_IsRoundTrippable()
    {
        return Prop.ForAll(
            GenStringWithEscapableChars().ToArbitrary(),
            original =>
            {
                // Act: escape the string as MapperGenerator would when emitting a C# string literal
                var escaped = MapperGenerator.EscapeString(original);

                // Simulate what C# compilation would do: interpret the escaped string literal
                var roundTripped = UnescapeCSharpStringLiteral(escaped);

                // Assert: round-tripping through escape → unescape gives back the original
                return (roundTripped == original).ToProperty()
                    .Label($"Round-trip failed. Original (len={original.Length}): " +
                           $"[{string.Join(",", original.Select(c => $"0x{(int)c:X2}"))}], " +
                           $"Escaped: '{escaped}', " +
                           $"RoundTripped (len={roundTripped.Length}): " +
                           $"[{string.Join(",", roundTripped.Select(c => $"0x{(int)c:X2}"))}]");
            });
    }
}
