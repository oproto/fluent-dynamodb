using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Provides helper methods for detecting format specifiers in .NET composite format strings.
/// </summary>
internal static class FormatSpecifierHelper
{
    // Regex: matches {N:specifier} where N is one or more digits and specifier is non-empty
    private static readonly Regex FormatSpecifierPattern =
        new(@"\{(\d+):([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Determines whether any placeholder in the format string contains a format specifier.
    /// </summary>
    /// <param name="format">The composite format string (e.g., "{0:yyyy-MM-dd}#{1}").</param>
    /// <returns>True if at least one placeholder has a format specifier after the colon.</returns>
    public static bool HasAnyFormatSpecifier(string? format)
    {
        if (string.IsNullOrEmpty(format))
            return false;
        return FormatSpecifierPattern.IsMatch(format);
    }

    /// <summary>
    /// Determines whether the placeholder at the given index has a format specifier.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="index">The placeholder index to check.</param>
    /// <returns>True if the placeholder at that index has a format specifier.</returns>
    public static bool HasFormatSpecifierForIndex(string? format, int index)
    {
        if (string.IsNullOrEmpty(format))
            return false;

        foreach (Match match in FormatSpecifierPattern.Matches(format))
        {
            if (int.TryParse(match.Groups[1].Value, out var matchIndex) && matchIndex == index)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the set of placeholder indices that have format specifiers.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <returns>A set of zero-based placeholder indices that include format specifiers.</returns>
    public static HashSet<int> GetIndicesWithFormatSpecifiers(string? format)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrEmpty(format))
            return result;

        foreach (Match match in FormatSpecifierPattern.Matches(format))
        {
            if (int.TryParse(match.Groups[1].Value, out var index))
                result.Add(index);
        }
        return result;
    }
}
