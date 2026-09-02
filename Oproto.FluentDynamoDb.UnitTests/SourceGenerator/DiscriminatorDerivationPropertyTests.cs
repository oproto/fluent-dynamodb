using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for EntityAnalyzer.DeriveDiscriminatorPattern.
/// Feature: unify-prefix-computed-discriminator, Property 2: Discriminator Pattern Derivation from Format
/// </summary>
public class DiscriminatorDerivationPropertyTests
{
    /// <summary>
    /// Generates a non-empty literal segment (no '{', '}', or '*' characters).
    /// </summary>
    private static Gen<string> GenLiteralSegment()
    {
        return Gen.Elements(
            "ORDER", "USER", "CUSTOMER", "TENANT", "INVOICE",
            "PRODUCT", "EVENT", "SESSION", "ACCT", "META",
            "LINE", "DETAIL", "TYPE", "GSI1", "STATUS",
            "#", "_", "-", ":", "|", ".", "~",
            "ORDER#", "USER_", "TENANT:", "PREFIX-",
            "A", "AB", "ABC");
    }

    /// <summary>
    /// Generates a format string that starts with a literal prefix followed by
    /// N placeholders interleaved with literal segments.
    /// This guarantees the format does NOT start with a placeholder, so
    /// DeriveDiscriminatorPattern will return a non-null result.
    /// </summary>
    private static Gen<(string Format, string ExpectedPattern)> GenFormatWithLiteralPrefix()
    {
        return from leadingLiteral in GenLiteralSegment()
               from placeholderCount in Gen.Choose(1, 4)
               from separators in Gen.ListOf(placeholderCount - 1, GenLiteralSegment())
               select BuildFormatAndExpected(leadingLiteral, placeholderCount, separators.ToList());
    }

    private static (string Format, string ExpectedPattern) BuildFormatAndExpected(
        string leadingLiteral, int placeholderCount, List<string> separators)
    {
        // Build format: leadingLiteral + {0} + sep[0] + {1} + sep[1] + ... + {N-1}
        var format = leadingLiteral + "{0}";
        var expected = leadingLiteral + "*";

        for (var i = 1; i < placeholderCount; i++)
        {
            var sep = separators[i - 1];
            format += sep + "{" + i + "}";
            expected += sep + "*";
        }

        return (format, expected);
    }

    /// <summary>
    /// Generates a format string that starts with a placeholder (e.g., "{0}#something").
    /// DeriveDiscriminatorPattern should return null for these because the pattern
    /// would start with "*".
    /// </summary>
    private static Gen<string> GenFormatStartingWithPlaceholder()
    {
        return from trailingSuffix in GenLiteralSegment()
               from extraPlaceholders in Gen.Choose(0, 2)
               select BuildFormatStartingWithPlaceholder(trailingSuffix, extraPlaceholders);
    }

    private static string BuildFormatStartingWithPlaceholder(string trailingSuffix, int extraPlaceholders)
    {
        var format = "{0}" + trailingSuffix;
        for (var i = 1; i <= extraPlaceholders; i++)
        {
            format += "{" + i + "}";
        }

        return format;
    }

    /// <summary>
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    /// For any normalized key format string containing N placeholders {0} through {N-1}
    /// interleaved with arbitrary literal segments where the format starts with a literal prefix,
    /// the derived discriminator pattern SHALL be identical to the format string with every
    /// {N} placeholder replaced by *, preserving all literal text unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "2")]
    public Property DeriveDiscriminatorPattern_ReplacesPlaceholdersWithWildcards()
    {
        return Prop.ForAll(
            GenFormatWithLiteralPrefix().ToArbitrary(),
            testCase =>
            {
                var (format, expectedPattern) = testCase;

                // Act
                var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

                // The result should not be null (format starts with a literal prefix)
                var notNull = result != null;

                // The result should equal the format with all {N} replaced by *
                var matchesExpected = result == expectedPattern;

                // Additionally verify that the expected pattern equals a manual regex replacement
                var manualReplacement = Regex.Replace(format, @"\{\d+\}", "*");
                var matchesManual = result == manualReplacement;

                return (notNull && matchesExpected && matchesManual).ToProperty()
                    .Label($"format='{format}', expected='{expectedPattern}', " +
                           $"result='{result}', notNull={notNull}, " +
                           $"matchesExpected={matchesExpected}, matchesManual={matchesManual}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    /// For any format string starting with a placeholder (e.g., "{0}#something"),
    /// DeriveDiscriminatorPattern SHALL return null because the resulting pattern
    /// would start with "*" and provides no useful fixed prefix for discrimination.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "2")]
    public Property DeriveDiscriminatorPattern_ReturnsNullForFormatsStartingWithPlaceholder()
    {
        return Prop.ForAll(
            GenFormatStartingWithPlaceholder().ToArbitrary(),
            format =>
            {
                // Act
                var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

                // Should return null because the pattern starts with "*"
                return (result == null).ToProperty()
                    .Label($"format='{format}', result='{result}' (expected null)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    /// The trivial format "{0}" (no prefix, single placeholder) SHALL produce null.
    /// </summary>
    [Fact]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "2")]
    public void DeriveDiscriminatorPattern_TrivialFormat_ReturnsNull()
    {
        var result = EntityAnalyzer.DeriveDiscriminatorPattern("{0}");
        Assert.Null(result);
    }
}
