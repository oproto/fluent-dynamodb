namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a single exclusion guard to generate in a less-specific entity's MatchesEntity method.
/// When a more-specific entity's pattern overlaps with this entity's pattern, an exclusion check
/// ensures the less-specific entity does not claim items that belong to the more-specific entity.
/// </summary>
internal class ExclusionPattern
{
    /// <summary>
    /// The entity class name that owns the more-specific pattern (for comments in generated code).
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// The original pattern string of the more-specific entity.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// The matching strategy for the exclusion check (StartsWith, EndsWith, Contains, ExactMatch).
    /// </summary>
    public DiscriminatorStrategy Strategy { get; set; }

    /// <summary>
    /// The literal text to use in the exclusion check (extracted from the pattern).
    /// </summary>
    public string LiteralText { get; set; } = string.Empty;
}
