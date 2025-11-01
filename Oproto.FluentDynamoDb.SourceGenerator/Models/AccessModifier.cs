namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Specifies the visibility modifier for generated code elements.
/// NOTE: This is a duplicate of the enum in Oproto.FluentDynamoDb.Attributes for use within the source generator.
/// </summary>
internal enum AccessModifier
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
