using Microsoft.CodeAnalysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Result of compound promotion analysis for a single table group.
/// </summary>
internal class CompoundPromotionResult
{
    /// <summary>
    /// New diagnostics to emit (FDDB104 info diagnostics for resolved pairs).
    /// </summary>
    public List<Diagnostic> Diagnostics { get; set; } = new();

    /// <summary>
    /// Set of entity class name pairs that were resolved by compound promotion.
    /// Used to filter FDDB102/DISC004 diagnostics before reporting.
    /// Format: ordered tuple (min(nameA, nameB), max(nameA, nameB)) for stable lookup.
    /// </summary>
    public HashSet<(string, string)> ResolvedPairs { get; set; } = new();
}
