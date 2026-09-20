using System.Collections.Generic;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Utilities;

/// <summary>
/// Enumerates the kinds of diagnostics produced during named placeholder normalization.
/// Each kind maps to a specific FDDB diagnostic code.
/// </summary>
internal enum NormalizationDiagnosticKind
{
    /// <summary>FDDB091: Multiple positional args containing '{', or named-placeholder Format combined with explicit source properties.</summary>
    AmbiguousUsage,

    /// <summary>FDDB092: Named placeholder references a property that does not exist on the entity.</summary>
    UnresolvedProperty,

    /// <summary>FDDB093: Format string mixes named placeholders ({Name}) with positional placeholders ({N}).</summary>
    MixedNamedAndPositional,

    /// <summary>FDDB094: Unclosed brace in format string ({Name without closing }).</summary>
    MalformedPlaceholder,

    /// <summary>FDDB095: Empty placeholder {} in format string.</summary>
    EmptyPlaceholder,

    /// <summary>FDDB096: Placeholder matches both a property name and a positional index; resolved as property name.</summary>
    AmbiguousNameIndex
}

/// <summary>
/// A diagnostic produced during named placeholder normalization.
/// </summary>
internal readonly struct NormalizationDiagnostic
{
    /// <summary>The category of this diagnostic, mapping to a specific FDDB code.</summary>
    public NormalizationDiagnosticKind Kind { get; }

    /// <summary>Human-readable description of the issue.</summary>
    public string Message { get; }

    /// <summary>The placeholder token that caused the diagnostic, if applicable.</summary>
    public string? Token { get; }

    /// <summary>Character offset within the format string where the issue was detected, if applicable.</summary>
    public int? CharOffset { get; }

    public NormalizationDiagnostic(NormalizationDiagnosticKind kind, string message, string? token = null, int? charOffset = null)
    {
        Kind = kind;
        Message = message;
        Token = token;
        CharOffset = charOffset;
    }
}

