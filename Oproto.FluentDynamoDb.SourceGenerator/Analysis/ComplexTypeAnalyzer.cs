using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes properties for complex DynamoDB type support including Maps, Sets, Lists,
/// TTL fields, JSON blobs, and blob references.
/// </summary>
internal class ComplexTypeAnalyzer
{
    /// <summary>
    /// Analyzes a property to detect complex type information.
    /// </summary>
    /// <param name="property">The property model to analyze.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <returns>Complex type information for the property.</returns>
    public ComplexTypeInfo AnalyzeProperty(PropertyModel property, SemanticModel semanticModel)
    {
        var info = new ComplexTypeInfo
        {
            PropertyName = property.PropertyName
        };

        // Detect collection types
        info.IsMap = IsMapType(property, semanticModel);
        info.IsSet = IsSetType(property, semanticModel);
        info.IsList = IsListType(property, semanticModel);
        
        // Detect List<T> with [DynamoDbMap] - these are lists of nested entities
        info.IsListOfMaps = IsListOfMapsType(property, semanticModel);

        // Detect special attributes
        info.IsTtl = HasAttribute(property, "TimeToLiveAttribute");
        info.IsJsonBlob = HasAttribute(property, "JsonBlobAttribute");

        // Detect BlobStorage attribute and extract configuration
        var blobStorageConfig = ExtractBlobStorageConfig(property);
        info.IsBlobStorage = blobStorageConfig.HasAttribute;
        info.BlobStorageLazyLoad = blobStorageConfig.LazyLoad;
        info.BlobDataInnerType = blobStorageConfig.InnerType;

        // Extract element type for collections
        if (info.IsSet || info.IsList || info.IsListOfMaps)
        {
            info.ElementType = ExtractElementType(property.PropertyType);
        }

    
        return info;
    }

