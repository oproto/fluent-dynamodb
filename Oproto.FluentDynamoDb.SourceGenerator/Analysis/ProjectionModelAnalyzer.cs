using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes projection model classes and validates their configuration.
/// </summary>
internal class ProjectionModelAnalyzer
{
    private readonly List<Diagnostic> _diagnostics = new();
    
    /// <summary>
    /// Gets the diagnostics collected during analysis.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    
    /// <summary>
    /// Analyzes a projection model class and extracts metadata.
    /// </summary>
    /// <param name="classDecl">The class declaration to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="entityModels">Available entity models for validation.</param>
    /// <returns>The extracted projection model, or null if analysis failed.</returns>
    public ProjectionModel? AnalyzeProjection(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        IReadOnlyList<EntityModel> entityModels)
    {
        _diagnostics.Clear();
        
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
            return null;
        
        // Check if class is partial
        if (!IsPartialClass(classDecl))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.ProjectionMustBePartial,
                classDecl.Identifier.GetLocation(),
                classSymbol.Name);
            return null;
        }
        
        // Get the DynamoDbProjection attribute
        var projectionAttribute = GetProjectionAttribute(classDecl, semanticModel);
        if (projectionAttribute == null)
            return null;
        
        // Extract source entity type
        var sourceEntityType = ExtractSourceEntityType(projectionAttribute, semanticModel);
        if (string.IsNullOrEmpty(sourceEntityType))
        {
            ReportDiagnostic(
                DiagnosticDescriptors.InvalidProjectionSourceEntity,
                classDecl.Identifier.GetLocation(),
                "Unknown", classSymbol.Name);
            return null;
        }
        
        // Find the source entity model
        var sourceEntity = entityModels.FirstOrDefault(e => 
            e.ClassName == sourceEntityType || 
            $"{e.Namespace}.{e.ClassName}" == sourceEntityType);
        
        if (sourceEntity == null)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.InvalidProjectionSourceEntity,
                classDecl.Identifier.GetLocation(),
                sourceEntityType, classSymbol.Name);
            return null;
        }
        
        var projectionModel = new ProjectionModel
        {
            ClassName = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            SourceEntityType = sourceEntityType,
            ClassDeclaration = classDecl,
            Discriminator = sourceEntity.Discriminator,
            // Keep legacy properties for backward compatibility
            DiscriminatorProperty = sourceEntity.EntityDiscriminator != null ? "EntityType" : null,
            DiscriminatorValue = sourceEntity.EntityDiscriminator
        };
        
        // Extract projection properties
        ExtractProjectionProperties(classDecl, semanticModel, projectionModel, sourceEntity);
        
        // Validate projection properties
        ValidateProjectionProperties(projectionModel, sourceEntity);
        
        // Validate property types
        ValidatePropertyTypes(projectionModel, sourceEntity);
        
        // Check for suboptimal configurations (warnings)
        CheckForSuboptimalConfigurations(projectionModel, sourceEntity);
        
        // Only return null if there are critical errors
        var criticalErrors = _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        return criticalErrors.Length > 0 ? null : projectionModel;
    }
    
    private bool IsPartialClass(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Modifiers.Any(m => m.ValueText == "partial");
    }
    
    private AttributeSyntax? GetProjectionAttribute(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
    {
        return classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var attributeName = attr.Name.ToString();
                return attributeName.Contains("DynamoDbProjection");
            });
    }
    
    private string? ExtractSourceEntityType(AttributeSyntax attribute, SemanticModel semanticModel)
    {
        // Get the first argument which should be typeof(SourceEntity)
        var firstArg = attribute.ArgumentList?.Arguments.FirstOrDefault();
        if (firstArg?.Expression is TypeOfExpressionSyntax typeOfExpr)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            if (typeInfo.Type != null)
            {
                return typeInfo.Type.ToDisplayString();
            }
        }
        
        return null;
    }
    
    private void ExtractProjectionProperties(
        ClassDeclarationSyntax classDecl,
        SemanticModel semanticModel,
        ProjectionModel projectionModel,
        EntityModel sourceEntity)
    {
        var properties = new List<ProjectionPropertyModel>();
        
        foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertySymbol = semanticModel.GetDeclaredSymbol(member) as IPropertySymbol;
            if (propertySymbol == null)
                continue;
            
            // Find corresponding property in source entity
            var sourceProperty = sourceEntity.Properties.FirstOrDefault(p => 
                p.PropertyName == propertySymbol.Name);
            
            var projectionProperty = new ProjectionPropertyModel
            {
                PropertyName = propertySymbol.Name,
                PropertyType = propertySymbol.Type.ToDisplayString(),
                IsNullable = propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated,
                SourceProperty = sourceProperty,
                AttributeName = sourceProperty?.AttributeName ?? string.Empty
            };
            
            properties.Add(projectionProperty);
        }
        
        projectionModel.Properties = properties.ToArray();
    }
    
    /// <summary>
    /// Validates that all projection properties exist on the source entity.
    /// </summary>
    private void ValidateProjectionProperties(ProjectionModel projection, EntityModel sourceEntity)
    {
        foreach (var property in projection.Properties)
        {
            if (property.SourceProperty == null)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.ProjectionPropertyNotFound,
                    projection.ClassDeclaration?.Identifier.GetLocation(),
                    property.PropertyName,
                    projection.ClassName,
                    sourceEntity.ClassName);
            }
        }
    }
    
    /// <summary>
    /// Validates that property types match between projection and source.
    /// </summary>
    private void ValidatePropertyTypes(ProjectionModel projection, EntityModel sourceEntity)
    {
        foreach (var property in projection.Properties)
        {
            if (property.SourceProperty == null)
                continue;
            
            // Normalize type names for comparison (handle nullable, fully qualified names, etc.)
            var projectionType = NormalizeTypeName(property.PropertyType);
            var sourceType = NormalizeTypeName(property.SourceProperty.PropertyType);
            
            if (projectionType != sourceType)
            {
                ReportDiagnostic(
                    DiagnosticDescriptors.ProjectionPropertyTypeMismatch,
                    projection.ClassDeclaration?.Identifier.GetLocation(),
                    property.PropertyName,
                    property.PropertyType,
                    projection.ClassName,
                    property.SourceProperty.PropertyType);
            }
        }
    }
    
    /// <summary>
    /// Checks for suboptimal projection configurations and emits warnings.
    /// </summary>
    private void CheckForSuboptimalConfigurations(ProjectionModel projection, EntityModel sourceEntity)
    {
        // PROJ101: Warn if projection includes all properties from source entity
        var sourcePropertyCount = sourceEntity.Properties.Length;
        var projectionPropertyCount = projection.Properties.Length;
        
        if (projectionPropertyCount >= sourcePropertyCount && sourcePropertyCount > 0)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.ProjectionIncludesAllProperties,
                projection.ClassDeclaration?.Identifier.GetLocation(),
                projection.ClassName,
                sourceEntity.ClassName);
        }
        
        // PROJ102: Warn if projection has many properties (threshold: 20)
        const int manyPropertiesThreshold = 20;
        if (projectionPropertyCount > manyPropertiesThreshold)
        {
            ReportDiagnostic(
                DiagnosticDescriptors.ProjectionHasManyProperties,
                projection.ClassDeclaration?.Identifier.GetLocation(),
                projection.ClassName,
                projectionPropertyCount);
        }
    }
    
    private string NormalizeTypeName(string typeName)
    {
        // Remove nullable annotations
        var normalized = typeName.TrimEnd('?');
        
        // Remove System. prefix for common types
        normalized = normalized
            .Replace("System.String", "string")
            .Replace("System.Int32", "int")
            .Replace("System.Int64", "long")
            .Replace("System.Double", "double")
            .Replace("System.Single", "float")
            .Replace("System.Decimal", "decimal")
            .Replace("System.Boolean", "bool")
            .Replace("System.DateTime", "DateTime")
            .Replace("System.DateTimeOffset", "DateTimeOffset")
            .Replace("System.Guid", "Guid")
            .Replace("System.Byte[]", "byte[]");
        
        return normalized;
    }
    
    private void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object[] messageArgs)
    {
        var diagnostic = Diagnostic.Create(descriptor, location ?? Location.None, messageArgs);
        _diagnostics.Add(diagnostic);
    }
}
