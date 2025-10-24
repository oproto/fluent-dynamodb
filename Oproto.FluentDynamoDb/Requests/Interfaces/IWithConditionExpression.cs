namespace Oproto.FluentDynamoDb.Requests.Interfaces;

/// <summary>
/// Interface for request builders that support condition expressions.
/// Provides access to internal helpers for extension methods with format string support.
/// </summary>
/// <typeparam name="TBuilder">The type of the builder implementing this interface.</typeparam>
public interface IWithConditionExpression<out TBuilder>
{
    /// <summary>
    /// Gets the internal attribute value helper for parameter generation in extension methods.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    AttributeValueInternal GetAttributeValueHelper();

    /// <summary>
    /// Gets the internal attribute name helper for attribute name mapping in extension methods.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    AttributeNameInternal GetAttributeNameHelper();

    /// <summary>
    /// Sets the condition expression on the builder.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    TBuilder SetConditionExpression(string expression);

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    TBuilder Self { get; }
}