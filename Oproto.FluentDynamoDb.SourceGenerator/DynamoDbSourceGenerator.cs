using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
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

            // Generate optimized entity implementation with mapping methods, Keys, and Fields
            var sourceCode = GenerateOptimizedEntityImplementation(entity);
            context.AddSource($"{entity.ClassName}.g.cs", sourceCode);

            // Generate security metadata if entity has sensitive fields
            var securityMetadata = SecurityMetadataGenerator.GenerateSecurityMetadata(entity);
            if (!string.IsNullOrEmpty(securityMetadata))
            {
                context.AddSource($"{entity.ClassName}SecurityMetadata.g.cs", securityMetadata);
            }

            // Generate stream conversion methods if requested
            if (entity.GenerateStreamConversion)
            {
                var streamCode = StreamMapperGenerator.GenerateStreamConversion(entity);
                if (!string.IsNullOrEmpty(streamCode))
                {
                    context.AddSource($"{entity.ClassName}StreamMapper.g.cs", streamCode);
                }
            }

            // Generate UpdateExpressions and UpdateModel classes for type-safe update operations
            var updateExpressionsCode = UpdateExpressionsGenerator.GenerateUpdateExpressionsClass(entity);
            context.AddSource($"{entity.ClassName}UpdateExpressions.g.cs", updateExpressionsCode);

            var updateModelCode = UpdateExpressionsGenerator.GenerateUpdateModelClass(entity);
            context.AddSource($"{entity.ClassName}UpdateModel.g.cs", updateModelCode);
        }

        // Group entities by table name for table class generation
        var entitiesByTable = GroupEntitiesByTableName(validEntityModels);

        // Validate default entity configuration for each table
        foreach (var tableGroup in entitiesByTable)
        {
            ValidateDefaultEntity(tableGroup.Value, context);
        }

        // Generate table classes for each table group
        foreach (var tableGroup in entitiesByTable)
        {
            var tableName = tableGroup.Key;
            var tableEntities = tableGroup.Value;
            
            var tableCode = TableGenerator.GenerateTableClass(tableName, tableEntities);
            if (!string.IsNullOrEmpty(tableCode))
            {
                // Use table name for the file name
                var tableClassName = GetTableClassName(tableName);
                context.AddSource($"{tableClassName}.g.cs", tableCode);
            }
            
            // Generate OnStream method and registry if any entities have stream conversion enabled
            var streamCode = StreamRegistryGenerator.GenerateOnStreamMethod(
                tableName,
                tableEntities,
                GetTableClassName(tableName),
                tableEntities[0].Namespace);
            
            if (!string.IsNullOrEmpty(streamCode))
            {
                var tableClassName = GetTableClassName(tableName);
                context.AddSource($"{tableClassName}StreamProcessor.g.cs", streamCode);
                
                // Validate consistent discriminator properties
                if (!StreamRegistryGenerator.ValidateConsistentDiscriminatorProperty(tableEntities))
                {
                    var distinctProperties = StreamRegistryGenerator.GetDistinctDiscriminatorProperties(tableEntities);
                    var location = tableEntities[0].ClassDeclaration?.Identifier.GetLocation() ?? Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InconsistentDiscriminatorProperties,
                        location,
                        tableName,
                        string.Join(", ", distinctProperties)));
                }
            }
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
    /// Groups entities by their table name.
    /// Entities with the same TableName are grouped together for consolidated table generation.
    /// Nested entities (with TableName starting with "_entity_") are excluded.
    /// </summary>
    /// <param name="entities">List of all entity models.</param>
    /// <returns>Dictionary mapping table names to lists of entities.</returns>
    private static Dictionary<string, List<EntityModel>> GroupEntitiesByTableName(List<EntityModel> entities)
    {
        return entities
            .Where(e => !string.IsNullOrEmpty(e.TableName) && !e.TableName.StartsWith("_entity_"))
            .GroupBy(e => e.TableName)
            .ToDictionary(g => g.Key!, g => g.ToList());
    }

    /// <summary>
    /// Validates default entity configuration for a table.
    /// Ensures that:
    /// - Single-entity tables work without explicit IsDefault
    /// - Multi-entity tables have exactly one default entity
    /// </summary>
    /// <param name="entities">List of entities in the table.</param>
    /// <param name="context">Source production context for reporting diagnostics.</param>
    private static void ValidateDefaultEntity(List<EntityModel> entities, SourceProductionContext context)
    {
        if (entities.Count == 0)
            return;

        var tableName = entities[0].TableName;
        var defaultEntities = entities.Where(e => e.IsDefault).ToList();

        // Single entity table - no validation needed, it's implicitly the default
        if (entities.Count == 1)
            return;

        // Multiple entities - must have exactly one default
        if (defaultEntities.Count == 0)
        {
            // Error: Multiple entities but no default specified
            var location = entities[0].ClassDeclaration?.Identifier.GetLocation() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.NoDefaultEntitySpecified,
                location,
                tableName));
        }
        else if (defaultEntities.Count > 1)
        {
            // Error: Multiple defaults specified
            // Report on the second default entity (first one is valid)
            var location = defaultEntities[1].ClassDeclaration?.Identifier.GetLocation() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleDefaultEntities,
                location,
                tableName));
        }
    }

    /// <summary>
    /// Gets the table class name from a table name.
    /// Converts table name to PascalCase and appends "Table".
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <returns>The generated table class name.</returns>
    private static string GetTableClassName(string tableName)
    {
        // Split by hyphens and underscores, capitalize each part
        var parts = tableName.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var cleanName = string.Join("", parts.Select(part => 
        {
            if (string.IsNullOrEmpty(part))
                return part;
            return char.ToUpperInvariant(part[0]) + part.Substring(1);
        }));
        
        return $"{cleanName}Table";
    }

}