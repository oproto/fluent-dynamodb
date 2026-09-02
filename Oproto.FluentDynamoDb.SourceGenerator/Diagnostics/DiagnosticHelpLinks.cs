namespace Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

/// <summary>
/// Centralized URL format for diagnostic help links.
/// </summary>
internal static class DiagnosticHelpLinks
{
    /// <summary>
    /// Base URL format for diagnostic documentation pages.
    /// Use with string.Format to produce the full URL for a diagnostic code.
    /// </summary>
    internal const string BaseUrlFormat = "https://fluentdynamodb.dev/diagnostics/{0}";
}
