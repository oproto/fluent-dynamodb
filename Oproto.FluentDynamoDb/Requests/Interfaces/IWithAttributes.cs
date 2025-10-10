using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Interface for request builders that support attribute name mappings.
/// Provides access to the internal attribute name helper for extension methods.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder implementing this interface.</typeparam>
public interface IWithAttributeNames<out TBuilder>
{
    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    AttributeNameInternal GetAttributeNameHelper();

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    TBuilder Self { get; }
}

/// <summary>
/// Interface for request builders that support attribute value mappings.
/// Provides access to the internal attribute value helper for extension methods.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder implementing this interface.</typeparam>
public interface IWithAttributeValues<out TBuilder>
{
    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    AttributeValueInternal GetAttributeValueHelper();

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    TBuilder Self { get; }
}