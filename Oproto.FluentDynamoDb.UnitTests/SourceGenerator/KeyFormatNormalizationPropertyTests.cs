using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for EntityAnalyzer.ComputeNonComputedKeyFormat.
/// Feature: unify-prefix-computed-discriminator, Property 1: Non-Computed Key Format Derivation
/// </summary>
public class KeyFormatNormalizationPropertyTests
{
    private static readonly MethodInfo ComputeNonComputedKeyFormatMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ComputeNonComputedKeyFormat",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Invokes the private static ComputeNonComputedKeyFormat method via reflection.
    /// </summary>
    private static string InvokeComputeNonComputedKeyFormat(KeyFormatModel? keyFormat)
    {
        return (string)ComputeNonComputedKeyFormatMethod.Invoke(null, new object?[] { keyFormat })!;
    }

    /// <summary>
    /// Generates non-empty prefix strings safe for format string contexts.
    /// Excludes '{' and '}' which have special meaning in .NET format strings.
    /// </summary>
    private static Gen<string> GenNonEmptyPrefix()
    {
        return Gen.Elements(
            "ORDER", "USER", "CUSTOMER", "TENANT", "INVOICE",
            "PRODUCT", "EVENT", "SESSION", "ACCT", "META",
            "A", "AB", "PREFIX", "GSI1", "TYPE");
    }

    /// <summary>
    /// Generates separator strings including empty string.
    /// Excludes '{' and '}' which have special meaning in .NET format strings.
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "_", "-", ":", "|", ".", "~", "::", "##", "__", "/", "@", "||", "");
    }

    /// <summary>
    /// Generates arbitrary value strings to be formatted into the key.
    /// </summary>
    private static Gen<string> GenValue()
    {
        return Gen.Elements(
            "alpha", "beta", "gamma", "delta", "epsilon",
            "123", "ABC", "test-value", "hello world",
            "ORDER", "USER", "2024", "us-east-1",
            "", "a", "some longer value with spaces",
            "uuid-1234-5678", "customer-42");
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// For any non-empty prefix string and for any separator string (including empty string),
    /// the normalized key format derived for a non-computed key property SHALL equal
    /// "{prefix}{separator}{0}", and string.Format(derivedFormat, value) SHALL equal
    /// prefix + separator + value for any value string.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "1")]
    public Property NonComputedKeyFormat_DerivedFormatProducesCorrectValue()
    {
        var testCaseGen = from prefix in GenNonEmptyPrefix()
                          from separator in GenSeparator()
                          from value in GenValue()
                          select (prefix, separator, value);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (prefix, separator, value) = testCase;

                var keyFormat = new KeyFormatModel
                {
                    Prefix = prefix,
                    Separator = separator
                };

                // Act: compute the non-computed key format
                var derivedFormat = InvokeComputeNonComputedKeyFormat(keyFormat);

                // Verify the format string shape: "{prefix}{separator}{0}"
                var expectedFormat = $"{prefix}{separator}{{0}}";
                var formatShapeCorrect = derivedFormat == expectedFormat;

                // Verify that string.Format(derivedFormat, value) == prefix + separator + value
                var formatResult = string.Format(derivedFormat, value);
                var expectedResult = prefix + separator + value;
                var formatResultCorrect = formatResult == expectedResult;

                return (formatShapeCorrect && formatResultCorrect).ToProperty()
                    .Label($"prefix='{prefix}', separator='{separator}', value='{value}', " +
                           $"derivedFormat='{derivedFormat}', expectedFormat='{expectedFormat}', " +
                           $"formatResult='{formatResult}', expectedResult='{expectedResult}'");
            });
    }
}
