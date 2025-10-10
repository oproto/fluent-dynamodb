using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Interface for request builders that support key specification.
/// Provides access to key setting mechanism for extension methods.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder implementing this interface.</typeparam>
public interface IWithKey<out TBuilder>
{
    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    TBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction);

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    TBuilder Self { get; }
}