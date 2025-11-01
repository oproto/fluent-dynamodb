using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Validates advanced type configurations and reports diagnostics for invalid configurations.
/// </summary>
internal class AdvancedTypeValidator
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the diagnostics collected during validation.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Validates advanced type configuration for a property.
    /// </summary>
    /// <param name="property">The property to validate.</param>
    /// <param name="advancedType">The advanced type information.</param>
    /// <param name="hasJsonSerializerPackage">Whether a JSON serializer package is referenced.</param>
    /// <param name="hasBlobProviderPackage">Whether a blob provider package is referenced.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    public void ValidateProperty(
        PropertyModel property,
        AdvancedTypeInfo advancedType,
        bool hasJsonSerializerPackage,
        bool hasBlobProviderPackage,
        SemanticModel semanticModel)
    {
        // Validate TTL property type
        if (advancedType.IsTtl)
        {
            ValidateTtlPropertyType(property);
        }

        // Validate JSON blob requires serializer package
        if (advancedType.IsJsonBlob && !hasJsonSerializerPackage)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.MissingJsonSerializer,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName);
        }

        // Validate blob reference requires provider package
        if (advancedType.IsBlobReference && !hasBlobProviderPackage)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.MissingBlobProvider,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName);
        }

        // Validate attribute combinations
        ValidateAttributeCombinations(property, advancedType);

        // Validate collection types
        if (advancedType.IsSet)
        {
            ValidateSetType(property, advancedType);
        }

        // Validate nested map types have [DynamoDbEntity] for AOT compatibility
        if (advancedType.IsMap)
        {
            ValidateNestedMapType(property, semanticModel);
        }
    }

    /// <summary>
    /// Validates that TTL property is DateTime or DateTimeOffset.
    /// </summary>
    private void ValidateTtlPropertyType(PropertyModel property)
    {
        var propertyType = property.PropertyType.TrimEnd('?');

        var validTtlTypes = new[]
        {
            "DateTime", "System.DateTime",
            "DateTimeOffset", "System.DateTimeOffset"
        };

        if (!validTtlTypes.Contains(propertyType))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.InvalidTtlType,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName,
                property.PropertyType);
        }
    }

    /// <summary>
    /// Validates that attribute combinations are compatible.
    /// </summary>
    private void ValidateAttributeCombinations(PropertyModel property, AdvancedTypeInfo advancedType)
    {
        // TTL cannot be combined with JsonBlob or BlobReference
        if (advancedType.IsTtl && (advancedType.IsJsonBlob || advancedType.IsBlobReference))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.IncompatibleAttributes,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName,
                "[TimeToLive] cannot be combined with [JsonBlob] or [BlobReference]");
        }

        // Map/Set/List cannot be combined with TTL
        if (advancedType.IsTtl && (advancedType.IsMap || advancedType.IsSet || advancedType.IsList))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.IncompatibleAttributes,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName,
                "[TimeToLive] cannot be used on collection types");
        }

        // JsonBlob and BlobReference can be combined - this is a valid pattern
        // When both are present, the property is serialized to JSON then stored as an external blob
        // No validation error needed for this combination
    }

    /// <summary>
    /// Validates that Set element type is supported.
    /// </summary>
    private void ValidateSetType(PropertyModel property, AdvancedTypeInfo advancedType)
    {
        if (advancedType.ElementType == null)
            return;

        var elementType = advancedType.ElementType.TrimEnd('?');

        var supportedSetTypes = new[]
        {
            "string", "System.String",
            "int", "System.Int32",
            "long", "System.Int64",
            "decimal", "System.Decimal",
            "double", "System.Double",
            "float", "System.Single",
            "byte[]", "System.Byte[]"
        };

        if (!supportedSetTypes.Contains(elementType))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.UnsupportedCollectionType,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName,
                $"HashSet<{advancedType.ElementType}>");
        }
    }

    /// <summary>
    /// Validates that nested map types have [DynamoDbEntity] attribute for AOT compatibility.
    /// </summary>
    private void ValidateNestedMapType(PropertyModel property, SemanticModel semanticModel)
    {
        // Only validate custom object maps (not Dictionary<string, string> or Dictionary<string, AttributeValue>)
        var propertyType = property.PropertyType;
        
        // Skip Dictionary types - they don't need [DynamoDbEntity]
        if (propertyType.Contains("Dictionary<"))
            return;

        // For custom types with [DynamoDbMap], verify the nested type has [DynamoDbEntity]
        // This is required for AOT compatibility - we need the nested type's generated ToDynamoDb/FromDynamoDb methods
        
        // Get the type symbol for the property
        if (property.PropertyDeclaration == null)
            return;

        var propertySymbol = semanticModel.GetDeclaredSymbol(property.PropertyDeclaration) as IPropertySymbol;
        if (propertySymbol == null)
            return;

        var nestedTypeSymbol = propertySymbol.Type;
        
        // Check if the nested type has [DynamoDbEntity] or [DynamoDbTable] attribute
        var hasEntityAttribute = nestedTypeSymbol.GetAttributes().Any(attr =>
        {
            var attrName = attr.AttributeClass?.Name;
            return attrName == "DynamoDbEntityAttribute" || 
                   attrName == "DynamoDbEntity" ||
                   attrName == "DynamoDbTableAttribute" ||
                   attrName == "DynamoDbTable";
        });

        if (!hasEntityAttribute)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.NestedMapTypeMissingEntity,
                property.PropertyDeclaration?.Identifier.GetLocation(),
                property.PropertyName,
                propertyType);
        }
    }

    /// <summary>
    /// Validates that only one TTL field exists per entity.
    /// </summary>
    /// <param name="entityModel">The entity model to validate.</param>
    public void ValidateEntityTtlFields(EntityModel entityModel)
    {
        var ttlProperties = entityModel.Properties
            .Where(p => p.AdvancedType?.IsTtl == true)
            .ToArray();

        if (ttlProperties.Length > 1)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.MultipleTtlFields,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName);
        }
    }

    /// <summary>
    /// Clears all collected diagnostics.
    /// </summary>
    public void ClearDiagnostics()
    {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Reports a diagnostic.
    /// </summary>
    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }
}
