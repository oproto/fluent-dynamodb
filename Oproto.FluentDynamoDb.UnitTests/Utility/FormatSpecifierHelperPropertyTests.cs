using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

/// <summary>
/// Property-based tests for FormatSpecifierHelper.
/// Validates that GetIndicesWithFormatSpecifiers, HasAnyFormatSpecifier, and
/// HasFormatSpecifierForIndex correctly identify format specifiers across
/// randomly generated composite format strings.
/// </summary>
[Trait("Feature", "computed-field-format-specifiers")]
[Trait("Property", "1")]
public class FormatSpecifierHelperPropertyTests
{
    /// <summary>
    /// Separator characters used between placeholders in composite format strings.
    /// </summary>
    private static Gen<char> SeparatorCharGen =>
        Gen.Elements('#', '-', '_');

    /// <summary>
    /// Generates a random format specifier string (e.g., "yyyy-MM-dd", "D4", "G", "HH:mm:ss").
    /// </summary>
    private static Gen<string> FormatSpecifierGen =>
        Gen.Elements(
            "yyyy-MM-dd", "D4", "G", "N2", "HH:mm:ss", "0.00",
            "dd/MM/yyyy", "F2", "X8", "C", "P0", "E2");

    /// <summary>
    /// Generates a single placeholder definition: index + whether it has a format specifier.
    /// </summary>
    private static Gen<(int Index, bool HasSpecifier, string? Specifier)> PlaceholderGen(int index) =>
        from hasSpecifier in Arb.Default.Bool().Generator
        from specifier in FormatSpecifierGen
        select (index, hasSpecifier, hasSpecifier ? specifier : (string?)null);

    /// <summary>
    /// Generates a list of 1-5 placeholder definitions with sequential indices.
    /// </summary>
    private static Gen<List<(int Index, bool HasSpecifier, string? Specifier)>> PlaceholderListGen =>
        from count in Gen.Choose(1, 5)
        from placeholders in Gen.Sequence(
            Enumerable.Range(0, count).Select(i => PlaceholderGen(i)))
        select placeholders.ToList();

    /// <summary>
    /// Builds a composite format string from placeholder definitions and a separator.
    /// </summary>
    private static string BuildFormatString(
        List<(int Index, bool HasSpecifier, string? Specifier)> placeholders, char separator)
    {
        var parts = placeholders.Select(p =>
            p.HasSpecifier ? $"{{{p.Index}:{p.Specifier}}}" : $"{{{p.Index}}}");
        return string.Join(separator.ToString(), parts);
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// Property 1: GetIndicesWithFormatSpecifiers returns exactly the set of indices
    /// that have format specifiers in a randomly generated composite format string.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetIndicesWithFormatSpecifiers_ReturnsExactlySpecifiedIndices()
    {
        var inputGen = from placeholders in PlaceholderListGen
                       from separator in SeparatorCharGen
                       select (placeholders, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (placeholders, separator) = input;
                var formatString = BuildFormatString(placeholders, separator);

                var expectedIndices = new HashSet<int>(
                    placeholders.Where(p => p.HasSpecifier).Select(p => p.Index));

                var actualIndices = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers(formatString);

                return actualIndices.SetEquals(expectedIndices).ToProperty()
                    .Label($"Format='{formatString}', Expected=[{string.Join(",", expectedIndices)}], Actual=[{string.Join(",", actualIndices)}]");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// HasAnyFormatSpecifier returns true if and only if at least one placeholder
    /// in the format string has a format specifier.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasAnyFormatSpecifier_TrueIffAtLeastOneSpecifier()
    {
        var inputGen = from placeholders in PlaceholderListGen
                       from separator in SeparatorCharGen
                       select (placeholders, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (placeholders, separator) = input;
                var formatString = BuildFormatString(placeholders, separator);

                var expectedHasAny = placeholders.Any(p => p.HasSpecifier);
                var actual = FormatSpecifierHelper.HasAnyFormatSpecifier(formatString);

                return (actual == expectedHasAny).ToProperty()
                    .Label($"Format='{formatString}', ExpectedHasAny={expectedHasAny}, Actual={actual}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// HasFormatSpecifierForIndex returns true for each index that has a specifier
    /// and false for each index that does not.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasFormatSpecifierForIndex_CorrectForEachIndex()
    {
        var inputGen = from placeholders in PlaceholderListGen
                       from separator in SeparatorCharGen
                       select (placeholders, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (placeholders, separator) = input;
                var formatString = BuildFormatString(placeholders, separator);

                var allCorrect = placeholders.All(p =>
                    FormatSpecifierHelper.HasFormatSpecifierForIndex(formatString, p.Index) == p.HasSpecifier);

                return allCorrect.ToProperty()
                    .Label($"Format='{formatString}', not all indices matched expectations");
            });
    }
}
