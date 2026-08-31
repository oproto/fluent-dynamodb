using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.Utilities;

/// <summary>
/// Provides helper methods for mapping placeholder indices in .NET composite format strings
/// to their actual positions in a split array. When a format string contains constant literal
/// segments (e.g., "TENANT#{0}#EXTERNAL_ACCESS"), splitting on the separator produces an array
/// where placeholder positions differ from their placeholder indices. This utility builds a
/// mapping from placeholder index to split position.
/// </summary>
internal static class FormatPlaceholderMapper
{
    /// <summary>
    /// Compiled regex that matches a segment consisting entirely of a placeholder: {N} or {N:format}
    /// where N is one or more digits and format is an optional format specifier.
    /// </summary>
    private static readonly Regex PlaceholderPattern =
        new Regex(@"^\{(\d+)(?::.*?)?\}$", RegexOptions.Compiled);

    /// <summary>
    /// Builds a dictionary mapping each placeholder index to its split position in the format string.
    /// </summary>
    /// <param name="format">The composite format string (e.g., "TENANT#{0}#EXTERNAL_ACCESS").</param>
    /// <param name="separator">The separator character used to split the format string.</param>
    /// <returns>
    /// A dictionary where each key is a placeholder index (N from {N}) and each value is the
    /// zero-based position of that placeholder in the split array.
    /// </returns>
    public static Dictionary<int, int> BuildPlaceholderToSplitIndexMap(string format, char separator)
    {
        var segments = format.Split(separator);
        var mapping = new Dictionary<int, int>();

        for (int i = 0; i < segments.Length; i++)
        {
            var match = PlaceholderPattern.Match(segments[i]);
            if (match.Success)
            {
                var placeholderIndex = int.Parse(match.Groups[1].Value);
                mapping[placeholderIndex] = i;
            }
        }

        return mapping;
    }

    /// <summary>
    /// Gets the split index for a given placeholder index in the format string.
    /// If the placeholder index is not found in the mapping, falls back to the placeholder index itself.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="separator">The separator character used to split the format string.</param>
    /// <param name="placeholderIndex">The placeholder index to look up.</param>
    /// <returns>
    /// The split position for the given placeholder index, or the placeholder index itself if not found.
    /// </returns>
    public static int GetSplitIndex(string format, char separator, int placeholderIndex)
    {
        var mapping = BuildPlaceholderToSplitIndexMap(format, separator);
        return mapping.TryGetValue(placeholderIndex, out var splitIndex) ? splitIndex : placeholderIndex;
    }
}
