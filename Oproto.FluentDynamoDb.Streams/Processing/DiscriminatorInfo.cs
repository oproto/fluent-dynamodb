namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Contains discriminator metadata for an entity type in stream processing.
/// </summary>
/// <remarks>
/// <para>
/// This class stores the discriminator configuration for an entity type, including the property name,
/// matching pattern, strategy, and value. It's used by the generated StreamDiscriminatorRegistry
/// to enable automatic discriminator lookup in table-integrated stream processing.
/// </para>
/// <para>
/// The discriminator information is extracted from the entity's DynamoDbTableAttribute at compile time
/// and stored in a static registry for AOT-compatible, reflection-free lookup.
/// </para>
/// </remarks>
public sealed class DiscriminatorInfo
{
    /// <summary>
    /// Gets or initializes the name of the discriminator property in the DynamoDB item.
    /// </summary>
    /// <remarks>
    /// This is typically a property like "EntityType", "SK", or any other attribute that
    /// identifies the entity type in a single-table design.
    /// </remarks>
    /// <example>
    /// "EntityType", "SK", "Type"
    /// </example>
    public required string Property { get; init; }

    /// <summary>
    /// Gets or initializes the discriminator pattern with wildcards (if applicable).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property contains the full pattern including wildcards for pattern-based matching.
    /// If the discriminator uses exact matching, this will be null and Value should be used instead.
    /// </para>
    /// <para>
    /// Pattern examples:
    /// - "USER#*" - prefix match
    /// - "*#USER" - suffix match
    /// - "*#USER#*" - contains match
    /// - null - exact match (use Value instead)
    /// </para>
    /// </remarks>
    /// <example>
    /// "USER#*", "*#ORDER", "*#PRODUCT#*", null
    /// </example>
    public string? Pattern { get; init; }

    /// <summary>
    /// Gets or initializes the matching strategy for the discriminator.
    /// </summary>
    /// <remarks>
    /// The strategy determines how the discriminator value is matched against stream records.
    /// This is derived from the Pattern property at compile time.
    /// </remarks>
    public required DiscriminatorStrategy Strategy { get; init; }

    /// <summary>
    /// Gets or initializes the discriminator value (without wildcards).
    /// </summary>
    /// <remarks>
    /// <para>
    /// For exact match strategies, this is the complete discriminator value.
    /// For pattern-based strategies, this is the pattern with wildcards removed.
    /// </para>
    /// <para>
    /// Examples:
    /// - Exact match: "User" (Pattern is null)
    /// - Prefix match: "USER#" (Pattern is "USER#*")
    /// - Suffix match: "#USER" (Pattern is "*#USER")
    /// - Contains match: "#USER#" (Pattern is "*#USER#*")
    /// </para>
    /// </remarks>
    /// <example>
    /// "User", "USER#", "#ORDER", "#PRODUCT#"
    /// </example>
    public required string Value { get; init; }
}

