namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a secondary cross-key constraint for compound discrimination.
/// Either a positive match (entity WITH a cross-key pattern) or an exclusion guard
/// (entity WITHOUT a cross-key pattern that must negate the other entity's pattern).
/// </summary>
internal class CompoundConstraint
{
    /// <summary>
    /// The DynamoDB attribute name of the cross-key property (e.g., "pk" when discriminator is on "sk").
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// The pattern string for the cross-key match (e.g., "PLATFORM#*").
    /// For exclusion guards, this is the OTHER entity's cross-key pattern being negated.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// The matching strategy derived from the pattern (StartsWith, ExactMatch, EndsWith, Contains).
    /// </summary>
    public DiscriminatorStrategy Strategy { get; set; }

    /// <summary>
    /// The literal text to use in the string operation (pattern with wildcards removed).
    /// </summary>
    public string LiteralText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an exclusion guard (negate match) rather than a positive compound check.
    /// When true, MatchesEntity returns false if the cross-key value MATCHES the pattern.
    /// When false, MatchesEntity returns true only if the cross-key value matches the pattern.
    /// </summary>
    public bool IsExclusion { get; set; }

    /// <summary>
    /// The entity class name whose pattern this exclusion negates (for generated code comments).
    /// Only meaningful when IsExclusion is true.
    /// </summary>
    public string ExclusionSourceEntity { get; set; } = string.Empty;

    /// <summary>
    /// Additional exclusion guards when entity has multiple compound-resolved overlaps.
    /// Populated when this entity has a null cross-key pattern and overlaps with
    /// multiple entities that each have different cross-key patterns.
    /// </summary>
    public List<CompoundConstraint>? AdditionalExclusions { get; set; }
}
