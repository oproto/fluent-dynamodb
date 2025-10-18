using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator;

/// <summary>
/// Source generator for DynamoDB entity mapping code, field constants, and key builders.
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
                transform: static (ctx, _) => GetEntityModel(ctx))
            .Where(static m => m.Model is not null);

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
                       attributeName.Contains("DynamoDbTableAttribute");
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
        catch (Exception)
        {
            // If there's an exception during analysis, return null to skip this entity
            return (null, Array.Empty<Diagnostic>());
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

            // Generate Fields class with field name constants
            var fieldsCode = FieldsGenerator.GenerateFieldsClass(entity);
            context.AddSource($"{entity.ClassName}Fields.g.cs", fieldsCode);

            // Generate Keys class with key builder methods
            var keysCode = KeysGenerator.GenerateKeysClass(entity);
            context.AddSource($"{entity.ClassName}Keys.g.cs", keysCode);

            // Generate entity implementation with mapping methods
            var sourceCode = MapperGenerator.GenerateEntityImplementation(entity);
            context.AddSource($"{entity.ClassName}.g.cs", sourceCode);
        }
    }


}