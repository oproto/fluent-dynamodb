namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface for projection models with discriminator support.
/// Enables filtering items by entity type in multi-entity tables.
/// </summary>
/// <typeparam name="TSelf">The implementing projection type.</typeparam>
public interface IDiscriminatedProjection<TSelf> : IProjectionModel<TSelf> 
    where TSelf : IDiscriminatedProjection<TSelf>
{
    /// <summary>
    /// Gets the discriminator property name in DynamoDB (e.g., "entity_type").
    /// </summary>
    static abstract string? DiscriminatorProperty { get; }
    
    /// <summary>
    /// Gets the expected discriminator value for this projection type.
    /// </summary>
    static abstract string? DiscriminatorValue { get; }
}
