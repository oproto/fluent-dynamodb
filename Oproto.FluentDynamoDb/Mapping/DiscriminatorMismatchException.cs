using System;

namespace Oproto.FluentDynamoDb.Mapping;

/// <summary>
/// Exception thrown when a DynamoDB item's discriminator value doesn't match the expected entity type.
/// This occurs when attempting to hydrate a projection model from an item with an incompatible discriminator.
/// </summary>
public class DiscriminatorMismatchException : Exception
{
    /// <summary>
    /// Gets the expected discriminator value.
    /// </summary>
    public string? ExpectedDiscriminator { get; }
    
    /// <summary>
    /// Gets the actual discriminator value found in the item.
    /// </summary>
    public string? ActualDiscriminator { get; }
    
    /// <summary>
    /// Gets the projection type that was being hydrated.
    /// </summary>
    public Type? ProjectionType { get; }
    
    /// <summary>
    /// Initializes a new instance of the DiscriminatorMismatchException class.
    /// </summary>
    public DiscriminatorMismatchException() : this("Discriminator value mismatch.")
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the DiscriminatorMismatchException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DiscriminatorMismatchException(string message) : base(message)
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the DiscriminatorMismatchException class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DiscriminatorMismatchException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the DiscriminatorMismatchException class with detailed context information.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="expectedDiscriminator">The expected discriminator value.</param>
    /// <param name="actualDiscriminator">The actual discriminator value found.</param>
    /// <param name="projectionType">The projection type being hydrated.</param>
    public DiscriminatorMismatchException(
        string message,
        string? expectedDiscriminator,
        string? actualDiscriminator,
        Type? projectionType) : base(message)
    {
        ExpectedDiscriminator = expectedDiscriminator;
        ActualDiscriminator = actualDiscriminator;
        ProjectionType = projectionType;
    }
    
    /// <summary>
    /// Creates a DiscriminatorMismatchException for a discriminator value mismatch.
    /// </summary>
    /// <param name="projectionType">The projection type being hydrated.</param>
    /// <param name="expectedDiscriminator">The expected discriminator value.</param>
    /// <param name="actualDiscriminator">The actual discriminator value found.</param>
    /// <returns>A new DiscriminatorMismatchException instance.</returns>
    public static DiscriminatorMismatchException Create(
        Type projectionType,
        string expectedDiscriminator,
        string? actualDiscriminator)
    {
        var message = actualDiscriminator == null
            ? $"Expected discriminator '{expectedDiscriminator}' for projection type '{projectionType.Name}', but discriminator property was not found in the item."
            : $"Expected discriminator '{expectedDiscriminator}' for projection type '{projectionType.Name}', but found '{actualDiscriminator}'.";
        
        return new DiscriminatorMismatchException(
            message,
            expectedDiscriminator,
            actualDiscriminator,
            projectionType);
    }
}