/// <summary>
/// Result of normalizing a format string from named to positional placeholders.
/// </summary>
internal readonly struct NormalizationResult
{
    /// <summary>The rewritten format string with {N} and {N:specifier} placeholders.</summary>
    public string NormalizedFormat { get; }

    /// <summary>Source property names in positional index order (first appearance).</summary>
    public string[] SourceProperties { get; }

    /// <summary>True if the input contained named placeholders and was normalized.</summary>
    public bool WasNormalized { get; }

    /// <summary>Diagnostics produced during normalization (unresolved names, malformed tokens, etc.).</summary>
    public IReadOnlyList<NormalizationDiagnostic> Diagnostics { get; }

    public NormalizationResult(string normalizedFormat, string[] sourceProperties, bool wasNormalized, IReadOnlyList<NormalizationDiagnostic> diagnostics)
    {
        NormalizedFormat = normalizedFormat;
        SourceProperties = sourceProperties;
        WasNormalized = wasNormalized;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Returns true if any diagnostic has error severity (all kinds except AmbiguousNameIndex which is a warning).
    /// </summary>
    public bool HasErrors
    {
        get
        {
            for (int i = 0; i < Diagnostics.Count; i++)
            {
                if (Diagnostics[i].Kind != NormalizationDiagnosticKind.AmbiguousNameIndex)
                    return true;
            }
            return false;
        }
    }
}

/// <summary>
/// Static utility that parses <c>{PropertyName}</c> and <c>{PropertyName:format}</c> tokens
/// in format strings, resolves them against entity properties, and rewrites them to positional
/// <c>{N}</c> form. Used during entity analysis to normalize named placeholders before the
/// downstream pipeline runs.
/// </summary>
internal static class NamedPlaceholderNormalizer
{
    /// <summary>
    /// Determines whether a string contains named placeholders.
    /// A named placeholder is <c>{Identifier}</c> or <c>{Identifier:format}</c> where
    /// <c>Identifier</c> is a valid C# identifier that is not a non-negative integer.
    /// </summary>
    /// <param name="formatString">The format string to check.</param>
    /// <returns><c>true</c> if the string contains at least one named placeholder; otherwise <c>false</c>.</returns>
    public static bool ContainsNamedPlaceholders(string formatString)
    {
        if (string.IsNullOrEmpty(formatString))
            return false;

        int i = 0;
        while (i < formatString.Length)
        {
            if (formatString[i] == '{')
            {
                int closingBrace = FindClosingBrace(formatString, i);
                if (closingBrace < 0)
                {
                    // Malformed — skip past the opening brace.
                    i++;
                    continue;
                }

                // Extract the content between braces.
                int contentStart = i + 1;
                int contentLength = closingBrace - contentStart;

                if (contentLength > 0)
                {
                    // Extract identifier portion (before any ':' format specifier).
                    string content = formatString.Substring(contentStart, contentLength);
                    string identifier = ExtractIdentifier(content);

                    if (identifier.Length > 0 && IsValidCSharpIdentifier(identifier) && !IsNonNegativeInteger(identifier))
                        return true;
                }

                i = closingBrace + 1;
            }
            else
            {
                i++;
            }
        }

        return false;
    }

    /// <summary>
    /// Normalizes a format string from named to positional placeholders.
    /// </summary>
    /// <param name="formatString">The format string potentially containing <c>{PropertyName}</c> tokens.</param>
    /// <param name="knownPropertyNames">Set of declared property names on the entity for resolution.</param>
    /// <returns>Normalization result containing the rewritten format and inferred source properties.</returns>
    public static NormalizationResult Normalize(string formatString, HashSet<string> knownPropertyNames)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return new NormalizationResult(
                formatString ?? string.Empty,
                Array.Empty<string>(),
                wasNormalized: false,
                diagnostics: Array.Empty<NormalizationDiagnostic>());
        }

        var diagnostics = new List<NormalizationDiagnostic>();
        var sourceProperties = new List<string>();
        var propertyToIndex = new Dictionary<string, int>();
        var result = new StringBuilder(formatString.Length);

        bool hasNamedPlaceholders = false;
        bool hasPositionalPlaceholders = false;
        string? firstNamedToken = null;
        string? firstPositionalToken = null;

        int i = 0;
        while (i < formatString.Length)
        {
            if (formatString[i] == '{')
            {
                int openBraceOffset = i;
                int closingBrace = FindClosingBrace(formatString, i);

                if (closingBrace < 0)
                {
                    // Malformed: unclosed brace. Extract text from '{' to end for the diagnostic.
                    string malformedText = formatString.Substring(i);
                    diagnostics.Add(new NormalizationDiagnostic(
                        NormalizationDiagnosticKind.MalformedPlaceholder,
                        $"Unclosed brace at character offset {openBraceOffset}: '{malformedText}'",
                        token: malformedText,
                        charOffset: openBraceOffset));

                    // Append the rest of the string as-is and stop.
                    result.Append(malformedText);
                    break;
                }

                int contentStart = i + 1;
                int contentLength = closingBrace - contentStart;

                if (contentLength == 0)
                {
                    // Empty placeholder: {}
                    diagnostics.Add(new NormalizationDiagnostic(
                        NormalizationDiagnosticKind.EmptyPlaceholder,
                        $"Empty placeholder '{{}}' at character offset {openBraceOffset}",
                        token: "{}",
                        charOffset: openBraceOffset));

                    result.Append("{}");
                    i = closingBrace + 1;
                    continue;
                }

                string content = formatString.Substring(contentStart, contentLength);
                string identifier = ExtractIdentifier(content);
                string? formatSpecifier = ExtractFormatSpecifier(content);

                if (identifier.Length == 0)
                {
                    // Content doesn't start with a valid identifier — just literal, pass through.
                    result.Append('{');
                    result.Append(content);
                    result.Append('}');
                    i = closingBrace + 1;
                    continue;
                }

                bool isInteger = IsNonNegativeInteger(identifier);
                bool isValidIdentifier = IsValidCSharpIdentifier(identifier);
                bool isKnownProperty = isValidIdentifier && knownPropertyNames.Contains(identifier);

                if (isInteger && isKnownProperty)
                {
                    // Ambiguity: matches both a positional index and a property name.
                    // Resolve as property name per design, emit FDDB096 warning.
                    hasNamedPlaceholders = true;
                    if (firstNamedToken == null)
                        firstNamedToken = identifier;

                    diagnostics.Add(new NormalizationDiagnostic(
                        NormalizationDiagnosticKind.AmbiguousNameIndex,
                        $"Placeholder '{{{identifier}}}' matches both a property name and a positional index. " +
                        $"It was resolved as a property name. Consider renaming the property to avoid ambiguity.",
                        token: identifier));

                    int index = GetOrAssignIndex(identifier, propertyToIndex, sourceProperties);
                    AppendPositionalPlaceholder(result, index, formatSpecifier);
                    i = closingBrace + 1;
                }
                else if (isInteger)
                {
                    // Pure positional placeholder.
                    hasPositionalPlaceholders = true;
                    if (firstPositionalToken == null)
                        firstPositionalToken = identifier;

                    // Pass through unchanged.
                    result.Append('{');
                    result.Append(content);
                    result.Append('}');
                    i = closingBrace + 1;
                }
                else if (isValidIdentifier)
                {
                    // Named placeholder.
                    hasNamedPlaceholders = true;
                    if (firstNamedToken == null)
                        firstNamedToken = identifier;

                    if (!isKnownProperty)
                    {
                        // Unresolved property name.
                        string availableProps = string.Join(", ", knownPropertyNames);
                        diagnostics.Add(new NormalizationDiagnostic(
                            NormalizationDiagnosticKind.UnresolvedProperty,
                            $"Named placeholder '{{{identifier}}}' does not match any property on the entity. " +
                            $"Available properties: {availableProps}",
                            token: identifier));

                        // Still rewrite to a placeholder so the rest of the format string can be parsed.
                        int index = GetOrAssignIndex(identifier, propertyToIndex, sourceProperties);
                        AppendPositionalPlaceholder(result, index, formatSpecifier);
                        i = closingBrace + 1;
                    }
                    else
                    {
                        // Valid named placeholder — resolve and rewrite.
                        int index = GetOrAssignIndex(identifier, propertyToIndex, sourceProperties);
                        AppendPositionalPlaceholder(result, index, formatSpecifier);
                        i = closingBrace + 1;
                    }
                }
                else
                {
                    // Not a valid identifier (contains invalid chars). Pass through as literal.
                    result.Append('{');
                    result.Append(content);
                    result.Append('}');
                    i = closingBrace + 1;
                }
            }
            else
            {
                result.Append(formatString[i]);
                i++;
            }
        }

        // Check for mixed named and positional placeholders.
        if (hasNamedPlaceholders && hasPositionalPlaceholders)
        {
            diagnostics.Add(new NormalizationDiagnostic(
                NormalizationDiagnosticKind.MixedNamedAndPositional,
                $"Format string mixes named placeholders (e.g., {{{firstNamedToken}}}) " +
                $"with positional placeholders (e.g., {{{firstPositionalToken}}}). " +
                $"Use all named or all positional placeholders.",
                token: firstNamedToken));
        }

        bool wasNormalized = hasNamedPlaceholders;

        return new NormalizationResult(
            result.ToString(),
            sourceProperties.ToArray(),
            wasNormalized,
            diagnostics);
    }

    /// <summary>
    /// Finds the index of the closing brace <c>}</c> matching the opening brace at <paramref name="openIndex"/>.
    /// </summary>
    /// <returns>The index of the closing brace, or -1 if not found.</returns>
    private static int FindClosingBrace(string str, int openIndex)
    {
        for (int j = openIndex + 1; j < str.Length; j++)
        {
            if (str[j] == '}')
                return j;

            // Nested opening braces are not supported in this format — just keep scanning.
        }
        return -1;
    }

    /// <summary>
    /// Extracts the identifier portion from placeholder content (everything before the first <c>:</c>).
    /// </summary>
    private static string ExtractIdentifier(string content)
    {
        int colonIndex = content.IndexOf(':');
        return colonIndex >= 0 ? content.Substring(0, colonIndex) : content;
    }

    /// <summary>
    /// Extracts the format specifier portion from placeholder content (everything after the first <c>:</c>),
    /// or <c>null</c> if there is no format specifier.
    /// </summary>
    private static string? ExtractFormatSpecifier(string content)
    {
        int colonIndex = content.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < content.Length - 1)
            return content.Substring(colonIndex + 1);
        return null;
    }

    /// <summary>
    /// Determines whether the string is a valid C# identifier: starts with a letter or underscore,
    /// followed by zero or more letters, digits, or underscores.
    /// </summary>
    private static bool IsValidCSharpIdentifier(string s)
    {
        if (s.Length == 0)
            return false;

        char first = s[0];
        if (first != '_' && !char.IsLetter(first))
            return false;

        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '_' && !char.IsLetterOrDigit(c))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the string is parseable as a non-negative integer (zero or positive integer with no leading sign).
    /// </summary>
    private static bool IsNonNegativeInteger(string s)
    {
        if (s.Length == 0)
            return false;

        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the existing index for a property name, or assigns the next available index
    /// if this is the property's first appearance.
    /// </summary>
    private static int GetOrAssignIndex(string propertyName, Dictionary<string, int> propertyToIndex, List<string> sourceProperties)
    {
        if (propertyToIndex.TryGetValue(propertyName, out int existingIndex))
            return existingIndex;

        int newIndex = sourceProperties.Count;
        propertyToIndex[propertyName] = newIndex;
        sourceProperties.Add(propertyName);
        return newIndex;
    }

    /// <summary>
    /// Appends a positional placeholder <c>{N}</c> or <c>{N:specifier}</c> to the string builder.
    /// </summary>
    private static void AppendPositionalPlaceholder(StringBuilder sb, int index, string? formatSpecifier)
    {
        sb.Append('{');
        sb.Append(index);
        if (formatSpecifier != null)
        {
            sb.Append(':');
            sb.Append(formatSpecifier);
        }
        sb.Append('}');
    }
}
