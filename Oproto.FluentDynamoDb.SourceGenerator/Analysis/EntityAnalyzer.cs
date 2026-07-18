using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes class and record declarations to extract DynamoDB entity information.
/// </summary>
internal class EntityAnalyzer
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the diagnostics collected during analysis.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Analyzes a type declaration (class or record) and extracts entity model information.
    /// </summary>
    /// <param name="typeDecl">The type declaration to analyze (class or record).</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <returns>The extracted entity model, or null if analysis failed.</returns>
    public EntityModel? AnalyzeEntity(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
        _diagnostics.Clear();

        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (typeSymbol == null)
            return null;

        // Check if type is partial
        if (!IsPartialType(typeDecl))
        {
            ReportDiagnostic(DiagnosticDescriptors.EntityMustBePartial, typeDecl.Identifier.GetLocation(), typeSymbol.Name);
            return null;
        }

        // For backward compatibility, store as ClassDeclaration if it's a class
        // Records are also stored here since they're reference types
        var classDecl = typeDecl as ClassDeclarationSyntax;

        var entityModel = new EntityModel
        {
            ClassName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            ClassDeclaration = classDecl,
            TypeDeclaration = typeDecl,
            SemanticModel = semanticModel,
            IsRecord = typeDecl is RecordDeclarationSyntax
        };

        // Detect JSON serializer configuration
        var jsonSerializerInfo = JsonSerializerDetector.DetectJsonSerializer(semanticModel.Compilation);
        entityModel.JsonSerializerInfo = jsonSerializerInfo;

        // Detect geospatial package
        entityModel.HasGeospatialPackage = DetectGeospatialPackage(semanticModel.Compilation);

        // Extract table information
        if (!ExtractTableInfo(typeDecl, semanticModel, entityModel))
            return null;

        // Extract property information
        ExtractProperties(typeDecl, semanticModel, entityModel);

        // Validate individual properties
        foreach (var property in entityModel.Properties)
        {
            ValidatePropertyModel(property, semanticModel);
            // ValidatePropertyPerformance is called internally by ValidatePropertyModel
        }

        // Validate entity configuration
        ValidateEntityModel(entityModel);

        // Extract index information
        ExtractIndexes(entityModel);

        // Validate new index attribute configurations (DYNDB120-127)
        ValidateIndexAttributes(entityModel);

        // Validate index projection configurations
        ValidateIndexProjectionConfiguration(entityModel);

        // Extract relationship information
        ExtractRelationships(typeDecl, semanticModel, entityModel);

        // Set IsMultiItemEntity based on relationships (must be after ExtractRelationships)
        entityModel.IsMultiItemEntity = entityModel.Relationships.Length > 0;

        // Validate related entity configurations (must be after ExtractRelationships)
        if (entityModel.Relationships.Length > 0)
        {
            ValidateRelatedEntityConfiguration(entityModel);
        }

        // === Unified Key Format & Discriminator Analysis ===
        ComputeNormalizedKeyFormats(entityModel);               // Step 1: Populate NormalizedKeyFormat
        DeriveDiscriminatorPatterns(entityModel);                // Step 2: Populate DerivedDiscriminatorPattern
        ValidatePrefixFormatConsistency(entityModel);            // Step 3: FDDB100
        ApplyAutoDerivedDiscriminator(entityModel);              // Step 4: Set entity.Discriminator
        ApplyAutoDerivedGsiDiscriminator(entityModel);           // Step 5: Set index.GsiDiscriminator
        ValidateExplicitVsDerivedDiscriminator(entityModel);     // Step 6: FDDB101
        DetectRedundantExplicitDiscriminator(entityModel);       // Step 7: FDDB103
        // === END Unified Key Format & Discriminator Analysis ===

        // Only return null if there are critical errors that prevent code generation
        var criticalErrors = _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error && IsCriticalError(d.Id)).ToArray();
        return criticalErrors.Length > 0 ? null : entityModel;
    }

    /// <summary>
    /// Analyzes a class declaration and extracts entity model information.
    /// This overload is provided for backward compatibility.
    /// </summary>
    public EntityModel? AnalyzeEntity(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        return AnalyzeEntity((TypeDeclarationSyntax)classDecl, semanticModel);
    }

    private bool IsPartialType(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl.Modifiers.Any(m => m.ValueText == "partial");
    }

    private bool ExtractTableInfo(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var tableAttribute = GetAttribute(typeDecl, semanticModel, "DynamoDbTableAttribute");
        
        // Check if this is a DynamoDbEntity (nested type) instead of a DynamoDbTable
        if (tableAttribute == null)
        {
            var entityAttribute = GetAttribute(typeDecl, semanticModel, "DynamoDbEntityAttribute");
            if (entityAttribute != null)
            {
                // This is a nested entity type - no table name required
                // Set a placeholder table name to indicate it's an entity
                entityModel.TableName = $"_entity_{entityModel.ClassName}";
                return true;
            }
            return false;
        }

        // Extract table name or type from constructor argument
        var firstArg = tableAttribute.ArgumentList?.Arguments.FirstOrDefault();
        if (firstArg?.Expression is LiteralExpressionSyntax tableNameLiteral)
        {
            // String-based table reference: [DynamoDbTable("TableName")]
            entityModel.TableName = tableNameLiteral.Token.ValueText;
        }
        else if (firstArg?.Expression is TypeOfExpressionSyntax typeOfExpr)
        {
            // Type-based table reference: [DynamoDbTable(typeof(MyTable))]
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            if (typeInfo.Type is INamedTypeSymbol namedType)
            {
                entityModel.IsTableTypeReference = true;
                entityModel.TableTypeName = namedType.Name;
                entityModel.TableNamespace = namedType.ContainingNamespace.ToDisplayString();
                
                // Use the type name as the table name for grouping entities
                entityModel.TableName = namedType.Name;
                
                // Validate that the referenced type is partial
                ValidateTableTypeIsPartial(typeOfExpr, namedType, semanticModel);
            }
        }

        // Extract IsDefault and Namespace properties from named arguments
        if (tableAttribute.ArgumentList != null)
        {
            foreach (var arg in tableAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "IsDefault" &&
                    arg.Expression is LiteralExpressionSyntax isDefaultLiteral)
                {
                    entityModel.IsDefault = bool.Parse(isDefaultLiteral.Token.ValueText);
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "Namespace" &&
                    arg.Expression is LiteralExpressionSyntax namespaceLiteral)
                {
                    // Only override namespace for string-based table references
                    // Type-based references use the type's namespace
                    if (!entityModel.IsTableTypeReference)
                    {
                        entityModel.TableNamespace = namespaceLiteral.Token.ValueText;
                    }
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
        ExtractScannableAttribute(typeDecl, semanticModel, entityModel);

        // Extract require write transaction attribute
        ExtractRequireWriteTransactionAttribute(typeDecl, semanticModel, entityModel);

        // Extract entity property configuration
        ExtractEntityPropertyConfiguration(typeDecl, semanticModel, entityModel);

        // Extract accessor configurations
        ExtractAccessorConfigurations(typeDecl, semanticModel, entityModel);

        // Extract stream conversion attribute
        ExtractStreamConversionAttribute(typeDecl, semanticModel, entityModel);

        // Extract dynamic fields attribute
        ExtractEnableDynamicFieldsAttribute(typeDecl, semanticModel, entityModel);

        // Extract UseFluentResults attribute
        ExtractUseFluentResultsAttribute(typeDecl, semanticModel, entityModel);

        return !string.IsNullOrEmpty(entityModel.TableName);
    }

    private void ExtractEnableDynamicFieldsAttribute(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var enableDynamicFieldsAttribute = GetAttribute(typeDecl, semanticModel, "EnableDynamicFieldsAttribute");
        if (enableDynamicFieldsAttribute == null)
        {
            entityModel.EnableDynamicFields = false;
            return;
        }

        // Check if type is partial - emit diagnostic if not
        if (!IsPartialType(typeDecl))
        {
            ReportDiagnostic(DiagnosticDescriptors.EnableDynamicFieldsRequiresPartial, 
                enableDynamicFieldsAttribute.GetLocation(), 
                entityModel.ClassName);
            entityModel.EnableDynamicFields = false;
            return;
        }

        // Check if type already has a DynamicFields property
        var existingDynamicFieldsProperty = typeDecl.Members
            .OfType<PropertyDeclarationSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == "DynamicFields");
        
        if (existingDynamicFieldsProperty != null)
        {
            ReportDiagnostic(DiagnosticDescriptors.DynamicFieldsPropertyAlreadyExists,
                enableDynamicFieldsAttribute.GetLocation(),
                entityModel.ClassName);
        }

        entityModel.EnableDynamicFields = true;
        
        // Default to sensitive logging (values redacted)
        entityModel.DynamicFieldsSensitiveLogging = true;

        // Extract SensitiveLogging property if specified
        if (enableDynamicFieldsAttribute.ArgumentList != null)
        {
            foreach (var arg in enableDynamicFieldsAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "SensitiveLogging" &&
                    arg.Expression is LiteralExpressionSyntax sensitiveLoggingLiteral)
                {
                    entityModel.DynamicFieldsSensitiveLogging = bool.Parse(sensitiveLoggingLiteral.Token.ValueText);
                }
            }
        }
    }

    private void ExtractScannableAttribute(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var scannableAttribute = GetAttribute(typeDecl, semanticModel, "ScannableAttribute");
        entityModel.IsScannable = scannableAttribute != null;
    }

    private void ExtractRequireWriteTransactionAttribute(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var requireWriteTransactionAttribute = GetAttribute(typeDecl, semanticModel, "RequireWriteTransactionAttribute");
        entityModel.RequiresWriteTransaction = requireWriteTransactionAttribute != null;
    }

    private void ExtractUseFluentResultsAttribute(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var useFluentResultsAttribute = GetAttribute(typeDecl, semanticModel, "UseFluentResultsAttribute");
        if (useFluentResultsAttribute == null)
        {
            entityModel.UseFluentResults = false;
            return;
        }

        entityModel.UseFluentResults = true;
        
        // Default to hiding traditional async methods
        entityModel.HideGeneratedAsyncMethods = true;

        // Extract HideGeneratedAsyncMethods property if specified
        if (useFluentResultsAttribute.ArgumentList != null)
        {
            foreach (var arg in useFluentResultsAttribute.ArgumentList.Arguments)
            {
                if (arg.NameEquals?.Name.Identifier.ValueText == "HideGeneratedAsyncMethods" &&
                    arg.Expression is LiteralExpressionSyntax hideAsyncMethodsLiteral)
                {
                    entityModel.HideGeneratedAsyncMethods = bool.Parse(hideAsyncMethodsLiteral.Token.ValueText);
                }
            }
        }
    }

    private void ExtractEntityPropertyConfiguration(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var entityPropertyAttribute = GetAttribute(typeDecl, semanticModel, "GenerateEntityPropertyAttribute");
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
                                typeDecl.Identifier.GetLocation(),
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

    private void ExtractAccessorConfigurations(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var accessorAttributes = GetAttributes(typeDecl, semanticModel, "GenerateAccessorsAttribute");
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

    private void ExtractStreamConversionAttribute(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var streamConversionAttribute = GetAttribute(typeDecl, semanticModel, "GenerateStreamConversionAttribute");
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

    private void ExtractProperties(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var properties = new List<PropertyModel>();

        foreach (var member in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
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

        // Extract Format, DateTimeKind, GeoHashPrecision, and spatial index properties from DynamoDbAttribute if present
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
                else if (arg.NameEquals?.Name.Identifier.ValueText == "SpatialIndexType" &&
                         arg.Expression is MemberAccessExpressionSyntax spatialIndexTypeExpr)
                {
                    // Extract the enum value (e.g., SpatialIndexType.S2 -> "S2")
                    var indexTypeName = spatialIndexTypeExpr.Name.Identifier.ValueText;
                    propertyModel.SpatialIndexType = indexTypeName;
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "S2Level" &&
                         arg.Expression is LiteralExpressionSyntax s2LevelLiteral)
                {
                    if (int.TryParse(s2LevelLiteral.Token.ValueText, out var level))
                    {
                        propertyModel.S2Level = level;
                    }
                }
                else if (arg.NameEquals?.Name.Identifier.ValueText == "H3Resolution" &&
                         arg.Expression is LiteralExpressionSyntax h3ResolutionLiteral)
                {
                    if (int.TryParse(h3ResolutionLiteral.Token.ValueText, out var resolution))
                    {
                        propertyModel.H3Resolution = resolution;
                    }
                }
            }
        }

        // Extract key attributes
        ExtractKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Detect constant key values (expression-body or read-only auto-property)
        DetectConstantKeyValue(propertyDecl, semanticModel, propertyModel);

        // Extract new GSI partition key attributes
        ExtractGsiPartitionKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract new GSI sort key attributes
        ExtractGsiSortKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract new LSI sort key attributes
        ExtractLsiSortKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract computed key attributes
        ExtractComputedKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract extracted key attributes
        ExtractExtractedKeyAttributes(propertyDecl, semanticModel, propertyModel);

        // Extract coordinate storage attributes
        ExtractCoordinateStorageAttributes(propertyDecl, semanticModel, propertyModel);

        // Check for RelatedEntity attribute (used to suppress DYNDB023 performance warnings)
        var relatedEntityAttr = GetAttribute(propertyDecl, semanticModel, "RelatedEntityAttribute");
        propertyModel.IsRelatedEntity = relatedEntityAttr != null;

        // Analyze complex type information
        var complexTypeAnalyzer = new ComplexTypeAnalyzer();
        propertyModel.ComplexType = complexTypeAnalyzer.AnalyzeProperty(propertyModel, semanticModel);

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

    /// <summary>
    /// Detects whether a key property has a compile-time constant value via expression-body
    /// or read-only auto-property syntax. Sets PropertyModel.ConstantKeyValue if detected.
    /// </summary>
    private void DetectConstantKeyValue(
        PropertyDeclarationSyntax propertyDecl,
        SemanticModel semanticModel,
        PropertyModel propertyModel)
    {
        // Only applies to key properties
        if (!propertyModel.IsPartitionKey && !propertyModel.IsSortKey)
            return;

        // Case 1: Expression-body property (public string Sk => "PROFILE")
        if (propertyDecl.ExpressionBody != null)
        {
            var expr = propertyDecl.ExpressionBody.Expression;
            var constantValue = semanticModel.GetConstantValue(expr);
            if (constantValue.HasValue && constantValue.Value is string strValue)
            {
                propertyModel.ConstantKeyValue = strValue;
            }
            return;
        }

        // Case 2: Read-only auto-property (public string Sk { get; } = "PROFILE")
        if (propertyDecl.AccessorList != null)
        {
            var accessors = propertyDecl.AccessorList.Accessors;
            bool hasOnlyGet = accessors.Count == 1
                && accessors[0].Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration;

            if (hasOnlyGet && propertyDecl.Initializer != null)
            {
                var initExpr = propertyDecl.Initializer.Value;
                var constantValue = semanticModel.GetConstantValue(initExpr);
                if (constantValue.HasValue && constantValue.Value is string strValue)
                {
                    propertyModel.ConstantKeyValue = strValue;
                }
            }
        }
    }

    private void ExtractGsiPartitionKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var gsiPkAttributes = GetAttributes(propertyDecl, semanticModel, "GsiPartitionKeyAttribute");
        var gsiPkModels = new List<GsiPartitionKeyModel>();

        foreach (var gsiPkAttr in gsiPkAttributes)
        {
            var gsiPkModel = new GsiPartitionKeyModel();

            // Extract index name from constructor argument
            if (gsiPkAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax indexNameLiteral)
            {
                gsiPkModel.IndexName = indexNameLiteral.Token.ValueText;
            }

            // Extract named arguments
            if (gsiPkAttr.ArgumentList != null)
            {
                foreach (var arg in gsiPkAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "Name" when arg.Expression is LiteralExpressionSyntax nameLiteral:
                            gsiPkModel.CustomName = nameLiteral.Token.ValueText;
                            break;
                        case "ProjectionType" when arg.Expression is MemberAccessExpressionSyntax projectionTypeExpr:
                            var projectionTypeName = projectionTypeExpr.Name.Identifier.ValueText;
                            if (Enum.TryParse<ProjectionType>(projectionTypeName, out var projectionType))
                            {
                                gsiPkModel.ProjectionType = projectionType;
                            }
                            break;
                    }
                }
            }

            // Extract GSI-specific discriminator configuration
            gsiPkModel.Discriminator = DiscriminatorAnalyzer.AnalyzeGsiDiscriminator(
                gsiPkAttr,
                semanticModel,
                gsiPkModel.IndexName,
                _diagnostics);

            gsiPkModels.Add(gsiPkModel);
        }

        propertyModel.GsiPartitionKeys = gsiPkModels.ToArray();
    }

    private void ExtractGsiSortKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var gsiSkAttributes = GetAttributes(propertyDecl, semanticModel, "GsiSortKeyAttribute");
        var gsiSkModels = new List<GsiSortKeyModel>();

        foreach (var gsiSkAttr in gsiSkAttributes)
        {
            var gsiSkModel = new GsiSortKeyModel();

            // Extract index name from constructor argument
            if (gsiSkAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax indexNameLiteral)
            {
                gsiSkModel.IndexName = indexNameLiteral.Token.ValueText;
            }

            // Extract named arguments
            if (gsiSkAttr.ArgumentList != null)
            {
                foreach (var arg in gsiSkAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "Name" when arg.Expression is LiteralExpressionSyntax nameLiteral:
                            gsiSkModel.CustomName = nameLiteral.Token.ValueText;
                            break;
                        case "ProjectionType" when arg.Expression is MemberAccessExpressionSyntax projectionTypeExpr:
                            var projectionTypeName = projectionTypeExpr.Name.Identifier.ValueText;
                            if (Enum.TryParse<ProjectionType>(projectionTypeName, out var projectionType))
                            {
                                gsiSkModel.ProjectionType = projectionType;
                            }
                            break;
                    }
                }
            }

            gsiSkModels.Add(gsiSkModel);
        }

        propertyModel.GsiSortKeys = gsiSkModels.ToArray();
    }

    private void ExtractLsiSortKeyAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        var lsiSkAttributes = GetAttributes(propertyDecl, semanticModel, "LsiSortKeyAttribute");
        var lsiSkModels = new List<LsiSortKeyModel>();

        foreach (var lsiSkAttr in lsiSkAttributes)
        {
            var lsiSkModel = new LsiSortKeyModel();

            // Extract index name from constructor argument
            if (lsiSkAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax indexNameLiteral)
            {
                lsiSkModel.IndexName = indexNameLiteral.Token.ValueText;
            }

            // Extract named arguments
            if (lsiSkAttr.ArgumentList != null)
            {
                foreach (var arg in lsiSkAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "Name" when arg.Expression is LiteralExpressionSyntax nameLiteral:
                            lsiSkModel.CustomName = nameLiteral.Token.ValueText;
                            break;
                        case "ProjectionType" when arg.Expression is MemberAccessExpressionSyntax projectionTypeExpr:
                            var projectionTypeName = projectionTypeExpr.Name.Identifier.ValueText;
                            if (Enum.TryParse<ProjectionType>(projectionTypeName, out var projectionType))
                            {
                                lsiSkModel.ProjectionType = projectionType;
                            }
                            break;
                    }
                }
            }

            lsiSkModels.Add(lsiSkModel);
        }

        propertyModel.LsiSortKeys = lsiSkModels.ToArray();
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
                // Skip named arguments, handle positional arguments (source properties)
                if (arg.NameEquals != null)
                    continue;

                if (arg.Expression is LiteralExpressionSyntax literal)
                {
                    sourceProperties.Add(literal.Token.ValueText);
                }
                else
                {
                    // Fallback: resolve compile-time constants (nameof, const, etc.)
                    var constantValue = semanticModel.GetConstantValue(arg.Expression);
                    if (constantValue.HasValue && constantValue.Value is string strValue)
                    {
                        sourceProperties.Add(strValue);
                    }
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
            else
            {
                // Fallback: resolve compile-time constants (nameof, const, etc.)
                var constantValue = semanticModel.GetConstantValue(args[0].Expression);
                if (constantValue.HasValue && constantValue.Value is string strValue)
                {
                    extractedModel.SourceProperty = strValue;
                }
            }

            // Second argument: index
            if (args[1].Expression is LiteralExpressionSyntax indexLiteral &&
                int.TryParse(indexLiteral.Token.ValueText, out var index))
            {
                extractedModel.Index = index;
            }
            else
            {
                // Fallback: resolve compile-time constants (const int, etc.)
                var indexConstant = semanticModel.GetConstantValue(args[1].Expression);
                if (indexConstant.HasValue && indexConstant.Value is int intValue)
                {
                    extractedModel.Index = intValue;
                }
                else if (indexConstant.HasValue)
                {
                    // Handle other integer types (short, byte, etc.)
                    try
                    {
                        extractedModel.Index = Convert.ToInt32(indexConstant.Value);
                    }
                    catch
                    {
                        // If conversion fails, leave Index at default (0)
                    }
                }
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

    private void ExtractCoordinateStorageAttributes(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        // Only process GeoLocation properties
        if (!propertyModel.PropertyType.Contains("GeoLocation"))
            return;

        // Check for StoreCoordinatesAttribute
        var storeCoordinatesAttr = GetAttribute(propertyDecl, semanticModel, "StoreCoordinatesAttribute");
        if (storeCoordinatesAttr != null)
        {
            // Extract named arguments
            if (storeCoordinatesAttr.ArgumentList != null)
            {
                foreach (var arg in storeCoordinatesAttr.ArgumentList.Arguments)
                {
                    switch (arg.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "LatitudeAttributeName" when arg.Expression is LiteralExpressionSyntax latLiteral:
                            propertyModel.LatitudeAttributeName = latLiteral.Token.ValueText;
                            break;
                        case "LongitudeAttributeName" when arg.Expression is LiteralExpressionSyntax lonLiteral:
                            propertyModel.LongitudeAttributeName = lonLiteral.Token.ValueText;
                            break;
                    }
                }
            }
        }

        // If StoreCoordinatesAttribute is not present, check for computed properties
        // that reference this GeoLocation property (Option 2 from design)
        if (!propertyModel.HasCoordinateStorage)
        {
            DetectComputedCoordinateProperties(propertyDecl, semanticModel, propertyModel);
        }
    }

    private void DetectComputedCoordinateProperties(PropertyDeclarationSyntax propertyDecl, SemanticModel semanticModel, PropertyModel propertyModel)
    {
        // Get the containing class
        var classDecl = propertyDecl.Parent as ClassDeclarationSyntax;
        if (classDecl == null)
            return;

        // Look for properties that are computed from this GeoLocation property
        // Pattern: public double Latitude => Location.Latitude;
        // Pattern: public double Longitude => Location.Longitude;
        
        string? latitudeAttributeName = null;
        string? longitudeAttributeName = null;

        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            // Skip the current property
            if (member == propertyDecl)
                continue;

            // Check if this is a computed property (has only a getter with expression body)
            if (member.ExpressionBody == null && member.AccessorList?.Accessors.Count != 1)
                continue;

            var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;
            if (propertySymbol == null)
                continue;

            // Check if property type is double or double?
            var isDouble = propertySymbol.Type.SpecialType == SpecialType.System_Double ||
                          (propertySymbol.Type is INamedTypeSymbol namedType &&
                           namedType.IsGenericType &&
                           namedType.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T &&
                           namedType.TypeArguments[0].SpecialType == SpecialType.System_Double);

            if (!isDouble)
                continue;

            // Check if the expression references our GeoLocation property
            var expression = member.ExpressionBody?.Expression ??
                           (member.AccessorList?.Accessors.FirstOrDefault()?.ExpressionBody?.Expression);

            if (expression == null)
                continue;

            var expressionText = expression.ToString();

            // Check if it references our property's Latitude or Longitude
            if (expressionText.Contains($"{propertyModel.PropertyName}.Latitude"))
            {
                // This is a latitude property - get its DynamoDbAttribute name
                var dynamoDbAttr = GetAttribute(member, semanticModel, "DynamoDbAttributeAttribute") ??
                                 GetAttribute(member, semanticModel, "DynamoDbAttribute");
                
                if (dynamoDbAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax latAttrName)
                {
                    latitudeAttributeName = latAttrName.Token.ValueText;
                }
            }
            else if (expressionText.Contains($"{propertyModel.PropertyName}.Longitude"))
            {
                // This is a longitude property - get its DynamoDbAttribute name
                var dynamoDbAttr = GetAttribute(member, semanticModel, "DynamoDbAttributeAttribute") ??
                                 GetAttribute(member, semanticModel, "DynamoDbAttribute");
                
                if (dynamoDbAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax lonAttrName)
                {
                    longitudeAttributeName = lonAttrName.Token.ValueText;
                }
            }
        }

        // If we found both latitude and longitude computed properties, set them
        if (!string.IsNullOrEmpty(latitudeAttributeName) && !string.IsNullOrEmpty(longitudeAttributeName))
        {
            propertyModel.LatitudeAttributeName = latitudeAttributeName;
            propertyModel.LongitudeAttributeName = longitudeAttributeName;
        }
    }

    private void ExtractIndexes(EntityModel entityModel)
    {
        var indexes = new Dictionary<string, IndexModel>();

        // Extract GSI indexes from GsiPartitionKey attributes
        foreach (var property in entityModel.Properties)
        {
            foreach (var gsiPk in property.GsiPartitionKeys)
            {
                if (!indexes.TryGetValue(gsiPk.IndexName, out var indexModel))
                {
                    indexModel = new IndexModel
                    {
                        IndexName = gsiPk.IndexName,
                        IndexType = IndexType.GlobalSecondaryIndex,
                        ProjectionType = gsiPk.ProjectionType
                    };
                    indexes[gsiPk.IndexName] = indexModel;
                }

                indexModel.PartitionKeyProperty = property.PropertyName;
                indexModel.PartitionKeyAttribute = property.AttributeName;

                // GsiPartitionKey values take precedence
                if (gsiPk.ProjectionType != ProjectionType.All)
                    indexModel.ProjectionType = gsiPk.ProjectionType;
                if (gsiPk.Discriminator != null && indexModel.GsiDiscriminator == null)
                    indexModel.GsiDiscriminator = gsiPk.Discriminator;
                if (!string.IsNullOrEmpty(gsiPk.CustomName) && string.IsNullOrEmpty(indexModel.CustomName))
                    indexModel.CustomName = gsiPk.CustomName;
            }
        }

        // Extract GSI sort keys
        foreach (var property in entityModel.Properties)
        {
            foreach (var gsiSk in property.GsiSortKeys)
            {
                if (!indexes.TryGetValue(gsiSk.IndexName, out var indexModel))
                {
                    indexModel = new IndexModel
                    {
                        IndexName = gsiSk.IndexName,
                        IndexType = IndexType.GlobalSecondaryIndex,
                        ProjectionType = gsiSk.ProjectionType
                    };
                    indexes[gsiSk.IndexName] = indexModel;
                }

                indexModel.SortKeyProperty = property.PropertyName;
                indexModel.SortKeyAttribute = property.AttributeName;

                // GsiSortKey values are fallbacks (only if GsiPartitionKey didn't set them)
                if (!string.IsNullOrEmpty(gsiSk.CustomName) && string.IsNullOrEmpty(indexModel.CustomName))
                    indexModel.CustomName = gsiSk.CustomName;
                if (gsiSk.ProjectionType != ProjectionType.All && indexModel.ProjectionType == ProjectionType.All)
                    indexModel.ProjectionType = gsiSk.ProjectionType;
            }
        }

        // Extract LSI indexes
        var partitionKeyProperty = entityModel.Properties.FirstOrDefault(p => p.IsPartitionKey);
        foreach (var property in entityModel.Properties)
        {
            foreach (var lsiSk in property.LsiSortKeys)
            {
                if (!indexes.TryGetValue(lsiSk.IndexName, out var indexModel))
                {
                    indexModel = new IndexModel
                    {
                        IndexName = lsiSk.IndexName,
                        IndexType = IndexType.LocalSecondaryIndex,
                        PartitionKeyProperty = partitionKeyProperty?.PropertyName ?? string.Empty,
                        PartitionKeyAttribute = partitionKeyProperty?.AttributeName ?? string.Empty,
                        ProjectionType = lsiSk.ProjectionType
                    };
                    indexes[lsiSk.IndexName] = indexModel;
                }

                indexModel.SortKeyProperty = property.PropertyName;
                indexModel.SortKeyAttribute = property.AttributeName;

                if (!string.IsNullOrEmpty(lsiSk.CustomName) && string.IsNullOrEmpty(indexModel.CustomName))
                    indexModel.CustomName = lsiSk.CustomName;
            }
        }

        // Compute ResolvedPropertyName for all indexes
        foreach (var indexModel in indexes.Values)
        {
            indexModel.ResolvedPropertyName = !string.IsNullOrEmpty(indexModel.CustomName)
                ? indexModel.CustomName
                : ConvertToPascalCase(indexModel.IndexName);
        }

        entityModel.Indexes = indexes.Values.ToArray();
    }

    private void ExtractRelationships(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel, EntityModel entityModel)
    {
        var relationships = new List<RelationshipModel>();

        foreach (var member in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
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
                
                // Check if the child entity type has its own [RelatedEntity] relationships
                var childEntityTypeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                if (childEntityTypeInfo.Type is INamedTypeSymbol childTypeSymbol)
                {
                    var childRelationships = ExtractChildEntityRelationships(childTypeSymbol, semanticModel);
                    relationshipModel.ChildEntityHasRelationships = childRelationships.Length > 0;
                    relationshipModel.ChildEntityRelationships = childRelationships;
                }
            }

            relationships.Add(relationshipModel);
        }

        entityModel.Relationships = relationships.ToArray();
    }

    /// <summary>
    /// Extracts [RelatedEntity] relationships from a child entity type symbol.
    /// Used for recursive composite entity assembly detection.
    /// </summary>
    private RelationshipModel[] ExtractChildEntityRelationships(INamedTypeSymbol childTypeSymbol, SemanticModel semanticModel)
    {
        var relationships = new List<RelationshipModel>();

        foreach (var member in childTypeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            // Check if the property has [RelatedEntity] attribute
            var relatedEntityAttr = member.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "RelatedEntityAttribute");
            
            if (relatedEntityAttr == null)
                continue;

            var relationshipModel = new RelationshipModel
            {
                PropertyName = member.Name,
                PropertyType = member.Type.ToDisplayString(),
                IsCollection = IsCollectionType(member.Type)
            };

            // Extract sort key pattern from constructor argument
            if (relatedEntityAttr.ConstructorArguments.Length > 0 && 
                relatedEntityAttr.ConstructorArguments[0].Value is string pattern)
            {
                relationshipModel.SortKeyPattern = pattern;
            }

            // Extract entity type from named argument
            var entityTypeArg = relatedEntityAttr.NamedArguments
                .FirstOrDefault(na => na.Key == "EntityType");
            
            if (entityTypeArg.Value.Value is INamedTypeSymbol entityTypeSymbol)
            {
                relationshipModel.EntityType = entityTypeSymbol.ToDisplayString();
                
                // Recursively check if this grandchild entity also has relationships
                var grandchildRelationships = ExtractChildEntityRelationships(entityTypeSymbol, semanticModel);
                relationshipModel.ChildEntityHasRelationships = grandchildRelationships.Length > 0;
                relationshipModel.ChildEntityRelationships = grandchildRelationships;
            }

            relationships.Add(relationshipModel);
        }

        return relationships.ToArray();
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

        // Note: IsMultiItemEntity is set after ExtractRelationships based on whether the entity has relationships

        // Validate computed and extracted keys
        ValidateComputedAndExtractedKeys(entityModel);

        // Validate complex types (Map, Set, List, TTL, JsonBlob, BlobStorage)
        ValidateComplexTypes(entityModel);

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
        // Skip validation for complex types (Map, Set, List, TTL, JsonBlob, BlobStorage)
        // as they are validated separately
        var isComplexType = propertyModel.ComplexType != null && (
            propertyModel.ComplexType.IsMap ||
            propertyModel.ComplexType.IsSet ||
            propertyModel.ComplexType.IsList ||
            propertyModel.ComplexType.IsTtl ||
            propertyModel.ComplexType.IsJsonBlob ||
            propertyModel.ComplexType.IsBlobStorage);

        // Check if the property type is an enum using the semantic model
        var isEnum = false;
        if (propertyModel.PropertyDeclaration != null)
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyModel.PropertyDeclaration) as IPropertySymbol;
            if (propertySymbol != null)
            {
                var typeSymbol = propertySymbol.Type;

                // Direct enum type
                if (typeSymbol.TypeKind == TypeKind.Enum)
                {
                    isEnum = true;
                }
                // Nullable<T> where T is an enum
                else if (typeSymbol is INamedTypeSymbol namedType &&
                         namedType.IsGenericType &&
                         namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                         namedType.TypeArguments[0].TypeKind == TypeKind.Enum)
                {
                    isEnum = true;
                }
            }
        }

        propertyModel.IsEnum = isEnum;

        if (!isComplexType && !isEnum && !IsSupportedPropertyType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.UnsupportedPropertyType,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType);
        }

        // Validate nested map types have [DynamoDbEntity] for AOT compatibility
        if (propertyModel.ComplexType?.IsMap == true)
        {
            ValidateNestedMapType(propertyModel, semanticModel);
        }

        // Validate attribute name
        if (!string.IsNullOrEmpty(propertyModel.AttributeName))
        {
            ValidateAttributeName(propertyModel);
        }

        // Validate spatial index configuration
        ValidateSpatialIndexConfiguration(propertyModel, semanticModel);

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

        // FDDB120: Constant key + [Computed]
        if (propertyModel.IsConstantKey && propertyModel.IsComputed)
        {
            ReportDiagnostic(DiagnosticDescriptors.ConstantKeyComputedConflict,
                propertyModel.PropertyDeclaration?.GetLocation(),
                propertyModel.PropertyName);
        }

        // FDDB121: Constant key + Prefix
        if (propertyModel.IsConstantKey && propertyModel.KeyFormat?.Prefix != null)
        {
            ReportDiagnostic(DiagnosticDescriptors.ConstantKeyPrefixConflict,
                propertyModel.PropertyDeclaration?.GetLocation(),
                propertyModel.PropertyName);
        }

        // FDDB125: Computed key + Prefix conflict
        if (propertyModel.IsComputed &&
            (propertyModel.IsPartitionKey || propertyModel.IsSortKey) &&
            !string.IsNullOrEmpty(propertyModel.KeyFormat?.Prefix))
        {
            ReportDiagnostic(DiagnosticDescriptors.ComputedKeyPrefixConflict,
                propertyModel.PropertyDeclaration?.GetLocation(),
                propertyModel.PropertyName,
                propertyModel.KeyFormat!.Prefix!);
        }

        // FDDB123: Empty/whitespace constant value
        if (propertyModel.IsConstantKey && string.IsNullOrWhiteSpace(propertyModel.ConstantKeyValue))
        {
            ReportDiagnostic(DiagnosticDescriptors.ConstantKeyEmptyValue,
                propertyModel.PropertyDeclaration?.GetLocation(),
                propertyModel.PropertyName);
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
        
        // Handle List<T> types - extract the element type
        if (nestedTypeSymbol is INamedTypeSymbol namedType && 
            namedType.IsGenericType &&
            (namedType.Name == "List" || namedType.Name == "IList" || 
             namedType.Name == "ICollection" || namedType.Name == "IEnumerable"))
        {
            // Get the element type (T in List<T>)
            if (namedType.TypeArguments.Length > 0)
            {
                nestedTypeSymbol = namedType.TypeArguments[0];
            }
        }
        
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
        // Skip properties not mapped to DynamoDB — no DynamoDB warning is relevant
        if (!propertyModel.HasAttributeMapping)
        {
            return;
        }

        // Skip extracted properties — source-only, never serialized to DynamoDB
        if (propertyModel.IsExtracted)
        {
            return;
        }

        // Skip enum properties — simple value types stored as string/int in DynamoDB
        if (propertyModel.IsEnum)
        {
            return;
        }

        // Skip performance warnings for RelatedEntity properties - these are intentionally
        // designed for composite entity patterns and should not trigger DYNDB023 warnings
        if (propertyModel.IsRelatedEntity)
        {
            return;
        }

        // Note: String properties are not flagged - naming heuristics (e.g., "Description", "Content")
        // produce too many false positives. DynamoDB handles strings of any size natively, and item
        // size limits (400KB) are the real constraint, which is better validated at runtime.

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
            "System.Guid", "System.Byte[]", "Ulid", "System.Ulid",
            // .NET 6+ date/time types
            "DateOnly", "TimeOnly", "System.DateOnly", "System.TimeOnly",
            // Common enum types
            "DayOfWeek", "System.DayOfWeek",
            // Unsigned integer types and short
            "ulong", "uint", "ushort", "byte", "sbyte", "short",
            "System.UInt64", "System.UInt32", "System.UInt16", "System.Byte", "System.SByte", "System.Int16"
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
            TypeDeclarationSyntax typeDecl => typeDecl.AttributeLists,
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
        // FDDB124: Extracted property must not also have DynamoDbAttribute mapping
        if (extractedProperty.HasAttributeMapping)
        {
            ReportDiagnostic(DiagnosticDescriptors.ExtractedPropertyHasAttributeMapping,
                extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                extractedProperty.PropertyName);
            return;
        }

        var extractedKey = extractedProperty.ExtractedKey!;

        // Validate source property exists
        if (!propertyNames.Contains(extractedKey.SourceProperty))
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidExtractedKeySource,
                extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                extractedProperty.PropertyName, extractedKey.SourceProperty);
            return;
        }

        // FDDB122: Cannot extract from a constant key property
        var sourceProperty = entityModel.Properties.FirstOrDefault(p => p.PropertyName == extractedKey.SourceProperty);
        if (sourceProperty != null && sourceProperty.IsConstantKey)
        {
            ReportDiagnostic(DiagnosticDescriptors.ConstantKeyExtractedConflict,
                extractedProperty.PropertyDeclaration?.GetLocation(),
                extractedProperty.PropertyName, sourceProperty.PropertyName);
        }

        // Validate index is non-negative
        if (extractedKey.Index < 0)
        {
            ReportDiagnostic(DiagnosticDescriptors.InvalidExtractedKeyIndex,
                extractedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                extractedProperty.PropertyName, extractedKey.Index, extractedKey.SourceProperty);
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

                    // Extract index portion: everything before the first colon
                    var colonIndex = placeholderText.IndexOf(':');
                    var indexText = colonIndex >= 0
                        ? placeholderText.Substring(0, colonIndex)
                        : placeholderText;

                    if (int.TryParse(indexText, out var placeholderIndex) && placeholderIndex >= 0)
                    {
                        placeholderCount = Math.Max(placeholderCount, placeholderIndex + 1);
                    }
                    else
                    {
                        // Invalid placeholder format - the index portion is not a valid non-negative integer
                        ReportDiagnostic(DiagnosticDescriptors.InvalidComputedKeyFormat,
                            computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                            computedProperty.PropertyName, format, $"Invalid placeholder: {{{placeholderText}}}");
                        return;
                    }

                    i = endIndex;
                }
            }

            // For explicit formats (HasCustomFormat), emit FDDB090 error on placeholder count mismatch
            if (computedKey.HasCustomFormat && placeholderCount != computedKey.SourceProperties.Length)
            {
                ReportDiagnostic(DiagnosticDescriptors.ComputedFormatPlaceholderMismatch,
                    computedProperty.PropertyDeclaration?.Identifier.GetLocation(),
                    computedProperty.PropertyName, format, placeholderCount, computedKey.SourceProperties.Length);
                return;
            }

            // Check if placeholder count exceeds source property count (general warning for non-explicit formats)
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

    private void ValidateComplexTypes(EntityModel entityModel)
    {
        var validator = new ComplexTypeValidator();

        // Check for package references using semantic model
        var compilation = entityModel.SemanticModel?.Compilation;
        var hasJsonSerializerPackage = false;
        var hasBlobProviderPackage = false;
        
        if (compilation != null)
        {
            hasJsonSerializerPackage = HasJsonSerializerPackage(compilation);
            hasBlobProviderPackage = HasBlobProviderPackage(compilation);
        }

        // Validate each property with complex types
        foreach (var property in entityModel.Properties)
        {
            if (property.ComplexType?.HasComplexType == true)
            {
                validator.ValidateProperty(
                    property,
                    property.ComplexType,
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

    private void ValidateSpatialIndexConfiguration(PropertyModel propertyModel, SemanticModel semanticModel)
    {
        // Check if any spatial index configuration is present
        var hasSpatialConfig = propertyModel.SpatialIndexType != null ||
                               propertyModel.S2Level.HasValue ||
                               propertyModel.H3Resolution.HasValue ||
                               propertyModel.GeoHashPrecision.HasValue;

        if (!hasSpatialConfig)
            return;

        // Check if property is GeoLocation type
        var isGeoLocation = propertyModel.PropertyType.Contains("GeoLocation");

        if (!isGeoLocation)
        {
            ReportDiagnostic(DiagnosticDescriptors.SpatialIndexOnNonGeoLocation,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName);
            return;
        }

        // Check if geospatial package is referenced
        var compilation = semanticModel.Compilation;
        var hasGeospatialPackage = compilation.ReferencedAssemblyNames
            .Any(a => a.Name.Equals("Oproto.FluentDynamoDb.Geospatial", StringComparison.OrdinalIgnoreCase));

        if (!hasGeospatialPackage)
        {
            ReportDiagnostic(DiagnosticDescriptors.MissingGeospatialPackage,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName);
            return;
        }

        // Determine the spatial index type (default to GeoHash if not specified)
        var spatialIndexType = propertyModel.SpatialIndexType ?? "GeoHash";

        // Validate S2Level is only used with S2 index type
        if (propertyModel.S2Level.HasValue && spatialIndexType != "S2")
        {
            ReportDiagnostic(DiagnosticDescriptors.S2LevelWithoutS2IndexType,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName);
        }

        // Validate H3Resolution is only used with H3 index type
        if (propertyModel.H3Resolution.HasValue && spatialIndexType != "H3")
        {
            ReportDiagnostic(DiagnosticDescriptors.H3ResolutionWithoutH3IndexType,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName);
        }

        // Validate GeoHashPrecision is only used with GeoHash index type
        if (propertyModel.GeoHashPrecision.HasValue && spatialIndexType != "GeoHash")
        {
            ReportDiagnostic(DiagnosticDescriptors.GeoHashPrecisionWithoutGeoHashIndexType,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName);
        }
    }

    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Validates that a type-based table reference uses a partial class.
    /// </summary>
    /// <param name="typeOfExpr">The typeof expression from the attribute.</param>
    /// <param name="namedType">The type symbol being referenced.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    private void ValidateTableTypeIsPartial(TypeOfExpressionSyntax typeOfExpr, INamedTypeSymbol namedType, SemanticModel semanticModel)
    {
        // Check if the type is declared as partial by examining its syntax references
        var isPartial = false;
        
        foreach (var syntaxRef in namedType.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is TypeDeclarationSyntax typeDecl)
            {
                if (typeDecl.Modifiers.Any(m => m.ValueText == "partial"))
                {
                    isPartial = true;
                    break;
                }
            }
        }

        if (!isPartial)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.NonPartialTableType,
                typeOfExpr.GetLocation(),
                namedType.Name);
        }
    }

    private static bool IsCriticalError(string diagnosticId)
    {
        // Only these errors prevent code generation
        return diagnosticId switch
        {
            "DYNDB001" => true, // Missing partition key
            "DYNDB002" => true, // Multiple partition keys
            "DYNDB010" => true, // Entity must be partial
            "FDDB051" => true, // Non-partial table type
            "FDDB120" => true, // Constant key + Computed conflict
            "FDDB121" => true, // Constant key + Prefix conflict
            "FDDB122" => true, // Extracted from constant key conflict
            "FDDB123" => true, // Empty constant key value
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

    /// <summary>
    /// Validates new index attribute configurations (DYNDB120-127).
    /// Checks for empty index names, duplicate keys, missing partition keys, and GSI/LSI type conflicts.
    /// </summary>
    /// <param name="entityModel">The entity model containing properties with index attributes to validate.</param>
    private void ValidateIndexAttributes(EntityModel entityModel)
    {
        // 1. Check for empty/whitespace index names (DYNDB124-126)
        foreach (var property in entityModel.Properties)
        {
            var location = property.PropertyDeclaration?.Identifier.GetLocation();

            foreach (var gsiPk in property.GsiPartitionKeys)
            {
                if (string.IsNullOrWhiteSpace(gsiPk.IndexName))
                {
                    ReportDiagnostic(DiagnosticDescriptors.EmptyGsiPartitionKeyIndexName, location, property.PropertyName);
                }
            }

            foreach (var gsiSk in property.GsiSortKeys)
            {
                if (string.IsNullOrWhiteSpace(gsiSk.IndexName))
                {
                    ReportDiagnostic(DiagnosticDescriptors.EmptyGsiSortKeyIndexName, location, property.PropertyName);
                }
            }

            foreach (var lsiSk in property.LsiSortKeys)
            {
                if (string.IsNullOrWhiteSpace(lsiSk.IndexName))
                {
                    ReportDiagnostic(DiagnosticDescriptors.EmptyLsiSortKeyIndexName, location, property.PropertyName);
                }
            }
        }

        // 2. Group by index name and check for conflicts
        var gsiPartitionKeys = entityModel.Properties
            .SelectMany(p => p.GsiPartitionKeys.Select(g => (Property: p, Model: g)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Model.IndexName))
            .GroupBy(x => x.Model.IndexName);

        var gsiSortKeys = entityModel.Properties
            .SelectMany(p => p.GsiSortKeys.Select(g => (Property: p, Model: g)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Model.IndexName))
            .GroupBy(x => x.Model.IndexName);

        var lsiSortKeys = entityModel.Properties
            .SelectMany(p => p.LsiSortKeys.Select(g => (Property: p, Model: g)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Model.IndexName))
            .GroupBy(x => x.Model.IndexName);

        // 3. Check duplicate GSI partition keys (DYNDB121)
        foreach (var group in gsiPartitionKeys.Where(g => g.Count() > 1))
        {
            var props = group.Select(x => x.Property.PropertyName).ToArray();
            var location = group.First().Property.PropertyDeclaration?.Identifier.GetLocation();
            ReportDiagnostic(DiagnosticDescriptors.DuplicateGsiPartitionKey, location, group.Key, entityModel.ClassName, props[0], props[1]);
        }

        // 4. Check duplicate GSI sort keys (DYNDB122)
        foreach (var group in gsiSortKeys.Where(g => g.Count() > 1))
        {
            var props = group.Select(x => x.Property.PropertyName).ToArray();
            var location = group.First().Property.PropertyDeclaration?.Identifier.GetLocation();
            ReportDiagnostic(DiagnosticDescriptors.DuplicateGsiSortKey, location, group.Key, entityModel.ClassName, props[0], props[1]);
        }

        // 5. Check duplicate LSI sort keys (DYNDB123)
        foreach (var group in lsiSortKeys.Where(g => g.Count() > 1))
        {
            var props = group.Select(x => x.Property.PropertyName).ToArray();
            var location = group.First().Property.PropertyDeclaration?.Identifier.GetLocation();
            ReportDiagnostic(DiagnosticDescriptors.DuplicateLsiSortKey, location, group.Key, entityModel.ClassName, props[0], props[1]);
        }

        // 6. Check GSI sort key without partition key (DYNDB120)
        var gsiPkIndexNames = new HashSet<string>(
            entityModel.Properties.SelectMany(p => p.GsiPartitionKeys
                .Where(g => !string.IsNullOrWhiteSpace(g.IndexName))
                .Select(g => g.IndexName)));

        foreach (var group in gsiSortKeys)
        {
            if (!gsiPkIndexNames.Contains(group.Key))
            {
                var location = group.First().Property.PropertyDeclaration?.Identifier.GetLocation();
                ReportDiagnostic(DiagnosticDescriptors.GsiSortKeyWithoutPartitionKey, location, group.Key, entityModel.ClassName);
            }
        }

        // 7. Check same index name used as both GSI and LSI (DYNDB127)
        var gsiIndexNames = new HashSet<string>(
            entityModel.Properties
                .SelectMany(p => p.GsiPartitionKeys
                    .Where(g => !string.IsNullOrWhiteSpace(g.IndexName))
                    .Select(g => g.IndexName)
                    .Concat(p.GsiSortKeys
                        .Where(g => !string.IsNullOrWhiteSpace(g.IndexName))
                        .Select(g => g.IndexName))));

        var lsiIndexNames = new HashSet<string>(
            entityModel.Properties.SelectMany(p => p.LsiSortKeys
                .Where(l => !string.IsNullOrWhiteSpace(l.IndexName))
                .Select(l => l.IndexName)));

        foreach (var overlap in gsiIndexNames.Intersect(lsiIndexNames))
        {
            var gsiProperty = entityModel.Properties.FirstOrDefault(p =>
                p.GsiPartitionKeys.Any(g => g.IndexName == overlap) ||
                p.GsiSortKeys.Any(g => g.IndexName == overlap));
            var location = gsiProperty?.PropertyDeclaration?.Identifier.GetLocation();
            ReportDiagnostic(DiagnosticDescriptors.GsiLsiIndexNameConflict, location, overlap, entityModel.ClassName);
        }
    }

    /// <summary>
    /// Validates index configurations for projection type warnings.
    /// </summary>
    /// <param name="entityModel">The entity model containing indexes to validate.</param>
    private void ValidateIndexProjectionConfiguration(EntityModel entityModel)
    {
        foreach (var index in entityModel.Indexes)
        {
            // Get the property that defines this index (for location reporting)
            var indexProperty = GetIndexProperty(entityModel, index);
            var location = indexProperty?.PropertyDeclaration?.Identifier.GetLocation();

            // FDDB070: Include projection without properties
            if (index.ProjectionType == ProjectionType.Include && 
                (index.ProjectedProperties == null || index.ProjectedProperties.Length == 0))
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.IncludeProjectionWithoutProperties,
                    location,
                    index.IndexName,
                    entityModel.ClassName);
            }

            // FDDB072: KeysOnly with UseProjection
            if (index.ProjectionType == ProjectionType.KeysOnly)
            {
                // Check if UseProjection attribute is present on the index property
                var hasUseProjection = DetectUseProjectionOnIndex(entityModel, index);
                if (hasUseProjection)
                {
                    ReportDiagnostic(
                        DiagnosticDescriptors.KeysOnlyWithUseProjection,
                        location,
                        index.IndexName,
                        entityModel.ClassName);
                }
            }
        }
    }

    /// <summary>
    /// Gets the property that defines the partition key for an index.
    /// </summary>
    private static PropertyModel? GetIndexProperty(EntityModel entityModel, IndexModel index)
    {
        // For GSIs, find the property with the partition key for this index
        if (index.IsGsi)
        {
            return entityModel.Properties.FirstOrDefault(p => 
                p.GsiPartitionKeys.Any(gsi => 
                    gsi.IndexName == index.IndexName));
        }
        
        // For LSIs, find the property with the sort key for this index
        return entityModel.Properties.FirstOrDefault(p => 
            p.LsiSortKeys.Any(lsi => lsi.IndexName == index.IndexName));
    }

    /// <summary>
    /// Detects if [UseProjection] attribute is present on the index property.
    /// </summary>
    private static bool DetectUseProjectionOnIndex(EntityModel entityModel, IndexModel index)
    {
        var indexProperty = GetIndexProperty(entityModel, index);
        if (indexProperty?.PropertyDeclaration == null)
            return false;

        // Look for UseProjection attribute in the property's attribute lists
        foreach (var attributeList in indexProperty.PropertyDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var attributeName = attribute.Name.ToString();
                if (attributeName.Contains("UseProjection"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a DynamoDB index name to a valid C# PascalCase identifier.
    /// Handles hyphens, underscores, and other special characters by removing them
    /// and capitalizing the following character.
    /// </summary>
    /// <param name="indexName">The DynamoDB index name to convert.</param>
    /// <returns>A valid C# identifier in PascalCase format.</returns>
    /// <example>
    /// "gsi1" -> "Gsi1"
    /// "status-index" -> "StatusIndex"
    /// "user_email_index" -> "UserEmailIndex"
    /// "GSI-1" -> "Gsi1"
    /// </example>
    internal static string ConvertToPascalCase(string indexName)
    {
        if (string.IsNullOrEmpty(indexName))
            return "Index";

        // Split by hyphens, underscores, and other non-alphanumeric characters
        var parts = indexName.Split(new[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return "Index";

        var result = new System.Text.StringBuilder();
        
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            // Capitalize first character, lowercase the rest for each part
            result.Append(char.ToUpperInvariant(part[0]));
            
            if (part.Length > 1)
            {
                // Check if the part is all uppercase (like "GSI") - keep it as-is but lowercase
                if (part.All(char.IsUpper))
                {
                    result.Append(part.Substring(1).ToLowerInvariant());
                }
                else
                {
                    result.Append(part.Substring(1));
                }
            }
        }

        var resultString = result.ToString();
        
        // Ensure the result starts with a letter (valid C# identifier)
        if (resultString.Length > 0 && !char.IsLetter(resultString[0]))
        {
            resultString = "Index" + resultString;
        }

        return string.IsNullOrEmpty(resultString) ? "Index" : resultString;
    }

    /// <summary>
    /// Validates that explicit ComputedAttribute.Format values don't conflict with key attribute Prefix values.
    /// For each key property with a non-empty Prefix and a ComputedKey with HasCustomFormat,
    /// checks if the format starts with "{Prefix}{Separator}" using ordinal comparison.
    /// Emits FDDB100 diagnostic if it doesn't match.
    /// </summary>
    /// <param name="entity">The entity model whose key properties will be validated for prefix/format consistency.</param>
    private void ValidatePrefixFormatConsistency(EntityModel entity)
    {
        foreach (var property in entity.Properties)
        {
            if (!property.IsPartitionKey && !property.IsSortKey)
                continue;

            if (property.KeyFormat == null || string.IsNullOrEmpty(property.KeyFormat.Prefix))
                continue;

            if (property.ComputedKey == null || !property.ComputedKey.HasCustomFormat)
                continue;

            var separator = property.KeyFormat.Separator ?? "#";
            var expectedStart = $"{property.KeyFormat.Prefix}{separator}";

            if (!property.ComputedKey.Format!.StartsWith(expectedStart, StringComparison.Ordinal))
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.PrefixFormatConflict,
                    property.PropertyDeclaration?.GetLocation(),
                    property.PropertyName,
                    property.KeyFormat.Prefix,
                    expectedStart,
                    property.ComputedKey.Format);
            }
        }
    }

    /// <summary>
    /// Computes the normalized key format for all partition key and sort key properties on the entity.
    /// For computed keys, delegates to <see cref="MapperGenerator.ComputeFormatString"/>.
    /// For non-computed keys, delegates to <see cref="ComputeNonComputedKeyFormat"/>.
    /// Stores the result in each property's <see cref="PropertyModel.NormalizedKeyFormat"/>.
    /// </summary>
    /// <param name="entity">The entity model whose key properties will have NormalizedKeyFormat populated.</param>
    private void ComputeNormalizedKeyFormats(EntityModel entity)
    {
        foreach (var property in entity.Properties)
        {
            if (!property.IsPartitionKey && !property.IsSortKey)
                continue;

            if (property.IsConstantKey)
            {
                // Constant key: the format IS the value — no placeholder substitution needed
                property.NormalizedKeyFormat = property.ConstantKeyValue;
            }
            else if (property.ComputedKey != null)
            {
                // Resolve source property models for Format injection (positional correspondence maintained)
                var sourcePropertyModels = property.ComputedKey.SourceProperties
                    .Select(name => entity.Properties.FirstOrDefault(p => p.PropertyName == name))
                    .ToArray();
                property.NormalizedKeyFormat = MapperGenerator.ComputeFormatString(property.ComputedKey, property.KeyFormat, sourcePropertyModels!);
            }
            else
            {
                property.NormalizedKeyFormat = ComputeNonComputedKeyFormat(property.KeyFormat);
            }
        }
    }

    /// <summary>
    /// Computes the normalized key format for a non-computed key property.
    /// For computed keys, defers to MapperGenerator.ComputeFormatString.
    /// </summary>
    /// <param name="keyFormat">The key format model containing prefix and separator information.</param>
    /// <returns>
    /// A format string: "{Prefix}{Separator}{0}" when prefix is present,
    /// or "{0}" when no prefix is configured.
    /// </returns>
    internal static string ComputeNonComputedKeyFormat(KeyFormatModel? keyFormat)
    {
        if (keyFormat == null || string.IsNullOrEmpty(keyFormat.Prefix))
            return "{0}";

        var separator = keyFormat.Separator ?? "#";
        return $"{keyFormat.Prefix}{separator}{{0}}";
    }

    /// <summary>
    /// Iterates over all entity properties and derives discriminator patterns for those
    /// that have a NormalizedKeyFormat populated (i.e., key properties).
    /// Stores the result in each property's <see cref="PropertyModel.DerivedDiscriminatorPattern"/>.
    /// </summary>
    /// <param name="entity">The entity model whose key properties will have DerivedDiscriminatorPattern populated.</param>
    private void DeriveDiscriminatorPatterns(EntityModel entity)
    {
        foreach (var property in entity.Properties)
        {
            if (property.NormalizedKeyFormat == null)
                continue;

            if (property.IsConstantKey)
            {
                // Constant key: the pattern IS the exact value — no wildcards
                property.DerivedDiscriminatorPattern = property.ConstantKeyValue;
            }
            else
            {
                property.DerivedDiscriminatorPattern = DeriveDiscriminatorPattern(property.NormalizedKeyFormat);
            }
        }
    }

    /// <summary>
    /// Derives a discriminator pattern from a normalized key format by replacing
    /// each {N} placeholder with *.
    /// Returns null if the resulting pattern is just "*" or starts with "*"
    /// (no useful fixed prefix for discrimination).
    /// </summary>
    /// <param name="normalizedKeyFormat">The normalized key format string (e.g., "ORDER#{0}", "{0}#{1}").</param>
    /// <returns>
    /// The discriminator pattern (e.g., "ORDER#*") when the pattern has a useful fixed prefix,
    /// or null when the pattern provides no discrimination capability.
    /// </returns>
    internal static string? DeriveDiscriminatorPattern(string normalizedKeyFormat)
    {
        // Replace all {N} and {N:format} placeholders with *
        var pattern = Regex.Replace(normalizedKeyFormat, @"\{\d+(?::[^}]*)?\}", "*");

        // A pattern of just "*" or starting with "*" provides no useful discrimination
        if (pattern == "*" || pattern.StartsWith("*"))
            return null;

        return pattern;
    }

    /// <summary>
    /// Selects the best key property for entity discrimination and populates
    /// EntityModel.Discriminator with an auto-derived DiscriminatorConfig.
    /// Priority: Sort key > Partition key. Skips if pattern is null ("*").
    /// Does not override existing explicit discriminators.
    /// </summary>
    /// <param name="entity">The entity model to populate with an auto-derived discriminator.</param>
    private void ApplyAutoDerivedDiscriminator(EntityModel entity)
    {
        // Don't override explicit discriminators
        if (entity.Discriminator != null && entity.Discriminator.IsValid)
            return;

        // Try sort key first (preferred for single-table designs)
        var skProperty = entity.SortKeyProperty;
        if (skProperty?.DerivedDiscriminatorPattern != null)
        {
            entity.Discriminator = CreateAutoDerivedDiscriminatorConfig(
                skProperty.AttributeName,
                skProperty.DerivedDiscriminatorPattern);
            return;
        }

        // Fall back to partition key
        var pkProperty = entity.PartitionKeyProperty;
        if (pkProperty?.DerivedDiscriminatorPattern != null)
        {
            entity.Discriminator = CreateAutoDerivedDiscriminatorConfig(
                pkProperty.AttributeName,
                pkProperty.DerivedDiscriminatorPattern);
        }
    }

    /// <summary>
    /// Creates a DiscriminatorConfig for an auto-derived discriminator pattern.
    /// </summary>
    /// <param name="attributeName">The DynamoDB attribute name of the key property (e.g., "sk", "pk").</param>
    /// <param name="pattern">The derived discriminator pattern (e.g., "ORDER#*", or "PROFILE" for constant keys).</param>
    /// <returns>A new DiscriminatorConfig with IsAutoDerived set to true.</returns>
    private static DiscriminatorConfig CreateAutoDerivedDiscriminatorConfig(
        string attributeName, string pattern)
    {
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);

        return new DiscriminatorConfig
        {
            PropertyName = attributeName,
            Pattern = strategy == DiscriminatorStrategy.ExactMatch ? null : pattern,
            ExactValue = strategy == DiscriminatorStrategy.ExactMatch ? pattern : null,
            Strategy = strategy,
            IsAutoDerived = true
        };
    }

    /// <summary>
    /// Populates GsiDiscriminator on IndexModel from the GSI partition key property's
    /// derived discriminator pattern, when no explicit GSI discriminator is configured.
    /// </summary>
    /// <param name="entity">The entity model whose GSI indexes will have GsiDiscriminator populated.</param>
    private void ApplyAutoDerivedGsiDiscriminator(EntityModel entity)
    {
        foreach (var index in entity.Indexes.Where(i => i.IsGsi && i.GsiDiscriminator == null))
        {
            var gsiPkProperty = entity.Properties
                .FirstOrDefault(p => p.GsiPartitionKeys.Any(g => g.IndexName == index.IndexName));

            if (gsiPkProperty?.DerivedDiscriminatorPattern != null)
            {
                index.GsiDiscriminator = new DiscriminatorConfig
                {
                    PropertyName = gsiPkProperty.AttributeName,
                    Pattern = gsiPkProperty.DerivedDiscriminatorPattern,
                    Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(
                        gsiPkProperty.DerivedDiscriminatorPattern),
                    IsAutoDerived = true
                };
            }
        }
    }

    /// <summary>
    /// Validates that an explicit DiscriminatorPattern on DynamoDbTableAttribute
    /// matches the auto-derived pattern for the referenced key property.
    /// Emits FDDB101 if they differ and the derived pattern is not null.
    /// </summary>
    /// <param name="entity">The entity model to validate.</param>
    private void ValidateExplicitVsDerivedDiscriminator(EntityModel entity)
    {
        if (entity.Discriminator == null || entity.Discriminator.IsAutoDerived)
            return;

        var explicitProperty = entity.Discriminator.PropertyName;
        var explicitPattern = entity.Discriminator.Pattern;

        if (string.IsNullOrEmpty(explicitPattern))
            return; // ExactValue discriminators don't conflict

        // Find the key property matching the discriminator property name
        var matchingKey = entity.Properties.FirstOrDefault(p =>
            (p.IsPartitionKey || p.IsSortKey) &&
            string.Equals(p.AttributeName, explicitProperty, StringComparison.Ordinal));

        if (matchingKey?.DerivedDiscriminatorPattern == null)
            return; // Derived is "*" — explicit supplements rather than contradicts

        if (!string.Equals(explicitPattern, matchingKey.DerivedDiscriminatorPattern, StringComparison.Ordinal))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.DiscriminatorKeyFormatConflict,
                entity.TypeDeclaration?.GetLocation(),
                entity.ClassName,
                explicitProperty,
                explicitPattern,
                matchingKey.DerivedDiscriminatorPattern);
        }
    }

    /// <summary>
    /// Detects when an explicit DiscriminatorPattern is redundant because it exactly
    /// matches the auto-derived pattern from the referenced key property.
    /// Emits FDDB103 Info diagnostic.
    /// </summary>
    /// <param name="entity">The entity model to check for redundant discriminator.</param>
    private void DetectRedundantExplicitDiscriminator(EntityModel entity)
    {
        if (entity.Discriminator == null || entity.Discriminator.IsAutoDerived)
            return;
        if (entity.Discriminator.Strategy == DiscriminatorStrategy.ExactMatch)
            return; // DiscriminatorValue doesn't get redundancy check

        var explicitProperty = entity.Discriminator.PropertyName;
        var explicitPattern = entity.Discriminator.Pattern;

        var matchingKey = entity.Properties.FirstOrDefault(p =>
            (p.IsPartitionKey || p.IsSortKey) &&
            string.Equals(p.AttributeName, explicitProperty, StringComparison.Ordinal));

        if (matchingKey?.DerivedDiscriminatorPattern != null &&
            string.Equals(explicitPattern, matchingKey.DerivedDiscriminatorPattern, StringComparison.Ordinal))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.RedundantExplicitDiscriminator,
                entity.TypeDeclaration?.GetLocation(),
                entity.ClassName,
                explicitPattern!);
        }
    }
}