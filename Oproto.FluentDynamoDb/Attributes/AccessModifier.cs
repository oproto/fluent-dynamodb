namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies the visibility modifier for generated code elements.
/// </summary>
public enum AccessModifier
{
    /// <summary>
    /// Public visibility - accessible from any code.
    /// </summary>
    Public,

    /// <summary>
    /// Internal visibility - accessible only within the same assembly.
    /// </summary>
    Internal,

    /// <summary>
    /// Protected visibility - accessible only within the containing class and derived classes.
    /// </summary>
    Protected,

    /// <summary>
    /// Private visibility - accessible only within the containing class.
    /// </summary>
    Private
}
