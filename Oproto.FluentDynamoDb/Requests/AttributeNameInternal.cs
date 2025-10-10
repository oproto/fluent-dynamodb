namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Internal class for managing attribute name mappings in DynamoDB expressions.
/// This class handles the collection and management of expression attribute names
/// that are used to avoid conflicts with DynamoDB reserved words.
/// </summary>
public class AttributeNameInternal
{
    /// <summary>
    /// Gets the dictionary of attribute name mappings that will be used in the DynamoDB request.
    /// </summary>
    public Dictionary<string, string> AttributeNames { get; init; } = new Dictionary<string, string>();
    
    /// <summary>
    /// Adds multiple attribute name mappings from a dictionary.
    /// </summary>
    /// <param name="attributeNames">Dictionary of parameter names to attribute names.</param>
    public void WithAttributes(Dictionary<string,string> attributeNames)
    {
        foreach (var attr in attributeNames)
        {
            AttributeNames.Add(attr.Key, attr.Value);
        }
    }
    
    /// <summary>
    /// Adds multiple attribute name mappings using a configuration action.
    /// </summary>
    /// <param name="attributeNameFunc">Action that configures the attribute name mappings.</param>
    public void WithAttributes(Action<Dictionary<string,string>> attributeNameFunc)
    {
        var attributeNames = new Dictionary<string, string>();
        attributeNameFunc(attributeNames);
        WithAttributes(attributeNames);
    }

    /// <summary>
    /// Adds a single attribute name mapping.
    /// </summary>
    /// <param name="parameterName">The parameter name to use in expressions.</param>
    /// <param name="attributeName">The actual attribute name in the table.</param>
    public void WithAttribute(string parameterName, string attributeName)
    {
        AttributeNames.Add(parameterName,attributeName);
    }
}