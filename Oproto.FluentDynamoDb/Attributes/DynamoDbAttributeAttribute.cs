namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Maps a property to a DynamoDB attribute with a specific name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbAttributeAttribute : Attribute
{
    /// <summary>
    /// Gets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; }

    /// <summary>
    /// Initializes a new instance of the DynamoDbAttributeAttribute class.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name.</param>
    public DynamoDbAttributeAttribute(string attributeName)
    {
        AttributeName = attributeName;
    }
}