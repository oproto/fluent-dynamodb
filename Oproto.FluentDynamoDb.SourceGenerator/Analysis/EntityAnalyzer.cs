using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes class declarations to extract DynamoDB entity information.
/// </summary>
public class EntityAnalyzer
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the diagnostics collected during analysis.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Analyzes a class declaration and extracts entity model information.
    /// </summary>
    /// <param name="classDecl">The class declaration to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>The extracted entity model, or null if analysis failed.</returns>
    public EntityModel? AnalyzeEntity(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        _diagnostics.Clear();

        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
            return null;

        // Check if class is partial
        if (!IsPartialClass(classDecl))
        {
            ReportDiagnostic(DiagnosticDescriptors.EntityMustBePartial, classDecl.Identifier.GetLocation(), classSymbol.Name);
            return null;
        }

        var entityModel = new EntityModel
        {
            ClassName = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            ClassDeclaration = classDecl
        };

        // Extract table information
        if (!ExtractTableInfo(classDecl, semanticModel, entityModel))
            return null;

        // Extract property information
        ExtractProperties(classDecl, semanticModel, entityModel);

        // Validate entity configuration
        ValidateEntityModel(entityModel);

        // Extract index information
        ExtractIndexes(entityModel);

        // Extract relationship information
        ExtractRelationships(classDecl, semanticModel, entityModel);

        return _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error) ? null : entityModel;
    }

    private bool IsPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.ValueText == "partial");
    }

    private bool ExtractTableInfo(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var tableAttribute = GetAttribute(classDecl, semanticModel, "DynamoDbTableAttribute");
        if (tableAttribute == null)
            return false;

        // Extract table name from constructor argument
        if (tableAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax tableNameLiteral)
        {
            entityModel.TableName = tableNameLiteral.Token.ValueText;
        }

        // Extract entity discriminator from named argument
        var discriminatorArg = tableAttribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "EntityDiscriminator");
        
        if (discriminatorArg?.Expression is LiteralExpressionSyntax discriminatorLiteral)
        {
            entityModel.EntityDiscriminator = discriminatorLiteral.Token.ValueText;
        }

        return !string.IsNullOrEmpty(entityModel.TableName);
    }

    private void ExtractProperties(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var properties = new List<PropertyModel>();

        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertyModel = AnalyzeProperty(member, semanticModel);
            if (propertyModel != null)
            {
                properties.Add(propertyModel);
            }
        }

        entityModel.Properties = properties.ToArray();
    }

    private PropertyModel? AnalyzeProperty(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel)
    {
        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDecl) as IPropertySymbol;
        if (propertySymbol == null)
            return null;

        var propertyModel = new PropertyModel
        {
            PropertyName = propertySymbol.Name,
            PropertyType = propertySymbol.Type.ToDisplayString(),
            PropertyDeclaration = propertyDecl,
            IsNullable = propertySymbol.Type.CanBeReferencedByName && propertySymbol.NullableAnnotation == NullableAnnotation.Annotated,
            IsCollection = IsCollectionType(propertySymbol.Type)
        };

        // Extract DynamoDbAttribute
        var dynamoDbAttribute = GetAttribute(propertyDecl, semanticModel, "DynamoDbAttributeAttribute");
        if (dynamoDbAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax attributeNameLiteral)
        {
            propertyModel.AttributeName = attributeNameLiteral.Token.ValueText;
        }

        // Extract key attributes
        ExtractKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract GSI attributes
        ExtractGsiAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract queryable attributes
        ExtractQueryableAttributes(propertyDecl, semanticModel, propertyModel);

        // Validate property configuration
        ValidatePropertyModel(propertyModel);

        return propertyModel;
    }

    private void ExtractKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        // Check for PartitionKey attribute
        var partitionKeyAttr = GetAttribute(propertyDecl, semanticModel, "PartitionKeyAttribute");
        if (partitionKeyAttr != null)
        {
            propertyModel.IsPartitionKey = true;
            propertyModel.KeyFormat = ExtractKeyFormat(partitionKeyAttr);
        }

        // Check for SortKey attribute
        var sortKeyAttr = GetAttribute(propertyDecl, semanticModel, "SortKeyAttribute");
        if (sortKeyAttr != null)
        {
            propertyModel.IsSortKey = true;
            propertyModel.KeyFormat ??= ExtractKeyFormat(sortKeyAttr);
        }
    }

    private KeyFormatModel ExtractKeyFormat(AttributeSyntax keyAttribute)
    {
        var keyFormat = new KeyFormatModel();

        if (keyAttribute.ArgumentList != null)
        {
            foreach (var arg in keyAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "Prefix" && 
                    arg.Expression is LiteralExpressionSyntax prefixLiteral)
                {
                    keyFormat.Prefix = prefixLiteral.Token.ValueText;
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "Separator" && 
                         arg.Expression is LiteralExpressionSyntax separatorLiteral)
                {
                    keyFormat.Separator = separatorLiteral.Token.ValueText;
                }
            }
        }

        return keyFormat;
    }

    private void ExtractGsiAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var gsiAttributes = GetAttributes(propertyDecl, semanticModel, "GlobalSecondaryIndexAttribute");
        var gsiModels = new List<GlobalSecondaryIndexModel>();

        foreach (var gsiAttr in gsiAttributes)
        {
            var gsiModel = new GlobalSecondaryIndexModel();

            // Extract index name from constructor argument
            if (gsiAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax indexNameLiteral)
            {
                gsiModel.IndexName = indexNameLiteral.Token.ValueText;
            }

            // Extract named arguments
            if (gsiAttr.ArgumentList != null)
            {
                foreach (var arg in gsiAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "IsPartitionKey" when arg.Expression is LiteralExpressionSyntax partitionKeyLiteral:
                            gsiModel.IsPartitionKey = bool.Parse(partitionKeyLiteral.Token.ValueText);
                            break;
                        case "IsSortKey" when arg.Expression is LiteralExpressionSyntax sortKeyLiteral:
                            gsiModel.IsSortKey = bool.Parse(sortKeyLiteral.Token.ValueText);
                            break;
                        case "KeyFormat" when arg.Expression is LiteralExpressionSyntax keyFormatLiteral:
                            gsiModel.KeyFormat = keyFormatLiteral.Token.ValueText;
                            break;
                    }
                }
            }

            gsiModels.Add(gsiModel);
        }

        propertyModel.GlobalSecondaryIndexes = gsiModels.ToArray();
    }

    private void ExtractQueryableAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var queryableAttr = GetAttribute(propertyDecl, semanticModel, "QueryableAttribute");
        if (queryableAttr == null)
            return;

        var queryableModel = new QueryableModel();

        // Extract named arguments
        if (queryableAttr.ArgumentList != null)
        {
            foreach (var arg in queryableAttr.ArgumentList.Arguments)
            {
                switch (arg.NameEquals?.Name.Identifier.ValueText)
                {
                    case "SupportedOperations":
                        // TODO: Extract array of operations - simplified for now
                        break;
                    case "AvailableInIndexes":
                        // TODO: Extract array of index names - simplified for now
                        break;
                }
            }
        }

        propertyModel.Queryable = queryableModel;
    }

    private void ExtractIndexes(EntityModel entityModel)
    {
        var indexes = new Dictionary<string, IndexModel>();

        foreach (var property in entityModel.Properties)
        {
            foreach (var gsi in property.GlobalSecondaryIndexes)
            {
                if (!indexes.TryGetValue(gsi.IndexName, out var indexModel))
                {
                    indexModel = new IndexModel { IndexName = gsi.IndexName };
                    indexes[gsi.IndexName] = indexModel;
                }

                if (gsi.IsPartitionKey)
                {
                    indexModel.PartitionKeyProperty = property.PropertyName;
                    indexModel.PartitionKeyFormat = gsi.KeyFormat;
                }
                else if (gsi.IsSortKey)
                {
                    indexModel.SortKeyProperty = property.PropertyName;
                    indexModel.SortKeyFormat = gsi.KeyFormat;
                }
            }
        }

        entityModel.Indexes = indexes.Values.ToArray();
    }

    private void ExtractRelationships(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var relationships = new List<RelationshipModel>();

        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var relatedEntityAttr = GetAttribute(member, semanticModel, "RelatedEntityAttribute");
            if (relatedEntityAttr == null)
                continue;

            var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;
            if (propertySymbol == null)
                continue;

            var relationshipModel = new RelationshipModel
            {
                PropertyName = propertySymbol.Name,
                PropertyType = propertySymbol.Type.ToDisplayString(),
                IsCollection = IsCollectionType(propertySymbol.Type)
            };

            // Extract sort key pattern from constructor argument
            if (relatedEntityAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax patternLiteral)
            {
                relationshipModel.SortKeyPattern = patternLiteral.Token.ValueText;
            }

            // Extract entity type from named argument
            var entityTypeArg = relatedEntityAttr.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "EntityType");
            
            if (entityTypeArg?.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                relationshipModel.EntityType = typeOfExpr.Type.ToString();
            }

            relationships.Add(relationshipModel);
        }

        entityModel.Relationships = relationships.ToArray();
    }

    private void ValidateEntityModel(EntityModel entityModel)
    {
        var partitionKeyProperties = entityModel.Properties.Where(p => p.IsPartitionKey).ToArray();
        var sortKeyProperties = entityModel.Properties.Where(p => p.IsSortKey).ToArray();

        // Validate partition key
        if (partitionKeyProperties.Length == 0)
        {
            ReportDiagnostic(DiagnosticDescriptors.MissingPartitionKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }
        else if (partitionKeyProperties.Length > 1)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiplePartitionKeys, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate sort key
        if (sortKeyProperties.Length > 1)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultipleSortKeys, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate GSI configurations
        foreach (var index in entityModel.Indexes)
        {
            if (string.IsNullOrEmpty(index.PartitionKeyProperty))
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidGsiConfiguration, 
                    entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                    index.IndexName, entityModel.ClassName);
            }
        }

        // Check if entity is multi-item (has collection properties with DynamoDB attributes)
        entityModel.IsMultiItemEntity = entityModel.Properties.Any(p => p.IsCollection && p.HasAttributeMapping);
        
        // Validate multi-item entity consistency
        if (entityModel.IsMultiItemEntity)
        {
            ValidateMultiItemEntityConsistency(entityModel);
        }
    }

    private void ValidatePropertyModel(PropertyModel propertyModel)
    {
        // Check if property has key attributes but missing DynamoDbAttribute
        if ((propertyModel.IsPartitionKey || propertyModel.IsSortKey || propertyModel.IsPartOfGsi) && 
            string.IsNullOrEmpty(propertyModel.AttributeName))
        {
            ReportDiagnostic(DiagnosticDescriptors.MissingDynamoDbAttribute, 
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(), 
                propertyModel.PropertyName);
        }

        // Validate property type support
        if (!IsSupportedPropertyType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.UnsupportedPropertyType, 
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(), 
                propertyModel.PropertyName, propertyModel.PropertyType);
        }
    }

    private bool IsCollectionType(ITypeSymbol type)
    {
        // Check if type implements IEnumerable<T> but is not string
        if (type.SpecialType == SpecialType.System_String)
            return false;

        return type.AllInterfaces.Any(i => 
            i.IsGenericType && 
            i.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
    }

    private bool IsSupportedPropertyType(string typeName)
    {
        // Basic type support - this will be expanded in later tasks
        var supportedTypes = new[]
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte[]", "System.String", "System.Int32", "System.Int64", "System.Double", 
            "System.Single", "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte[]", "Ulid"
        };

        // Remove nullable annotations for checking
        var baseType = typeName.TrimEnd('?');
        
        // Check for nullable value types
        if (baseType.StartsWith("System.Nullable<") || baseType.Contains("?"))
        {
            return true; // Assume nullable types are supported if base type is
        }

        // Check for collections
        if (baseType.StartsWith("System.Collections.Generic.List<") ||
            baseType.StartsWith("List<") ||
            baseType.StartsWith("IList<") ||
            baseType.StartsWith("ICollection<") ||
            baseType.StartsWith("IEnumerable<"))
        {
            return true; // Collections are supported
        }

        return supportedTypes.Contains(baseType);
    }

    private AttributeSyntax? GetAttribute(SyntaxNode node, SemanticModel semanticModel, string attributeName)
    {
        return GetAttributes(node, semanticModel, attributeName).FirstOrDefault();
    }

    private IEnumerable<AttributeSyntax> GetAttributes(SyntaxNode node, SemanticModel semanticModel, string attributeName)
    {
        var attributeLists = node switch
        {
            ClassDeclarationSyntax classDecl => classDecl.AttributeLists,
            PropertyDeclarationSyntax propDecl => propDecl.AttributeLists,
            _ => default
        };

        if (attributeLists.Count == 0)
            return Enumerable.Empty<AttributeSyntax>();

        return attributeLists
            .SelectMany(al => al.Attributes)
            .Where(attr => 
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attr);
                if (symbolInfo.Symbol is IMethodSymbol method)
                {
                    var containingType = method.ContainingType.ToDisplayString();
                    return containingType.EndsWith(attributeName) || 
                           containingType.EndsWith(attributeName.Replace("Attribute", ""));
                }
                return false;
            });
    }

    private void ValidateMultiItemEntityConsistency(EntityModel entityModel)
    {
        // Multi-item entities must have a partition key for grouping
        if (entityModel.PartitionKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityMissingPartitionKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
            return;
        }

        // Multi-item entities should have a sort key for item ordering
        if (entityModel.SortKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityMissingSortKey, 
                entityModel.ClassDeclaration?.Identifier.GetLocation(), 
                entityModel.ClassName);
        }

        // Validate that collection properties have appropriate attribute mappings
        var collectionProperties = entityModel.Properties.Where(p => p.IsCollection && p.HasAttributeMapping).ToArray();
        
        foreach (var collectionProperty in collectionProperties)
        {
            // Collection properties in multi-item entities should not conflict with key attributes
            if (collectionProperty.IsPartitionKey || collectionProperty.IsSortKey)
            {
                ReportDiagnostic(DiagnosticDescriptors.CollectionPropertyCannotBeKey, 
                    collectionProperty.PropertyDeclaration?.Identifier.GetLocation(), 
                    collectionProperty.PropertyName, entityModel.ClassName);
            }
        }

        // Ensure partition key generation is consistent
        ValidatePartitionKeyGeneration(entityModel);
    }

    private void ValidatePartitionKeyGeneration(EntityModel entityModel)
    {
        var partitionKeyProperty = entityModel.PartitionKeyProperty;
        if (partitionKeyProperty?.KeyFormat != null)
        {
            // If partition key has a format, ensure it's suitable for multi-item entities
            var keyFormat = partitionKeyProperty.KeyFormat;
            
            // Warn if partition key format might not be suitable for grouping
            if (string.IsNullOrEmpty(keyFormat.Prefix) && string.IsNullOrEmpty(keyFormat.Separator))
            {
                ReportDiagnostic(DiagnosticDescriptors.MultiItemEntityPartitionKeyFormat, 
                    partitionKeyProperty.PropertyDeclaration?.Identifier.GetLocation(), 
                    partitionKeyProperty.PropertyName, entityModel.ClassName);
            }
        }
    }

    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }
}