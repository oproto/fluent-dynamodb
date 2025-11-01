namespace Oproto.FluentDynamoDb.SourceGenerator.Models;

/// <summary>
/// Represents a property in a projection model.
/// </summary>
internal class ProjectionPropertyModel
{
    /// <summary>
    /// Gets or sets the property name in the projection class.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the property type.
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the DynamoDB attribute name.
    /// </summary>
    public string AttributeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether the property is nullable.
    /// </summary>
    public bool IsNullable { get; set; }
    
    /// <summary>
    /// Gets or sets the corresponding property on the source entity.
    /// </summary>
    public PropertyModel? SourceProperty { get; set; }
}