    /// <summary>
    /// Extracts BlobStorage attribute configuration from a property.
    /// </summary>
    private (bool HasAttribute, bool LazyLoad, string? InnerType) ExtractBlobStorageConfig(PropertyModel property)
    {
        if (property.PropertyDeclaration == null)
            return (false, false, null);

        var attributeLists = property.PropertyDeclaration.AttributeLists;
        if (attributeLists.Count == 0)
            return (false, false, null);

        // Find BlobStorage attribute
        var blobStorageAttr = attributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var attributeNameText = attr.Name.ToString();
                return attributeNameText == "BlobStorageAttribute" ||
                       attributeNameText == "BlobStorage" ||
                       attributeNameText.EndsWith(".BlobStorageAttribute") ||
                       attributeNameText.EndsWith(".BlobStorage");
            });

        if (blobStorageAttr == null)
            return (false, false, null);

        // Extract LazyLoad property
        var lazyLoad = false;
        if (blobStorageAttr.ArgumentList != null)
        {
            foreach (var arg in blobStorageAttr.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "LazyLoad" &&
                    arg.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax lazyLoadLiteral)
                {
                    lazyLoad = bool.Parse(lazyLoadLiteral.Token.ValueText);
                }
            }
        }

        // Extract inner type from BlobData<T>
        var innerType = ExtractBlobDataInnerType(property.PropertyType);

        return (true, lazyLoad, innerType);
    }

    /// <summary>
    /// Extracts the inner type T from BlobData&lt;T&gt;.
    /// </summary>
    private string? ExtractBlobDataInnerType(string propertyType)
    {
        // Handle nullable types like BlobData<T>?
        var baseType = propertyType.TrimEnd('?');

        // Check if it's BlobData<T>
        if (!baseType.StartsWith("BlobData<") && 
            !baseType.StartsWith("Oproto.FluentDynamoDb.Providers.BlobStorage.BlobData<"))
            return null;

        // Extract the inner type
        var startIndex = baseType.IndexOf('<') + 1;
        var endIndex = baseType.LastIndexOf('>');
        if (endIndex > startIndex)
        {
            return baseType.Substring(startIndex, endIndex - startIndex).Trim();
        }

        return null;
    }

    /// <summary>
    /// Determines if a property is a Map type (Dictionary or custom object with [DynamoDbMap]).
    /// Note: List<T> with [DynamoDbMap] is NOT a map type - it's a list of maps.
    /// </summary>
    private bool IsMapType(PropertyModel property, SemanticModel semanticModel)
    {
        var propertyType = property.PropertyType;

        // Check for Dictionary<string, string> or Dictionary<string, AttributeValue>
        if (propertyType.StartsWith("Dictionary<") || 
            propertyType.StartsWith("System.Collections.Generic.Dictionary<"))
        {
            return true;
        }

        // Check for custom class with [DynamoDbMap] attribute
        // BUT exclude List<T> types - those are lists of maps, not maps themselves
        if (HasAttribute(property, "DynamoDbMapAttribute"))
        {
            // If it's a List<T>, it's not a map type - it's a list of maps
            if (propertyType.StartsWith("List<") || 
                propertyType.StartsWith("System.Collections.Generic.List<") ||
                propertyType.StartsWith("IList<") ||
                propertyType.StartsWith("ICollection<") ||
                propertyType.StartsWith("IEnumerable<"))
            {
                return false;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a property is a List of nested entities with [DynamoDbMap] attribute.
    /// These are serialized as DynamoDB List (L) containing Map (M) elements.
    /// </summary>
    private bool IsListOfMapsType(PropertyModel property, SemanticModel semanticModel)
    {
        var propertyType = property.PropertyType;

        // Must be a List<T> type
        if (!propertyType.StartsWith("List<") && 
            !propertyType.StartsWith("System.Collections.Generic.List<") &&
            !propertyType.StartsWith("IList<") &&
            !propertyType.StartsWith("ICollection<") &&
            !propertyType.StartsWith("IEnumerable<"))
        {
            return false;
        }

        // Must have [DynamoDbMap] attribute
        return HasAttribute(property, "DynamoDbMapAttribute");
    }

    /// <summary>
    /// Determines if a property is a Set type (HashSet&lt;T&gt;).
    /// </summary>
    private bool IsSetType(PropertyModel property, SemanticModel semanticModel)
    {
        var propertyType = property.PropertyType;

        // Check for HashSet<T>
        if (propertyType.StartsWith("HashSet<") || 
            propertyType.StartsWith("System.Collections.Generic.HashSet<"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a property is a List type (List&lt;T&gt;).
    /// </summary>
    private bool IsListType(PropertyModel property, SemanticModel semanticModel)
    {
        var propertyType = property.PropertyType;

        // Check for List<T>
        if (propertyType.StartsWith("List<") || 
            propertyType.StartsWith("System.Collections.Generic.List<"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a property has a specific attribute.
    /// </summary>
    private bool HasAttribute(PropertyModel property, string attributeName)
    {
        if (property.PropertyDeclaration == null)
            return false;

        var attributeLists = property.PropertyDeclaration.AttributeLists;
        if (attributeLists.Count == 0)
            return false;

        var targetName = attributeName.Replace("Attribute", "");

        return attributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var attributeNameText = attr.Name.ToString();
                return attributeNameText == attributeName ||
                       attributeNameText == targetName ||
                       attributeNameText.EndsWith("." + attributeName) ||
                       attributeNameText.EndsWith("." + targetName);
            });
    }

    /// <summary>
    /// Extracts the element type from a generic collection type.
    /// </summary>
    private string ExtractElementType(string collectionType)
    {
        // Handle nullable collections like List<T>?
        var baseType = collectionType.TrimEnd('?');

        // Extract element type from generic collections
        // Examples: List<string> -> string, HashSet<int> -> int
        if (baseType.Contains('<') && baseType.Contains('>'))
        {
            var startIndex = baseType.IndexOf('<') + 1;
            var endIndex = baseType.LastIndexOf('>');
            if (endIndex > startIndex)
            {
                return baseType.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        return "object";
    }

    /// <summary>
    /// Determines the DynamoDB set type (SS, NS, or BS) based on element type.
    /// </summary>
    public string GetSetType(string elementType)
    {
        // Remove nullable annotations
        var baseType = elementType.TrimEnd('?');

        // String Set (SS)
        if (baseType == "string" || baseType == "System.String")
        {
            return "SS";
        }

        // Number Set (NS)
        if (baseType == "int" || baseType == "System.Int32" ||
            baseType == "long" || baseType == "System.Int64" ||
            baseType == "decimal" || baseType == "System.Decimal" ||
            baseType == "double" || baseType == "System.Double" ||
            baseType == "float" || baseType == "System.Single")
        {
            return "NS";
        }

        // Binary Set (BS)
        if (baseType == "byte[]" || baseType == "System.Byte[]")
        {
            return "BS";
        }

        // Unsupported type
        return "UNSUPPORTED";
    }

    /// <summary>
    /// Determines if an element type is a primitive type suitable for List conversion.
    /// </summary>
    public bool IsPrimitiveElementType(string elementType)
    {
        var baseType = elementType.TrimEnd('?');

        var primitiveTypes = new HashSet<string>
        {
            "string", "int", "long", "double", "float", "decimal", "bool",
            "DateTime", "DateTimeOffset", "Guid", "byte",
            "System.String", "System.Int32", "System.Int64", "System.Double",
            "System.Single", "System.Decimal", "System.Boolean",
            "System.DateTime", "System.DateTimeOffset", "System.Guid", "System.Byte"
        };

        return primitiveTypes.Contains(baseType);
    }
}
