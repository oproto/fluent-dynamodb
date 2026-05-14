using System.Text;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Builds DynamoDB document paths from expression member chains.
/// Handles nested properties and list indices for filter, condition, and update expressions.
/// </summary>
/// <remarks>
/// <para><strong>Overview:</strong></para>
/// <para>
/// DocumentPathBuilder constructs DynamoDB document paths for accessing nested attributes.
/// It supports property access (e.g., <c>#address.#city</c>) and list index access 
/// (e.g., <c>#tags[0]</c>), as well as mixed paths (e.g., <c>#items[0].#name</c>).
/// </para>
/// 
/// <para><strong>Path Segment Types:</strong></para>
/// <list type="table">
/// <listheader><term>Type</term><description>Format</description><description>Example</description></listheader>
/// <item><term>Property</term><description>#attributeName</description><description>#address, #city</description></item>
/// <item><term>Index</term><description>[n]</description><description>[0], [1], [2]</description></item>
/// </list>
/// 
/// <para><strong>Path Building Rules:</strong></para>
/// <list type="bullet">
/// <item><description>Properties are separated by dots: <c>#address.#city</c></description></item>
/// <item><description>Indices are appended directly without dots: <c>#tags[0]</c></description></item>
/// <item><description>Mixed paths combine both: <c>#items[0].#name</c></description></item>
/// </list>
/// 
/// <para><strong>Usage:</strong></para>
/// <para>
/// This class is used internally by ExpressionTranslator and UpdateExpressionTranslator
/// to build document paths for nested property access in lambda expressions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Build a nested property path: #address.#city
/// var builder = new DocumentPathBuilder(attributeNames);
/// builder.AddProperty("Address", "address");
/// builder.AddProperty("City", "city");
/// var path = builder.Build(); // Returns "#attr0.#attr1"
/// 
/// // Build a list index path: #tags[0]
/// var builder = new DocumentPathBuilder(attributeNames);
/// builder.AddProperty("Tags", "tags");
/// builder.AddIndex(0);
/// var path = builder.Build(); // Returns "#attr0[0]"
/// 
/// // Build a mixed path: #items[0].#name
/// var builder = new DocumentPathBuilder(attributeNames);
/// builder.AddProperty("Items", "items");
/// builder.AddIndex(0);
/// builder.AddProperty("Name", "name");
/// var path = builder.Build(); // Returns "#attr0[0].#attr1"
/// </code>
/// </example>
internal class DocumentPathBuilder
{
    private readonly AttributeNameInternal _attributeNames;
    private readonly List<PathSegment> _segments = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPathBuilder"/> class.
    /// </summary>
    /// <param name="attributeNames">The attribute name helper for registering attribute name placeholders.</param>
    /// <exception cref="ArgumentNullException">Thrown when attributeNames is null.</exception>
    public DocumentPathBuilder(AttributeNameInternal attributeNames)
    {
        _attributeNames = attributeNames ?? throw new ArgumentNullException(nameof(attributeNames));
    }

    /// <summary>
    /// Adds a property segment to the path.
    /// </summary>
    /// <param name="propertyName">The C# property name (used for generating unique placeholder).</param>
    /// <param name="attributeName">The DynamoDB attribute name to map to. If null, uses propertyName.</param>
    /// <returns>The placeholder generated for this property (e.g., "#attr0").</returns>
    /// <exception cref="ArgumentException">Thrown when propertyName is null or empty.</exception>
    public string AddProperty(string propertyName, string? attributeName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));

        var attrName = attributeName ?? propertyName;
        
        // Check if this attribute name already has a placeholder
        var existingPlaceholder = FindExistingPlaceholder(attrName);
        if (existingPlaceholder != null)
        {
            _segments.Add(new PathSegment(PathSegmentType.Property, existingPlaceholder));
            return existingPlaceholder;
        }
        
        var placeholder = GenerateAttributeNamePlaceholder();
        _attributeNames.WithAttribute(placeholder, attrName);
        _segments.Add(new PathSegment(PathSegmentType.Property, placeholder));
        return placeholder;
    }

    /// <summary>
    /// Finds an existing placeholder for the given attribute name.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name to look for.</param>
    /// <returns>The existing placeholder if found, null otherwise.</returns>
    private string? FindExistingPlaceholder(string attributeName)
    {
        foreach (var kvp in _attributeNames.AttributeNames)
        {
            if (kvp.Value == attributeName)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Adds a list index segment to the path.
    /// </summary>
    /// <param name="index">The zero-based index of the list element.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is negative.</exception>
    public void AddIndex(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative.");

        _segments.Add(new PathSegment(PathSegmentType.Index, $"[{index}]"));
    }

    /// <summary>
    /// Builds the complete document path string.
    /// </summary>
    /// <returns>
    /// The formatted document path string (e.g., "#address.#city", "#tags[0]", "#items[0].#name").
    /// Returns an empty string if no segments have been added.
    /// </returns>
    public string Build()
    {
        if (_segments.Count == 0)
            return string.Empty;

        var result = new StringBuilder();
        
        for (int i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            
            if (segment.Type == PathSegmentType.Index)
            {
                // Index segments are appended directly without separator
                result.Append(segment.Value);
            }
            else
            {
                // Property segments need dot separator (except for first segment)
                if (i > 0 && _segments[i - 1].Type != PathSegmentType.Index)
                {
                    result.Append('.');
                }
                else if (i > 0 && _segments[i - 1].Type == PathSegmentType.Index)
                {
                    // Property after index needs dot separator
                    result.Append('.');
                }
                result.Append(segment.Value);
            }
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Gets the number of segments in the path.
    /// </summary>
    public int SegmentCount => _segments.Count;

    /// <summary>
    /// Generates a unique attribute name placeholder.
    /// </summary>
    private string GenerateAttributeNamePlaceholder()
    {
        var count = _attributeNames.AttributeNames.Count;
        return count < 10 
            ? string.Concat("#attr", count.ToString()) 
            : $"#attr{count}";
    }

    /// <summary>
    /// Represents a segment in a document path.
    /// </summary>
    private readonly struct PathSegment
    {
        public PathSegmentType Type { get; }
        public string Value { get; }

        public PathSegment(PathSegmentType type, string value)
        {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// The type of path segment.
    /// </summary>
    private enum PathSegmentType
    {
        /// <summary>Property access segment (e.g., #address).</summary>
        Property,
        /// <summary>List index segment (e.g., [0]).</summary>
        Index
    }
}
