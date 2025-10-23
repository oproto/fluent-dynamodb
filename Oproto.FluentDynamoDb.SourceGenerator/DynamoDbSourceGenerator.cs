using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator;

/// <summary>
/// High-performance DynamoDB source generator with advanced optimizations and features.
/// Uses incremental generation, caching, and performance optimizations for fast builds.
/// </summary>
[Generator]
public class DynamoDbSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register syntax receiver for classes with DynamoDbTable attribute
        var entityClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDynamoDbEntity(s),
                transform: static (ctx, _) => GetEntityModel(ctx));
        // Note: We don't filter out null models here because we still need to report diagnostics

        // Register syntax receiver for classes with DynamoDbProjection attribute
        var projectionClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDynamoDbProjection(s),
                transform: static (ctx, _) => ctx);

        // Combine entity and projection classes for processing
        var combined = entityClasses.Collect()
            .Combine(projectionClasses.Collect());

        // Register code generation
        context.RegisterSourceOutput(combined, Execute);
    }

    private static bool IsDynamoDbEntity(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        return classDecl.AttributeLists.Any(al =>
            al.Attributes.Any(a =>
            {
                var attributeName = a.Name.ToString();
                return attributeName.Contains("DynamoDbTable") ||
                       attributeName.Contains("DynamoDbTableAttribute") ||
                       attributeName.Contains("DynamoDbEntity") ||
                       attributeName.Contains("DynamoDbEntityAttribute");
            }));
    }

    private static bool IsDynamoDbProjection(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
            return false;

        return classDecl.AttributeLists.Any(al =>
            al.Attributes.Any(a =>
            {
                var attributeName = a.Name.ToString();
                return attributeName.Contains("DynamoDbProjection") ||
                       attributeName.Contains("DynamoDbProjectionAttribute");
            }));
    }

    private static (EntityModel? Model, IReadOnlyList<Diagnostic> Diagnostics) GetEntityModel(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl)
            return (null, Array.Empty<Diagnostic>());

        try
        {
            var analyzer = new EntityAnalyzer();
            var entityModel = analyzer.AnalyzeEntity(classDecl, context.SemanticModel);

            return (entityModel, analyzer.Diagnostics);
        }
        catch (Exception ex)
        {
            // Create a diagnostic for the exception to help with debugging
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DYNDB999",
                    "Source generator error",
                    "Source generator failed to analyze entity '{0}': {1}",
                    "DynamoDb",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                classDecl.Identifier.GetLocation(),
                classDecl.Identifier.ValueText,
                ex.Message);

            return (null, new[] { diagnostic });
        }
    }

    private static void Execute(
        SourceProductionContext context,
        (ImmutableArray<(EntityModel? Model, IReadOnlyList<Diagnostic> Diagnostics)> Entities,
         ImmutableArray<GeneratorSyntaxContext> ProjectionContexts) input)
    {
        var (entities, projectionContexts) = input;

        // First, process all entities and collect valid entity models
        var validEntityModels = new List<EntityModel>();

        foreach (var (entity, diagnostics) in entities)
        {
            // Report diagnostics
            foreach (var diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            if (entity == null) continue;

            validEntityModels.Add(entity);

            // Check if this is a nested entity (DynamoDbEntity) vs a table entity (DynamoDbTable)
            var isNestedEntity = entity.TableName?.StartsWith("_entity_") == true;

            // Generate Fields class with field name constants (for all entities)
            var fieldsCode = FieldsGenerator.GenerateFieldsClass(entity);
            context.AddSource($"{entity.ClassName}Fields.g.cs", fieldsCode);

            // Generate Keys class only for table entities (not nested entities)
            if (!isNestedEntity)
            {
                var keysCode = KeysGenerator.GenerateKeysClass(entity);
                context.AddSource($"{entity.ClassName}Keys.g.cs", keysCode);
            }

            // Generate optimized entity implementation with mapping methods
            var sourceCode = GenerateOptimizedEntityImplementation(entity);
            context.AddSource($"{entity.ClassName}.g.cs", sourceCode);
        }

        // Now process projection models and collect them
        var validProjectionModels = new List<ProjectionModel>();
        
        foreach (var projectionContext in projectionContexts)
        {
            if (projectionContext.Node is not ClassDeclarationSyntax classDecl)
                continue;

            try
            {
                var analyzer = new ProjectionModelAnalyzer();
                var projectionModel = analyzer.AnalyzeProjection(
                    classDecl,
                    projectionContext.SemanticModel,
                    validEntityModels);

                // Report diagnostics
                foreach (var diagnostic in analyzer.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }

                if (projectionModel == null)
                    continue;

                // Generate projection expression
                projectionModel.ProjectionExpression = ProjectionExpressionGenerator.GenerateProjectionExpression(projectionModel);

                // Generate projection metadata class
                var metadataCode = ProjectionExpressionGenerator.GenerateProjectionMetadata(projectionModel);
                context.AddSource($"{projectionModel.ClassName}Metadata.g.cs", metadataCode);
                
                // Generate FromDynamoDb method for projection model
                var fromDynamoDbCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projectionModel);
                context.AddSource($"{projectionModel.ClassName}.g.cs", fromDynamoDbCode);
                
                validProjectionModels.Add(projectionModel);
            }
            catch (Exception ex)
            {
                // Create a diagnostic for the exception to help with debugging
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DYNDB999",
                        "Source generator error",
                        "Source generator failed to analyze projection '{0}': {1}",
                        "DynamoDb",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    classDecl.Identifier.GetLocation(),
                    classDecl.Identifier.ValueText,
                    ex.Message);

                context.ReportDiagnostic(diagnostic);
            }
        }
        
        // Generate table index properties for entities grouped by table
        GenerateTableIndexProperties(context, validEntityModels, validProjectionModels);
    }

    /// <summary>
    /// Generates optimized entity implementation using MapperGenerator.
    /// </summary>
    private static string GenerateOptimizedEntityImplementation(EntityModel entity)
    {
        // Use MapperGenerator as the single source of truth for entity implementation
        return MapperGenerator.GenerateEntityImplementation(entity);
    }
    
    /// <summary>
    /// Generates table index properties for entities grouped by table name.
    /// </summary>
    private static void GenerateTableIndexProperties(
        SourceProductionContext context,
        List<EntityModel> entities,
        List<ProjectionModel> projectionModels)
    {
        // Group entities by table name
        var entitiesByTable = entities
            .Where(e => !string.IsNullOrEmpty(e.TableName) && !e.TableName.StartsWith("_entity_"))
            .GroupBy(e => e.TableName)
            .ToList();
        
        foreach (var tableGroup in entitiesByTable)
        {
            var tableName = tableGroup.Key;
            var tableEntities = tableGroup.ToList();
            
            // Check if any entity in this table has GSI definitions
            var hasGsiDefinitions = tableEntities.Any(e => e.Indexes.Length > 0);
            if (!hasGsiDefinitions)
                continue;
            
            // Generate index properties for this table
            var indexPropertiesCode = TableIndexGenerator.GenerateIndexProperties(
                tableName,
                tableEntities,
                projectionModels);
            
            if (!string.IsNullOrEmpty(indexPropertiesCode))
            {
                // Use the table name to create a unique file name
                var tableClassName = tableName.Replace("-", "").Replace("_", "");
                context.AddSource($"{tableClassName}Table.Indexes.g.cs", indexPropertiesCode);
            }
        }
    }
}