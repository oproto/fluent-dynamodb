namespace Oproto.FluentDynamoDb.Streams.Processing;

/// <summary>
/// Defines the matching strategy for discriminator values in stream processing.
/// </summary>
/// <remarks>
/// <para>
/// This enum specifies how discriminator values from stream records should be matched
/// against entity type configurations. The strategy is determined at compile time based
/// on the discriminator pattern specified in the entity's DynamoDbTableAttribute.
/// </para>
/// <para>
/// The matching strategy enables flexible entity identification in single-table designs
/// where discriminator values may follow various patterns (exact values, prefixes, suffixes, etc.).
/// </para>
/// </remarks>
public enum DiscriminatorStrategy
{
    /// <summary>
    /// Exact string match - the discriminator value must exactly equal the configured value.
    /// </summary>
    /// <remarks>
    /// Used when the discriminator pattern contains no wildcards.
    /// Example: "User" matches only "User"
    /// </remarks>
    ExactMatch,

    /// <summary>
    /// Prefix match - the discriminator value must start with the configured value.
    /// </summary>
    /// <remarks>
    /// Used when the discriminator pattern has a trailing wildcard.
    /// Example: "USER#*" matches "USER#123", "USER#456", etc.
    /// </remarks>
    StartsWith,

    /// <summary>
    /// Suffix match - the discriminator value must end with the configured value.
    /// </summary>
    /// <remarks>
    /// Used when the discriminator pattern has a leading wildcard.
    /// Example: "*#USER" matches "ADMIN#USER", "GUEST#USER", etc.
    /// </remarks>
    EndsWith,

    /// <summary>
    /// Contains match - the discriminator value must contain the configured value.
    /// </summary>
    /// <remarks>
    /// Used when the discriminator pattern has wildcards at both ends.
    /// Example: "*#USER#*" matches "ADMIN#USER#123", "GUEST#USER#456", etc.
    /// </remarks>
    Contains
}

