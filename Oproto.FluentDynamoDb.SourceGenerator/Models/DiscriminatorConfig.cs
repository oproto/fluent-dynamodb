namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents discriminator configuration for an entity or GSI.
/// </summary>
internal class DiscriminatorConfig
{
    /// <summary>
    /// Gets or sets the property name containing the discriminator (e.g., "entity_type", "SK", "PK").
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exact value to match (if using exact match strategy).
    /// </summary>
    public string? ExactValue { get; set; }

    /// <summary>
    /// Gets or sets the pattern to match (if using pattern match strategy).
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Gets or sets the matching strategy to use.
    /// </summary>
    public DiscriminatorStrategy Strategy { get; set; }

    /// <summary>
    /// Gets a value indicating whether this discriminator configuration is valid.
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(PropertyName) && 
                          (Strategy != DiscriminatorStrategy.None);
}

/// <summary>
/// Defines the strategy for matching discriminator values.
/// </summary>
internal enum DiscriminatorStrategy
{
    /// <summary>
    /// No discriminator matching.
    /// </summary>
    None,

    /// <summary>
    /// Exact string match (e.g., "USER").
    /// </summary>
    ExactMatch,

    /// <summary>
    /// Starts with pattern (e.g., "USER#*" matches "USER#123").
    /// </summary>
    StartsWith,

    /// <summary>
    /// Ends with pattern (e.g., "*#USER" matches "TENANT#USER").
    /// </summary>
    EndsWith,

    /// <summary>
    /// Contains pattern (e.g., "*#USER#*" matches "TENANT#abc#USER#123").
    /// </summary>
    Contains,

    /// <summary>
    /// Complex pattern with multiple wildcards.
    /// </summary>
    Complex
}
