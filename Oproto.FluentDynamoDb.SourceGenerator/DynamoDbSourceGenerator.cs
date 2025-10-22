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

        // Register code generation
        context.RegisterSourceOutput(entityClasses.Collect(), Execute);
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

    private static void Execute(SourceProductionContext context, ImmutableArray<(EntityModel? Model, IReadOnlyList<Diagnostic> Diagnostics)> entities)
    {
        foreach (var (entity, diagnostics) in entities)
        {
            // Report diagnostics
            foreach (var diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }

            if (entity == null) continue;

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

            // Generate JsonSerializerContext for System.Text.Json if needed
            var jsonContextCode = JsonSerializerContextGenerator.GenerateJsonSerializerContext(entity);
            if (jsonContextCode != null)
            {
                context.AddSource($"{entity.ClassName}JsonContext.g.cs", jsonContextCode);
            }

            // Generate optimized entity implementation with mapping methods
            var sourceCode = GenerateOptimizedEntityImplementation(entity);
            context.AddSource($"{entity.ClassName}.g.cs", sourceCode);
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
}