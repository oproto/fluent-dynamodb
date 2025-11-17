using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes class declarations to extract DynamoDB entity information.
/// </summary>
internal class EntityAnalyzer
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
            ClassDeclaration = classDecl,
            SemanticModel = semanticModel
        };

        // Detect JSON serializer configuration
        var jsonSerializerInfo = JsonSerializerDetector.DetectJsonSerializer(semanticModel.Compilation);
        entityModel.JsonSerializerInfo = jsonSerializerInfo;

        // Detect geospatial package
        entityModel.HasGeospatialPackage = DetectGeospatialPackage(semanticModel.Compilation);

        // Extract table information
        if (!ExtractTableInfo(classDecl, semanticModel, entityModel))
            return null;

        // Extract property information
        ExtractProperties(classDecl, semanticModel, entityModel);

        // Validate individual properties
        foreach (var property in entityModel.Properties)
        {
            ValidatePropertyModel(property, semanticModel);
            ValidatePropertyPerformance(property);
        }

        // Validate entity configuration
        ValidateEntityModel(entityModel);

        // Extract index information
        ExtractIndexes(entityModel);

        // Extract relationship information
        ExtractRelationships(classDecl, semanticModel, entityModel);

        // Validate related entity configurations (must be after ExtractRelationships)
        if (entityModel.Relationships.Length > 0)
        {
            ValidateRelatedEntityConfiguration(entityModel);
        }

        // Only return null if there are critical errors that prevent code generation
        var criticalErrors = _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && IsCriticalError(d.Id)).ToArray();
        return criticalErrors.Length > 0 ? null : entityModel;
    }

    private bool IsPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.ValueText == "partial");
    }

    private bool ExtractTableInfo(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var tableAttribute = GetAttribute(classDecl, semanticModel, "DynamoDbTableAttribute");
        
        // Check if this is a DynamoDbEntity (nested type) instead of a DynamoDbTable
        if (tableAttribute == null)
        {
            var entityAttribute = GetAttribute(classDecl, semanticModel, "DynamoDbEntityAttribute");
            if (entityAttribute != null)
            {
                // This is a nested entity type - no table name required
                // Set a placeholder table name to indicate it's an entity
                entityModel.TableName = $"_entity_{entityModel.ClassName}";
                return true;
            }
            return false;
        }

        // Extract table name from constructor argument
        if (tableAttribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax tableNameLiteral)
        {
            entityModel.TableName = tableNameLiteral.Token.ValueText;
        }

        // Extract IsDefault property from named arguments
        if (tableAttribute.ArgumentList != null)
        {
            foreach (var arg in tableAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "IsDefault" &&
                    arg.Expression is LiteralExpressionSyntax isDefaultLiteral)
                {
                    entityModel.IsDefault = bool.Parse(isDefaultLiteral.Token.ValueText);
                    break;
                }
            }
        }

        // Extract discriminator configuration
        entityModel.Discriminator = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            tableAttribute, 
            semanticModel, 
            entityModel.ClassName, 
            _diagnostics);
        
        // Keep legacy property for backward compatibility
        if (entityModel.Discriminator != null && entityModel.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            entityModel.EntityDiscriminator = entityModel.Discriminator.ExactValue;
        }

        // Extract scannable attribute
        ExtractScannableAttribute(classDecl, semanticModel, entityModel);

        // Extract entity property configuration
        ExtractEntityPropertyConfiguration(classDecl, semanticModel, entityModel);

        // Extract accessor configurations
        ExtractAccessorConfigurations(classDecl, semanticModel, entityModel);

        // Extract stream conversion attribute
        ExtractStreamConversionAttribute(classDecl, semanticModel, entityModel);

        return !string.IsNullOrEmpty(entityModel.TableName);
    }

    private void ExtractScannableAttribute(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var scannableAttribute = GetAttribute(classDecl, semanticModel, "ScannableAttribute");
        entityModel.IsScannable = scannableAttribute != null;
    }

    private void ExtractEntityPropertyConfiguration(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var entityPropertyAttribute = GetAttribute(classDecl, semanticModel, "GenerateEntityPropertyAttribute");
        if (entityPropertyAttribute == null)
        {
            // Use default configuration
            entityModel.EntityPropertyConfig = new EntityPropertyConfig();
            return;
        }

        var config = new EntityPropertyConfig();

        // Extract named arguments
        if (entityPropertyAttribute.ArgumentList != null)
        {
            foreach (var arg in entityPropertyAttribute.ArgumentList.Arguments)
            {
                switch (arg.NameEquals?.Name.Identifier.ValueText)
                {
                    case "Name" when arg.Expression is LiteralExpressionSyntax nameLiteral:
                        var name = nameLiteral.Token.ValueText;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            // Emit FDDB004 diagnostic for empty entity property name
                            ReportDiagnostic(DiagnosticDescriptors.EmptyEntityPropertyName,
                                classDecl.Identifier.GetLocation(),
                                entityModel.ClassName);
                        }
                        else
                        {
                            config.Name = name;
                        }
                        break;

                    case "Generate" when arg.Expression is LiteralExpressionSyntax generateLiteral:
                        config.Generate = bool.Parse(generateLiteral.Token.ValueText);
                        break;

                    case "Modifier" when arg.Expression is MemberAccessExpressionSyntax modifierExpr:
                        // Extract the enum value (e.g., AccessModifier.Internal -> "Internal")
                        var modifierName = modifierExpr.Name.Identifier.ValueText;
                        if (Enum.TryParse<AccessModifier>(modifierName, out var modifier))
                        {
                            config.Modifier = modifier;
                        }
                        break;
                }
            }
        }

        entityModel.EntityPropertyConfig = config;
    }

    private void ExtractAccessorConfigurations(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var accessorAttributes = GetAttributes(classDecl, semanticModel, "GenerateAccessorsAttribute");
        var configs = new List<AccessorConfig>();
        var operationsSeen = new Dictionary<TableOperation, Location>();

        foreach (var accessorAttr in accessorAttributes)
        {
            var config = new AccessorConfig();

            // Extract named arguments
            if (accessorAttr.ArgumentList != null)
            {
                foreach (var arg in accessorAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "Operations":
                            config.Operations = ExtractOperationsFlags(arg.Expression);
                            break;

                        case "Generate" when arg.Expression is LiteralExpressionSyntax generateLiteral:
                            config.Generate = bool.Parse(generateLiteral.Token.ValueText);
                            break;

                        case "Modifier" when arg.Expression is MemberAccessExpressionSyntax modifierExpr:
                            var modifierName = modifierExpr.Name.Identifier.ValueText;
                            if (Enum.TryParse<AccessModifier>(modifierName, out var modifier))
                            {
                                config.Modifier = modifier;
                            }
                            break;
                    }
                }
            }

            // Validate that operations don't conflict with previously seen configurations
            var individualOperations = ExpandOperationFlags(config.Operations);
            foreach (var operation in individualOperations)
            {
                if (operationsSeen.TryGetValue(operation, out var previousLocation))
                {
                    // Emit FDDB003 diagnostic for conflicting accessor configuration
                    ReportDiagnostic(DiagnosticDescriptors.ConflictingAccessorConfiguration,
                        accessorAttr.GetLocation(),
                        entityModel.ClassName,
                        operation.ToString());
                }
                else
                {
                    operationsSeen[operation] = accessorAttr.GetLocation();
                }
            }

            configs.Add(config);
        }

        entityModel.AccessorConfigs = configs;
    }

    private void ExtractStreamConversionAttribute(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var streamConversionAttribute = GetAttribute(classDecl, semanticModel, "GenerateStreamConversionAttribute");
        entityModel.GenerateStreamConversion = streamConversionAttribute != null;

        // Validate that Amazon.Lambda.DynamoDBEvents is referenced when attribute is present
        if (entityModel.GenerateStreamConversion)
        {
            ValidateLambdaEventsPackageReference(semanticModel, entityModel);
        }
    }

    private void ValidateLambdaEventsPackageReference(SemanticModel semanticModel, EntityModel entityModel)
    {
        // Check if Amazon.Lambda.DynamoDBEvents.DynamoDBEvent type is available in the compilation
        var lambdaEventType = semanticModel.Compilation.GetTypeByMetadataName("Amazon.Lambda.DynamoDBEvents.DynamoDBEvent");

        if (lambdaEventType == null)
        {
            // Package is not referenced - emit diagnostic error
            ReportDiagnostic(
                DiagnosticDescriptors.MissingLambdaEventsPackage,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName);
        }
    }

    private TableOperation ExtractOperationsFlags(ExpressionSyntax expression)
    {
        // Handle single enum value: DynamoDbOperation.Get
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var operationName = memberAccess.Name.Identifier.ValueText;
            if (Enum.TryParse<TableOperation>(operationName, out var operation))
            {
                return operation;
            }
        }

        // Handle bitwise OR: DynamoDbOperation.Get | DynamoDbOperation.Query
        if (expression is BinaryExpressionSyntax binaryExpr && binaryExpr.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.BitwiseOrExpression))
        {
            var left = ExtractOperationsFlags(binaryExpr.Left);
            var right = ExtractOperationsFlags(binaryExpr.Right);
            return left | right;
        }

        // Default to All if we can't parse
        return TableOperation.All;
    }

    private List<TableOperation> ExpandOperationFlags(TableOperation operations)
    {
        var result = new List<TableOperation>();

        // If All is specified, expand to all individual operations
        if (operations.HasFlag(TableOperation.All))
        {
            result.Add(TableOperation.Get);
            result.Add(TableOperation.Query);
            result.Add(TableOperation.Scan);
            result.Add(TableOperation.Put);
            result.Add(TableOperation.Delete);
            result.Add(TableOperation.Update);
            return result;
        }

        // Otherwise, check each flag individually
        if (operations.HasFlag(TableOperation.Get))
            result.Add(TableOperation.Get);
        if (operations.HasFlag(TableOperation.Query))
            result.Add(TableOperation.Query);
        if (operations.HasFlag(TableOperation.Scan))
            result.Add(TableOperation.Scan);
        if (operations.HasFlag(TableOperation.Put))
            result.Add(TableOperation.Put);
        if (operations.HasFlag(TableOperation.Delete))
            result.Add(TableOperation.Delete);
        if (operations.HasFlag(TableOperation.Update))
            result.Add(TableOperation.Update);

        return result;
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
        else
        {
            // Fallback: try without "Attribute" suffix
            dynamoDbAttribute = GetAttribute(propertyDecl, semanticModel, "DynamoDbAttribute");
            if (dynamoDbAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax fallbackLiteral)
            {
                propertyModel.AttributeName = fallbackLiteral.Token.ValueText;
            }
        }

        // Extract Format, DateTimeKind, and GeoHashPrecision properties from DynamoDbAttribute if present
        if (dynamoDbAttribute?.ArgumentList != null)
        {
            foreach (var arg in dynamoDbAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "Format" &&
                    arg.Expression is LiteralExpressionSyntax formatLiteral)
                {
                    propertyModel.Format = formatLiteral.Token.ValueText;
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "DateTimeKind" &&
                         arg.Expression is MemberAccessExpressionSyntax dateTimeKindExpr)
                {
                    // Extract the enum value (e.g., DateTimeKind.Utc -> "Utc")
                    var kindName = dateTimeKindExpr.Name.Identifier.ValueText;
                    if (Enum.TryParse<DateTimeKind>(kindName, out var kind))
                    {
                        propertyModel.DateTimeKind = kind;
                    }
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "GeoHashPrecision" &&
                         arg.Expression is LiteralExpressionSyntax geoHashPrecisionLiteral)
                {
                    if (int.TryParse(geoHashPrecisionLiteral.Token.ValueText, out var precision))
                    {
                        propertyModel.GeoHashPrecision = precision;
                    }
                }
            }
        }

        // Extract key attributes
        ExtractKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract GSI attributes
        ExtractGsiAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract queryable attributes
        ExtractQueryableAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract computed key attributes
        ExtractComputedKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract extracted key attributes
        ExtractExtractedKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Analyze advanced type information
        var advancedTypeAnalyzer = new AdvancedTypeAnalyzer();
        propertyModel.AdvancedType = advancedTypeAnalyzer.AnalyzeProperty(propertyModel, semanticModel);

        // Analyze security attributes
        var securityAnalyzer = new SecurityAttributeAnalyzer();
        propertyModel.Security = securityAnalyzer.AnalyzeProperty(propertyModel, semanticModel);

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

            // Extract GSI-specific discriminator configuration
            gsiModel.Discriminator = DiscriminatorAnalyzer.AnalyzeGsiDiscriminator(
                gsiAttr, 
                semanticModel, 
                gsiModel.IndexName, 
                _diagnostics);

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

    private void ExtractComputedKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var computedAttr = GetAttribute(propertyDecl, semanticModel, "ComputedAttribute");
        if (computedAttr == null)
            return;

        var computedModel = new ComputedKeyModel();

        // Extract source properties from constructor arguments
        if (computedAttr.ArgumentList?.Arguments != null)
        {
            var sourceProperties = new List<string>();

            foreach (var arg in computedAttr.ArgumentList.Arguments)
            {
                // Skip named arguments for now, handle positional arguments (source properties)
                if (arg.NameEquals == null && arg.Expression is LiteralExpressionSyntax literal)
                {
                    sourceProperties.Add(literal.Token.ValueText);
                }
            }

            computedModel.SourceProperties = sourceProperties.ToArray();
        }

        // Extract named arguments
        if (computedAttr.ArgumentList != null)
        {
            foreach (var arg in computedAttr.ArgumentList.Arguments)
            {
                switch (arg.NameEquals?.Name.Identifier.ValueText)
                {
                    case "Format" when arg.Expression is LiteralExpressionSyntax formatLiteral:
                        computedModel.Format = formatLiteral.Token.ValueText;
                        break;
                    case "Separator" when arg.Expression is LiteralExpressionSyntax separatorLiteral:
                        computedModel.Separator = separatorLiteral.Token.ValueText;
                        break;
                }
            }
        }

        propertyModel.ComputedKey = computedModel;
    }

    private void ExtractExtractedKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var extractedAttr = GetAttribute(propertyDecl, semanticModel, "ExtractedAttribute");
        if (extractedAttr == null)
            return;

        var extractedModel = new ExtractedKeyModel();

        // Extract constructor arguments (source property and index)
        if (extractedAttr.ArgumentList?.Arguments != null && extractedAttr.ArgumentList.Arguments.Count >= 2)
        {
            var args = extractedAttr.ArgumentList.Arguments;

            // First argument: source property
            if (args[0].Expression is LiteralExpressionSyntax sourcePropertyLiteral)
            {
                extractedModel.SourceProperty = sourcePropertyLiteral.Token.ValueText;
            }

            // Second argument: index
            if (args[1].Expression is LiteralExpressionSyntax indexLiteral &&
                int.TryParse(indexLiteral.Token.ValueText, out var index))
            {
                extractedModel.Index = index;
            }
        }

        // Extract named arguments
        if (extractedAttr.ArgumentList != null)
        {
            foreach (var arg in extractedAttr.ArgumentList.Arguments)
            {
                switch (arg.NameEquals?.Name.Identifier.ValueText)
                {
                    case "Separator" when arg.Expression is LiteralExpressionSyntax separatorLiteral:
                        extractedModel.Separator = separatorLiteral.Token.ValueText;
                        break;
                }
            }
        }

        propertyModel.ExtractedKey = extractedModel;
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

                // Propagate GSI discriminator (use first one found if multiple properties define it)
                if (gsi.Discriminator != null && indexModel.GsiDiscriminator == null)
                {
                    indexModel.GsiDiscriminator = gsi.Discriminator;
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

        // Check if this is a nested entity (DynamoDbEntity) vs a table entity (DynamoDbTable)
        var isNestedEntity = entityModel.TableName?.StartsWith("_entity_") == true;

        // Validate partition key - this is critical for DynamoDB table entities
        // Nested entities (marked with [DynamoDbEntity]) don't need partition keys
        if (!isNestedEntity)
        {
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

        // Multi-item entity concept is legacy - collections are now just serialized as DynamoDB Lists
        // ToListAsync() handles multiple entity instances, compound entities use different patterns
        entityModel.IsMultiItemEntity = false;

        // Validate computed and extracted keys
        ValidateComputedAndExtractedKeys(entityModel);

        // Validate advanced types (Map, Set, List, TTL, JsonBlob, BlobReference)
        ValidateAdvancedTypes(entityModel);

        // Validate security attributes (Sensitive, Encrypted)
        ValidateSecurityAttributes(entityModel);

        // Additional comprehensive validations
        ValidateEntityComplexity(entityModel);
        ValidateEntityScalability(entityModel);
        ValidateCircularReferences(entityModel);
    }

    private void ValidatePropertyModel(PropertyModel propertyModel, SemanticModel semanticModel)
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
        // Skip validation for advanced types (Map, Set, List, TTL, JsonBlob, BlobReference)
        // as they are validated separately
        var isAdvancedType = propertyModel.AdvancedType != null && (
            propertyModel.AdvancedType.IsMap ||
            propertyModel.AdvancedType.IsSet ||
            propertyModel.AdvancedType.IsList ||
            propertyModel.AdvancedType.IsTtl ||
            propertyModel.AdvancedType.IsJsonBlob ||
            propertyModel.AdvancedType.IsBlobReference);

        if (!isAdvancedType && !IsSupportedPropertyType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.UnsupportedPropertyType,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType);
        }

        // Validate nested map types have [DynamoDbEntity] for AOT compatibility
        if (propertyModel.AdvancedType?.IsMap == true)
        {
            ValidateNestedMapType(propertyModel, semanticModel);
        }

        // Validate attribute name
        if (!string.IsNullOrEmpty(propertyModel.AttributeName))
        {
            ValidateAttributeName(propertyModel);
        }

        // Validate key format if present
        if (propertyModel.KeyFormat != null)
        {
            ValidateKeyFormat(propertyModel);
        }

        // Check for collection properties used as keys
        if (propertyModel.IsCollection && (propertyModel.IsPartitionKey || propertyModel.IsSortKey))
        {
            ReportDiagnostic(DiagnosticDescriptors.CollectionPropertyCannotBeKey,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, "Entity");
        }

        // Performance warnings for large types
        ValidatePropertyPerformance(propertyModel);
    }

    private void ValidateAttributeName(PropertyModel propertyModel)
    {
        var attributeName = propertyModel.AttributeName;

        // Check for invalid characters
        if (attributeName.Contains('\0') || attributeName.Contains('\n') || attributeName.Contains('\r'))
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidAttributeName,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                attributeName, propertyModel.PropertyName, "Contains invalid control characters");
        }

        // Check for reserved words
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ABORT", "ABSOLUTE", "ACTION", "ADD", "AFTER", "AGENT", "AGGREGATE", "ALL", "ALLOCATE", "ALTER",
            "ANALYZE", "AND", "ANY", "ARCHIVE", "ARE", "ARRAY", "AS", "ASC", "ASCII", "ASENSITIVE", "ASSERTION",
            "ASYMMETRIC", "AT", "ATOMIC", "ATTACH", "ATTRIBUTE", "AUTH", "AUTHORIZATION", "AUTHORIZE", "AUTO",
            "AVG", "BACK", "BACKUP", "BASE", "BATCH", "BEFORE", "BEGIN", "BETWEEN", "BIGINT", "BINARY", "BIT",
            "BLOB", "BLOCK", "BOOLEAN", "BOTH", "BREADTH", "BUCKET", "BULK", "BY", "BYTE", "CALL", "CALLED",
            "CALLING", "CAPACITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG", "CHAR", "CHARACTER",
            "CHECK", "CLASS", "CLOB", "CLOSE", "CLUSTER", "CLUSTERED", "CLUSTERING", "CLUSTERS", "COALESCE",
            "COLLATE", "COLLATION", "COLLECTION", "COLUMN", "COLUMNS", "COMBINE", "COMMENT", "COMMIT",
            "COMPACT", "COMPILE", "COMPRESS", "CONDITION", "CONFLICT", "CONNECT", "CONNECTION", "CONSISTENCY",
            "CONSISTENT", "CONSTRAINT", "CONSTRAINTS", "CONSTRUCTOR", "CONSUMED", "CONTAINS", "CONTINUE",
            "CONVERT", "COPY", "CORRESPONDING", "COUNT", "COUNTER", "CREATE", "CROSS", "CUBE", "CURRENT",
            "CURSOR", "CYCLE", "DATA", "DATABASE", "DATE", "DATETIME", "DAY", "DEALLOCATE", "DEC", "DECIMAL",
            "DECLARE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DEFINE", "DEFINED", "DEFINITION", "DELETE",
            "DELIMITED", "DEPTH", "DEREF", "DESC", "DESCRIBE", "DESCRIPTOR", "DETACH", "DETERMINISTIC",
            "DIAGNOSTICS", "DIRECTORIES", "DISABLE", "DISCONNECT", "DISTINCT", "DISTRIBUTE", "DO", "DOMAIN",
            "DOUBLE", "DROP", "DUMP", "DURATION", "DYNAMIC", "EACH", "ELEMENT", "ELSE", "ELSEIF", "EMPTY",
            "ENABLE", "END", "EQUAL", "EQUALS", "ERROR", "ESCAPE", "ESCAPED", "EVAL", "EVALUATE", "EXCEEDED",
            "EXCEPT", "EXCEPTION", "EXCEPTIONS", "EXCLUSIVE", "EXEC", "EXECUTE", "EXISTS", "EXIT", "EXPLAIN",
            "EXPLODE", "EXPORT", "EXPRESSION", "EXTENDED", "EXTERNAL", "EXTRACT", "FAIL", "FALSE", "FAMILY",
            "FETCH", "FIELDS", "FILE", "FILTER", "FILTERING", "FINAL", "FINISH", "FIRST", "FIXED", "FLATTERN",
            "FLOAT", "FOR", "FORCE", "FOREIGN", "FORMAT", "FORWARD", "FOUND", "FREE", "FROM", "FULL",
            "FUNCTION", "FUNCTIONS", "GENERAL", "GENERATE", "GET", "GLOB", "GLOBAL", "GO", "GOTO", "GRANT",
            "GREATER", "GROUP", "GROUPING", "HANDLER", "HASH", "HAVE", "HAVING", "HEAP", "HIDDEN", "HOLD",
            "HOUR", "IDENTIFIED", "IDENTITY", "IF", "IGNORE", "IMMEDIATE", "IMPORT", "IN", "INCLUDING",
            "INCLUSIVE", "INCREMENT", "INCREMENTAL", "INDEX", "INDEXED", "INDEXES", "INDICATOR", "INFINITE",
            "INITIALLY", "INLINE", "INNER", "INNTER", "INOUT", "INPUT", "INSENSITIVE", "INSERT", "INSTEAD",
            "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "INVALIDATE", "IS", "ISOLATION", "ITEM",
            "ITEMS", "ITERATE", "JOIN", "KEY", "KEYS", "LAG", "LANGUAGE", "LARGE", "LAST", "LATERAL", "LEAD",
            "LEADING", "LEAVE", "LEFT", "LENGTH", "LESS", "LEVEL", "LIKE", "LIMIT", "LIMITED", "LINES", "LIST",
            "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOCKS", "LOG",
            "LOGED", "LONG", "LOOP", "LOWER", "MAP", "MATCH", "MATERIALIZED", "MAX", "MAXLEN", "MEMBER",
            "MERGE", "METHOD", "METRICS", "MIN", "MINUS", "MINUTE", "MISSING", "MOD", "MODE", "MODIFIES",
            "MODIFY", "MODULE", "MONTH", "MULTI", "MULTISET", "NAME", "NAMES", "NATIONAL", "NATURAL", "NCHAR",
            "NCLOB", "NEW", "NEXT", "NO", "NONE", "NOT", "NULL", "NULLIF", "NUMBER", "NUMERIC", "OBJECT",
            "OF", "OFFLINE", "OFFSET", "OLD", "ON", "ONLINE", "ONLY", "OPAQUE", "OPEN", "OPERATOR", "OPTION",
            "OR", "ORDER", "ORDINALITY", "OTHER", "OTHERS", "OUT", "OUTER", "OUTPUT", "OVER", "OVERLAPS",
            "OVERRIDE", "OWNER", "PAD", "PARALLEL", "PARAMETER", "PARAMETERS", "PARTIAL", "PARTITION",
            "PARTITIONED", "PARTITIONS", "PATH", "PERCENT", "PERCENTILE", "PERMISSION", "PERMISSIONS", "PIPE",
            "PIPELINED", "PLAN", "POOL", "POSITION", "PRECISION", "PREPARE", "PRESERVE", "PRIMARY", "PRIOR",
            "PRIVATE", "PRIVILEGES", "PROCEDURE", "PROCESSED", "PROJECT", "PROJECTION", "PROPERTY", "PROVISIONING",
            "PUBLIC", "PUT", "QUERY", "QUIT", "QUORUM", "RAISE", "RANDOM", "RANGE", "RANK", "RAW", "READ",
            "READS", "REAL", "REBUILD", "RECORD", "RECURSIVE", "REDUCE", "REF", "REFERENCE", "REFERENCES",
            "REFERENCING", "REGEXP", "REGION", "REINDEX", "RELATIVE", "RELEASE", "REMAINDER", "RENAME",
            "REPEAT", "REPLACE", "REQUEST", "RESET", "RESIGNAL", "RESOURCE", "RESPONSE", "RESTORE", "RESTRICT",
            "RESULT", "RETURN", "RETURNING", "RETURNS", "REVERSE", "REVOKE", "RIGHT", "ROLE", "ROLES",
            "ROLLBACK", "ROLLUP", "ROUTINE", "ROW", "ROWS", "RULE", "RULES", "SAMPLE", "SATISFIES", "SAVE",
            "SAVEPOINT", "SCAN", "SCHEMA", "SCOPE", "SCROLL", "SEARCH", "SECOND", "SECTION", "SEGMENT",
            "SELECT", "SELF", "SEMI", "SENSITIVE", "SEPARATE", "SEQUENCE", "SERIALIZABLE", "SESSION", "SET",
            "SETS", "SHARD", "SHARE", "SHARED", "SHORT", "SHOW", "SIGNAL", "SIMILAR", "SIZE", "SKEWED",
            "SMALLINT", "SNAPSHOT", "SOME", "SOURCE", "SPACE", "SPACES", "SPARSE", "SPECIFIC", "SPECIFICTYPE",
            "SPLIT", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "START", "STATE",
            "STATIC", "STATUS", "STORAGE", "STORE", "STORED", "STREAM", "STRING", "STRUCT", "STYLE", "SUB",
            "SUBMULTISET", "SUBPARTITION", "SUBSTRING", "SUBTYPE", "SUM", "SUPER", "SYMMETRIC", "SYNONYM",
            "SYSTEM", "TABLE", "TABLESAMPLE", "TEMP", "TEMPORARY", "TERMINATED", "TEXT", "THAN", "THEN",
            "THROUGHPUT", "TIME", "TIMESTAMP", "TIMEZONE", "TINYINT", "TO", "TOKEN", "TOTAL", "TOUCH",
            "TRAILING", "TRANSACTION", "TRANSFORM", "TRANSLATE", "TRANSLATION", "TREAT", "TRIGGER", "TRIM",
            "TRUE", "TRUNCATE", "TTL", "TUPLE", "TYPE", "UNDER", "UNDO", "UNION", "UNIQUE", "UNIT", "UNKNOWN",
            "UNLOGGED", "UNNEST", "UNPROCESSED", "UNSIGNED", "UNTIL", "UPDATE", "UPPER", "URL", "USAGE",
            "USE", "USER", "USERS", "USING", "UUID", "VACUUM", "VALUE", "VALUED", "VALUES", "VARCHAR",
            "VARIABLE", "VARIANCE", "VARINT", "VARYING", "VIEW", "VIEWS", "VIRTUAL", "VOID", "WAIT", "WHEN",
            "WHENEVER", "WHERE", "WHILE", "WINDOW", "WITH", "WITHIN", "WITHOUT", "WORK", "WRAPPED", "WRITE",
            "YEAR", "ZONE"
        };

        if (reservedWords.Contains(attributeName))
        {
            ReportDiagnostic(DiagnosticDescriptors.ReservedWordUsage,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, attributeName);
        }

        // Check attribute name length
        if (attributeName.Length > 255)
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidAttributeName,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                attributeName, propertyModel.PropertyName, "Attribute name exceeds 255 character limit");
        }
    }

    private void ValidateKeyFormat(PropertyModel propertyModel)
    {
        var keyFormat = propertyModel.KeyFormat;
        if (keyFormat == null) return;

        // Validate separator
        if (!string.IsNullOrEmpty(keyFormat.Separator))
        {
            if (keyFormat.Separator.Contains('\0') || keyFormat.Separator.Length > 10)
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidKeyFormatSyntax,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"Separator: '{keyFormat.Separator}'", propertyModel.PropertyName);
            }
        }

        // Validate prefix
        if (!string.IsNullOrEmpty(keyFormat.Prefix))
        {
            if (keyFormat.Prefix.Contains('\0') || keyFormat.Prefix.Length > 100)
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidKeyFormatSyntax,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"Prefix: '{keyFormat.Prefix}'", propertyModel.PropertyName);
            }

            // Check for potential key collision patterns
            if (keyFormat.Prefix.EndsWith(keyFormat.Separator ?? "#"))
            {
                ReportDiagnostic(DiagnosticDescriptors.PotentialKeyCollision,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    $"{keyFormat.Prefix}{keyFormat.Separator}{{value}}", propertyModel.PropertyName);
            }
        }
    }

    private void ValidateNestedMapType(PropertyModel propertyModel, SemanticModel semanticModel)
    {
        // Only validate custom object maps (not Dictionary<string, string> or Dictionary<string, AttributeValue>)
        var propertyType = propertyModel.PropertyType;
        
        // Skip Dictionary types - they don't need [DynamoDbEntity]
        if (propertyType.Contains("Dictionary<"))
            return;

        // For custom types with [DynamoDbMap], verify the nested type has [DynamoDbEntity]
        // This is required for AOT compatibility - we need the nested type's generated ToDynamoDb/FromDynamoDb methods
        
        // Get the type symbol for the property
        if (propertyModel.PropertyDeclaration == null)
            return;

        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyModel.PropertyDeclaration) as IPropertySymbol;
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
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName,
                propertyType);
        }
    }

    private void ValidatePropertyPerformance(PropertyModel propertyModel)
    {
        // Warn about potentially large string properties
        if (propertyModel.PropertyType == "string" && !propertyModel.IsCollection)
        {
            // This is a heuristic - in practice, you'd need more context
            if (propertyModel.PropertyName.ToLowerInvariant().Contains("description") ||
                propertyModel.PropertyName.ToLowerInvariant().Contains("content") ||
                propertyModel.PropertyName.ToLowerInvariant().Contains("body"))
            {
                ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    propertyModel.PropertyName, propertyModel.PropertyType,
                    "Large string properties may impact DynamoDB performance and costs");
            }
        }

        // Warn about binary data properties
        if (propertyModel.PropertyType == "byte[]" || propertyModel.PropertyType == "System.Byte[]")
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType,
                "Binary data properties may cause performance issues. Consider using native DynamoDB List (L) or Map (M) types");
        }

        // Warn about complex collection types
        if (propertyModel.IsCollection && IsComplexCollectionType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType,
                "Complex collection types may cause performance issues. Consider using native DynamoDB List (L) or Map (M) types");
        }

        // Warn about nested complex objects
        if (!propertyModel.IsCollection && !IsPrimitiveType(propertyModel.PropertyType) &&
            propertyModel.PropertyType != "object" && !propertyModel.PropertyType.EndsWith("?"))
        {
            // Check if it's a complex nested object (not a simple value type)
            if (IsComplexNestedType(propertyModel.PropertyType))
            {
                ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                    propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                    propertyModel.PropertyName, propertyModel.PropertyType,
                    "Complex nested objects may cause performance issues. Consider using native DynamoDB List (L) or Map (M) types");
            }
        }
    }

    private bool IsComplexCollectionType(string collectionType)
    {
        // Check if collection contains complex types
        var elementType = GetCollectionElementType(collectionType);

        // Dictionary<string, object> and similar complex types are performance concerns
        if (elementType.StartsWith("Dictionary<") || elementType.StartsWith("System.Collections.Generic.Dictionary<"))
        {
            return true;
        }

        // Collections of complex objects (not primitive types)
        return !IsPrimitiveType(elementType) && elementType != "object";
    }

    private string GetCollectionElementType(string collectionType)
    {
        // Handle nullable collections like List<T>?
        var baseType = collectionType.TrimEnd('?');

        // Extract element type from generic collections
        // Examples: List<string> -> string, IEnumerable<ChildEntity> -> ChildEntity
        if (baseType.Contains('<') && baseType.Contains('>'))
        {
            var startIndex = baseType.IndexOf('<') + 1;
            var endIndex = baseType.LastIndexOf('>');
            if (endIndex > startIndex)
            {
                return baseType.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        // Handle array types like string[] -> string
        if (baseType.EndsWith("[]"))
        {
            return baseType.Substring(0, baseType.Length - 2);
        }

        // If we can't determine the element type, return the original type
        return baseType;
    }

    private bool IsComplexNestedType(string typeName)
    {
        // Skip nullable annotations
        var baseType = typeName.TrimEnd('?');

        // These are complex types that may cause performance issues
        if (baseType.StartsWith("Dictionary<") || baseType.StartsWith("System.Collections.Generic.Dictionary<"))
        {
            return true;
        }

        // Custom classes/structs that aren't primitive types
        if (!IsPrimitiveType(baseType) &&
            baseType != "object" &&
            !baseType.StartsWith("System.") &&
            !baseType.Contains("[]"))
        {
            return true;
        }

        return false;
    }

    private bool IsPrimitiveType(string typeName)
    {
        var primitiveTypes = new HashSet<string>
        {
            "string", "int", "long", "double", "float", "decimal", "bool", "DateTime", "DateTimeOffset",
            "Guid", "byte", "short", "uint", "ulong", "ushort", "sbyte", "char",
            "System.String", "System.Int32", "System.Int64", "System.Double", "System.Single",
            "System.Decimal", "System.Boolean", "System.DateTime", "System.DateTimeOffset",
            "System.Guid", "System.Byte", "System.Int16", "System.UInt32", "System.UInt64",
            "System.UInt16", "System.SByte", "System.Char", "Ulid", "System.Ulid"
        };

        var baseType = typeName.TrimEnd('?');
        return primitiveTypes.Contains(baseType);
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
            "System.Guid", "System.Byte[]", "Ulid", "System.Ulid"
        };

        // Remove nullable annotations for checking
        var baseType = typeName.TrimEnd('?');

        // Check for nullable value types
        if (baseType.StartsWith("System.Nullable<") || baseType.Contains("?"))
        {
            return true; // Assume nullable types are supported if base type is
        }

        // Check for GeoLocation type (requires geospatial package)
        if (baseType == "GeoLocation" || 
            baseType == "Oproto.FluentDynamoDb.Geospatial.GeoLocation")
        {
            return true; // GeoLocation is supported when geospatial package is referenced
        }

        // Check for Dictionary types (Map support)
        if (baseType.StartsWith("System.Collections.Generic.Dictionary<") ||
            baseType.StartsWith("Dictionary<"))
        {
            return true; // Dictionary types are supported for Map conversion
        }

        // Check for HashSet types (Set support)
        if (baseType.StartsWith("System.Collections.Generic.HashSet<") ||
            baseType.StartsWith("HashSet<"))
        {
            return true; // HashSet types are supported for Set conversion
        }

        // Check for List types (List support)
        if (baseType.StartsWith("System.Collections.Generic.List<") ||
            baseType.StartsWith("List<") ||
            baseType.StartsWith("IList<") ||
            baseType.StartsWith("ICollection<") ||
            baseType.StartsWith("IEnumerable<"))
        {
            return true; // List types are supported
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

        var targetName = attributeName.Replace("Attribute", "");

        return attributeLists
            .SelectMany(al => al.Attributes)
            .Where(attr =>
            {
                var attributeNameText = attr.Name.ToString();

                return attributeNameText == attributeName ||
                       attributeNameText == targetName ||
                       attributeNameText.EndsWith("." + attributeName) ||
                       attributeNameText.EndsWith("." + targetName);
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

    private void ValidateRelatedEntityConfiguration(EntityModel entityModel)
    {
        // Check if entity has sort key for pattern matching
        if (entityModel.SortKeyProperty == null)
        {
            ReportDiagnostic(DiagnosticDescriptors.RelatedEntitiesRequireSortKey,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName);
        }

        // Check for complex relationship patterns that may impact scalability
        ValidateRelationshipComplexity(entityModel);

        // Check for conflicting patterns
        var patterns = entityModel.Relationships.Select(r => r.SortKeyPattern).ToArray();
        for (int i = 0; i < patterns.Length; i++)
        {
            for (int j = i + 1; j < patterns.Length; j++)
            {
                if (PatternsConflict(patterns[i], patterns[j]))
                {
                    ReportDiagnostic(DiagnosticDescriptors.ConflictingRelatedEntityPatterns,
                        entityModel.ClassDeclaration?.Identifier.GetLocation(),
                        patterns[i], patterns[j], entityModel.ClassName);
                }
            }
        }

        // Validate each relationship
        foreach (var relationship in entityModel.Relationships)
        {
            ValidateRelationshipModel(relationship, entityModel);
        }
    }

    private void ValidateRelationshipModel(RelationshipModel relationship, EntityModel entityModel)
    {
        // Check for ambiguous patterns
        if (relationship.SortKeyPattern == "*" || string.IsNullOrWhiteSpace(relationship.SortKeyPattern))
        {
            ReportDiagnostic(DiagnosticDescriptors.AmbiguousRelatedEntityPattern,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                relationship.SortKeyPattern, relationship.PropertyName);
        }

        // Validate entity type if specified
        if (!string.IsNullOrWhiteSpace(relationship.EntityType))
        {
            // Basic validation - in a real implementation, we'd check if the type exists
            if (!IsValidEntityType(relationship.EntityType))
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidRelatedEntityType,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    relationship.PropertyName, relationship.EntityType);
            }
        }
    }

    private void ValidateRelationshipComplexity(EntityModel entityModel)
    {
        var relationships = entityModel.Relationships;

        // Check for complex relationship patterns that may impact scalability
        if (relationships.Length >= 3)
        {
            // Multiple related entities can impact query performance and complexity
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity has {relationships.Length} related entity relationships which may impact query performance and complexity");
        }

        // Check for collection relationships that may cause hot partitions
        var collectionRelationships = relationships.Where(r => r.IsCollection).ToArray();
        if (collectionRelationships.Length >= 2)
        {
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity has {collectionRelationships.Length} collection relationships which may cause hot partition issues");
        }

        // Check for wildcard patterns that may be inefficient
        var wildcardPatterns = relationships.Where(r => r.SortKeyPattern.Contains("*")).ToArray();
        if (wildcardPatterns.Length >= 2)
        {
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity has {wildcardPatterns.Length} wildcard relationship patterns which may require inefficient query patterns");
        }
    }

    private bool PatternsConflict(string pattern1, string pattern2)
    {
        // Simple conflict detection - patterns conflict if one is a prefix of another
        if (pattern1 == pattern2)
            return true;

        // Handle wildcard patterns
        var prefix1 = pattern1.Replace("*", "");
        var prefix2 = pattern2.Replace("*", "");

        return prefix1.StartsWith(prefix2) || prefix2.StartsWith(prefix1);
    }

    private bool IsValidEntityType(string entityType)
    {
        // Basic validation - check if it looks like a valid type name
        return !string.IsNullOrWhiteSpace(entityType) &&
               !entityType.Contains(" ") &&
               char.IsUpper(entityType[0]);
    }

    private void ValidateComputedAndExtractedKeys(EntityModel entityModel)
    {
        var propertyNames = new HashSet<string>(entityModel.Properties.Select(p => p.PropertyName));
        var computedProperties = entityModel.Properties.Where(p => p.IsComputed).ToArray();
        var extractedProperties = entityModel.Properties.Where(p => p.IsExtracted).ToArray();

        // Validate computed properties
        foreach (var computedProperty in computedProperties)
        {
            ValidateComputedProperty(computedProperty, propertyNames, entityModel);
        }

        // Validate extracted properties
        foreach (var extractedProperty in extractedProperties)
        {
            ValidateExtractedProperty(extractedProperty, propertyNames, entityModel);
        }

        // Check for circular dependencies between computed properties
        ValidateComputedKeyCircularDependencies(computedProperties, entityModel);
    }

    private void ValidateComputedProperty(PropertyModel computedProperty, HashSet<string> propertyNames, EntityModel entityModel)
    {
        var computedKey = computedProperty.ComputedKey!;

        // Check if property references itself
        if (computedKey.SourceProperties.Contains(computedProperty.PropertyName))
        {
            ReportDiagnostic(DiagnosticDescriptors.SelfReferencingComputedKey,
                computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                computedProperty.PropertyName);
            return;
        }

        // Validate all source properties exist
        foreach (var sourceProperty in computedKey.SourceProperties)
        {
            if (!propertyNames.Contains(sourceProperty))
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeySource,
                    computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                    computedProperty.PropertyName, sourceProperty);
            }
        }

        // Validate format if specified
        if (!string.IsNullOrEmpty(computedKey.Format))
        {
            ValidateComputedKeyFormat(computedProperty, computedKey);
        }
    }

    private void ValidateExtractedProperty(PropertyModel extractedProperty, HashSet<string> propertyNames, EntityModel entityModel)
    {
        var extractedKey = extractedProperty.ExtractedKey!;

        // Validate source property exists
        if (!propertyNames.Contains(extractedKey.SourceProperty))
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidExtractedKeySource,
                extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                extractedProperty.PropertyName, extractedKey.SourceProperty);
            return;
        }

        // Validate index is non-negative
        if (extractedKey.Index < 0)
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidExtractedKeyIndex,
                extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                extractedProperty.PropertyName, extractedKey.Index, extractedKey.SourceProperty);
        }

        // Check if source property is also computed (potential circular dependency)
        var sourceProperty = entityModel.Properties.FirstOrDefault(p => p.PropertyName == extractedKey.SourceProperty);
        if (sourceProperty?.IsComputed == true)
        {
            // This is allowed but we should check for circular dependencies
            var computedSourceProperties = sourceProperty.ComputedKey?.SourceProperties ?? Array.Empty<string>();
            if (computedSourceProperties.Contains(extractedProperty.PropertyName))
            {
                ReportDiagnostic(DiagnosticDescriptors.CircularKeyDependency,
                    extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                    $"{extractedProperty.PropertyName} -> {extractedKey.SourceProperty} -> {extractedProperty.PropertyName}");
            }
        }
    }

    private void ValidateComputedKeyFormat(PropertyModel computedProperty, ComputedKeyModel computedKey)
    {
        var format = computedKey.Format!;

        try
        {
            // Basic format validation - check for valid placeholder syntax
            var placeholderCount = 0;
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] == '{')
                {
                    var endIndex = format.IndexOf('}', i);
                    if (endIndex == -1)
                    {
                        ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeyFormat,
                            computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                            computedProperty.PropertyName, format, "Unclosed placeholder");
                        return;
                    }

                    var placeholderText = format.Substring(i + 1, endIndex - i - 1);
                    if (int.TryParse(placeholderText, out var placeholderIndex))
                    {
                        placeholderCount = Math.Max(placeholderCount, placeholderIndex + 1);
                    }
                    else if (!string.IsNullOrEmpty(placeholderText) && !placeholderText.Contains(':'))
                    {
                        // Invalid placeholder format
                        ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeyFormat,
                            computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                            computedProperty.PropertyName, format, $"Invalid placeholder: {{{placeholderText}}}");
                        return;
                    }

                    i = endIndex;
                }
            }

            // Check if placeholder count matches source property count
            if (placeholderCount > computedKey.SourceProperties.Length)
            {
                ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeyFormat,
                    computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                    computedProperty.PropertyName, format,
                    $"Format requires {placeholderCount} parameters but only {computedKey.SourceProperties.Length} source properties provided");
            }
        }
        catch (Exception)
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeyFormat,
                computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                computedProperty.PropertyName, format, "Invalid format string");
        }
    }

    private void ValidateComputedKeyCircularDependencies(PropertyModel[] computedProperties, EntityModel entityModel)
    {
        var dependencyGraph = new Dictionary<string, HashSet<string>>();

        // Build dependency graph
        foreach (var computedProperty in computedProperties)
        {
            var dependencies = new HashSet<string>();
            foreach (var sourceProperty in computedProperty.ComputedKey!.SourceProperties)
            {
                dependencies.Add(sourceProperty);
            }
            dependencyGraph[computedProperty.PropertyName] = dependencies;
        }

        // Check for circular dependencies using DFS
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var computedProperty in computedProperties)
        {
            if (HasCircularDependency(computedProperty.PropertyName, dependencyGraph, visited, recursionStack, out var cycle))
            {
                ReportDiagnostic(DiagnosticDescriptors.CircularKeyDependency,
                    computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                    cycle);
                break;
            }
        }
    }

    private bool HasCircularDependency(string propertyName, Dictionary<string, HashSet<string>> dependencyGraph,
        HashSet<string> visited, HashSet<string> recursionStack, out string cycle)
    {
        cycle = string.Empty;

        if (recursionStack.Contains(propertyName))
        {
            cycle = string.Join(" -> ", recursionStack) + " -> " + propertyName;
            return true;
        }

        if (visited.Contains(propertyName))
            return false;

        visited.Add(propertyName);
        recursionStack.Add(propertyName);

        if (dependencyGraph.TryGetValue(propertyName, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (HasCircularDependency(dependency, dependencyGraph, visited, recursionStack, out cycle))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(propertyName);
        return false;
    }

    private void ValidateEntityComplexity(EntityModel entityModel)
    {
        // Check for too many attributes
        var attributeCount = entityModel.Properties.Count(p => p.HasAttributeMapping);
        if (attributeCount > 50)
        {
            ReportDiagnostic(DiagnosticDescriptors.TooManyAttributes,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName, attributeCount);
        }

        // Check for complex nested structures
        var complexProperties = entityModel.Properties.Count(p => !IsPrimitiveType(p.PropertyType) && !p.IsCollection);
        if (complexProperties > 10)
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName, "Complex nested structure",
                $"Entity has {complexProperties} complex properties which may impact serialization performance");
        }
    }

    private void ValidateEntityScalability(EntityModel entityModel)
    {
        // Check for GSI overuse (keep this check as it's valid)
        if (entityModel.Indexes.Length > 5)
        {
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity has {entityModel.Indexes.Length} GSIs which may impact write performance and costs");
        }

        // Check for multi-item entities with complex collections (scalability concern)
        if (entityModel.IsMultiItemEntity)
        {
            var complexCollectionCount = entityModel.Properties.Count(p =>
                p.IsCollection && p.HasAttributeMapping && IsComplexCollectionType(p.PropertyType));

            if (complexCollectionCount > 2)
            {
                ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    entityModel.ClassName,
                    $"Multi-item entity with {complexCollectionCount} complex collections may not scale well");
            }
        }

        // Check for entities with many complex properties (potential scalability issue)
        var complexPropertyCount = entityModel.Properties.Count(p =>
            p.HasAttributeMapping && (IsComplexCollectionType(p.PropertyType) || IsComplexNestedType(p.PropertyType)));

        if (complexPropertyCount >= 3)
        {
            ReportDiagnostic(DiagnosticDescriptors.ScalabilityWarning,
                entityModel.ClassDeclaration?.Identifier.GetLocation(),
                entityModel.ClassName,
                $"Entity with {complexPropertyCount} complex properties may impact DynamoDB performance and scalability");
        }
    }

    private void ValidateCircularReferences(EntityModel entityModel)
    {
        // Basic circular reference detection for related entities
        var entityTypeName = entityModel.ClassName;

        foreach (var relationship in entityModel.Relationships)
        {
            if (!string.IsNullOrEmpty(relationship.EntityType))
            {
                // Check if related entity references back to this entity
                if (relationship.EntityType == entityTypeName)
                {
                    ReportDiagnostic(DiagnosticDescriptors.CircularReferenceDetected,
                        entityModel.ClassDeclaration?.Identifier.GetLocation(),
                        entityModel.ClassName);
                    break;
                }
            }
        }

        // Check for self-referencing collection properties
        foreach (var property in entityModel.Properties.Where(p => p.IsCollection))
        {
            var elementType = GetCollectionElementType(property.PropertyType);
            if (elementType == entityTypeName)
            {
                ReportDiagnostic(DiagnosticDescriptors.CircularReferenceDetected,
                    entityModel.ClassDeclaration?.Identifier.GetLocation(),
                    entityModel.ClassName);
                break;
            }
        }
    }

    private void ValidateAdvancedTypes(EntityModel entityModel)
    {
        var validator = new AdvancedTypeValidator();

        // Check for package references using semantic model
        var compilation = entityModel.SemanticModel?.Compilation;
        var hasJsonSerializerPackage = false;
        var hasBlobProviderPackage = false;
        
        if (compilation != null)
        {
            hasJsonSerializerPackage = HasJsonSerializerPackage(compilation);
            hasBlobProviderPackage = HasBlobProviderPackage(compilation);
        }

        // Validate each property with advanced types
        foreach (var property in entityModel.Properties)
        {
            if (property.AdvancedType?.HasAdvancedType == true)
            {
                validator.ValidateProperty(
                    property,
                    property.AdvancedType,
                    hasJsonSerializerPackage,
                    hasBlobProviderPackage,
                    entityModel.SemanticModel!);
            }
        }

        // Validate entity-level constraints (e.g., only one TTL field)
        validator.ValidateEntityTtlFields(entityModel);

        // Add all diagnostics from validator
        foreach (var diagnostic in validator.Diagnostics)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    private bool HasJsonSerializerPackage(Compilation compilation)
    {
        // Check for System.Text.Json package
        var hasSystemTextJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.SystemTextJson", StringComparison.OrdinalIgnoreCase));

        // Check for Newtonsoft.Json package
        var hasNewtonsoftJson = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.NewtonsoftJson", StringComparison.OrdinalIgnoreCase));

        return hasSystemTextJson || hasNewtonsoftJson;
    }

    private bool HasBlobProviderPackage(Compilation compilation)
    {
        // Check for S3 blob provider package
        var hasS3Provider = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.BlobStorage.S3", StringComparison.OrdinalIgnoreCase));

        // Could add checks for other blob provider packages here
        return hasS3Provider;
    }

    private void ValidateSecurityAttributes(EntityModel entityModel)
    {
        var compilation = entityModel.SemanticModel?.Compilation;
        if (compilation == null)
            return;

        // Check if Encryption.Kms package is referenced
        var hasEncryptionKms = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.Encryption.Kms", StringComparison.OrdinalIgnoreCase));

        // Check for encrypted properties
        foreach (var property in entityModel.Properties)
        {
            if (property.Security?.IsEncrypted == true && !hasEncryptionKms)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.MissingEncryptionKms,
                    property.PropertyDeclaration?.Identifier.GetLocation(),
                    property.PropertyName,
                    entityModel.ClassName);
            }
        }
    }

    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }

    private static bool IsCriticalError(string diagnosticId)
    {
        // Only these errors prevent code generation
        return diagnosticId switch
        {
            "DYNDB001" => true, // Missing partition key
            "DYNDB002" => true, // Multiple partition keys
            "DYNDB010" => true, // Entity must be partial
            "DYNDB007" => false, // Missing DynamoDbAttribute - not critical, can still generate
            _ => false
        };
    }

    /// <summary>
    /// Detects whether the Oproto.FluentDynamoDb.Geospatial package is referenced in the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <returns>True if the geospatial package is referenced, false otherwise.</returns>
    private static bool DetectGeospatialPackage(Compilation compilation)
    {
        return compilation.ReferencedAssemblyNames
            .Any(a => a.Name == "Oproto.FluentDynamoDb.Geospatial");
    }
}