using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

/// <summary>
/// Utility class for verifying that generated source code compiles successfully.
/// Provides detailed error reporting with line numbers and source context.
/// </summary>
public static class CompilationVerifier
{
    /// <summary>
    /// Asserts that the provided source code compiles without errors.
    /// </summary>
    /// <param name="sourceCode">The C# source code to compile</param>
    /// <param name="additionalSources">Optional additional source files needed for compilation</param>
    /// <exception cref="CompilationFailedException">Thrown when the source code fails to compile</exception>
    public static void AssertGeneratedCodeCompiles(string sourceCode, params string[] additionalSources)
    {
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(sourceCode)
        };

        // Add any additional source files
        foreach (var additionalSource in additionalSources)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(additionalSource));
        }

        // Add mock attributes and interfaces for testing
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(GetMockAttributes()));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(GetMockInterfaces()));

        var compilation = CSharpCompilation.Create(
            "TestCompilation",
            syntaxTrees,
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new System.IO.MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            throw new CompilationFailedException(
                FormatCompilationErrors(errors, sourceCode, additionalSources));
        }
    }

    /// <summary>
    /// Gets mock attribute definitions for testing purposes.
    /// These are attributes that are planned but not yet implemented.
    /// </summary>
    private static string GetMockAttributes()
    {
        return @"
namespace Oproto.FluentDynamoDb.Attributes
{
    /// <summary>
    /// Mock attribute for DynamoDbEntity - marks a class as a DynamoDB entity type.
    /// This is a placeholder for testing until the actual attribute is implemented.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class DynamoDbEntityAttribute : System.Attribute { }
}
";
    }

    /// <summary>
    /// Gets mock interface and class definitions for testing purposes.
    /// These provide the necessary types that generated code depends on.
    /// </summary>
    private static string GetMockInterfaces()
    {
        // No mocks needed - all types are provided by the referenced assemblies
        return string.Empty;
    }

    /// <summary>
    /// Gets the standard set of metadata references needed for compilation.
    /// </summary>
    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET references
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IO.Stream).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.CancellationToken).Assembly.Location),
            
            // AWS SDK references
            MetadataReference.CreateFromFile(typeof(Amazon.DynamoDBv2.Model.AttributeValue).Assembly.Location),
            
            // FluentDynamoDb library references
            MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity).Assembly.Location)
        };

        // Add runtime assemblies
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        references.AddRange(new[]
        {
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "netstandard.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimePath, "System.Linq.Expressions.dll"))
        });

        return references;
    }

    /// <summary>
    /// Formats compilation errors with detailed context including line numbers and source code.
    /// </summary>
    private static string FormatCompilationErrors(
        List<Diagnostic> errors,
        string primarySource,
        string[] additionalSources)
    {
        var errorMessage = new StringBuilder();
        errorMessage.AppendLine("Generated code failed to compile:");
        errorMessage.AppendLine();

        foreach (var error in errors)
        {
            var lineSpan = error.Location.GetLineSpan();
            var lineNumber = lineSpan.StartLinePosition.Line + 1;
            var column = lineSpan.StartLinePosition.Character + 1;

            errorMessage.AppendLine($"Error {error.Id}: {error.GetMessage()}");
            errorMessage.AppendLine($"  at line {lineNumber}, column {column}");
            errorMessage.AppendLine();

            // Add source context
            var sourceText = GetSourceForDiagnostic(error, primarySource, additionalSources);
            if (sourceText != null)
            {
                var lines = sourceText.Split('\n');
                var errorLine = lineSpan.StartLinePosition.Line;

                // Show 2 lines before and after the error
                var startLine = Math.Max(0, errorLine - 2);
                var endLine = Math.Min(lines.Length - 1, errorLine + 2);

                errorMessage.AppendLine("  Source context:");
                for (int i = startLine; i <= endLine; i++)
                {
                    var marker = i == errorLine ? ">>>" : "   ";
                    errorMessage.AppendLine($"  {marker} {i + 1,4}: {lines[i].TrimEnd()}");
                }
                errorMessage.AppendLine();
            }
        }

        // Include full generated source for debugging
        errorMessage.AppendLine("Full generated source:");
        errorMessage.AppendLine("=".PadRight(80, '='));
        errorMessage.AppendLine(primarySource);
        errorMessage.AppendLine("=".PadRight(80, '='));

        if (additionalSources.Length > 0)
        {
            errorMessage.AppendLine();
            errorMessage.AppendLine("Additional sources:");
            for (int i = 0; i < additionalSources.Length; i++)
            {
                errorMessage.AppendLine($"--- Source {i + 1} ---");
                errorMessage.AppendLine(additionalSources[i]);
                errorMessage.AppendLine();
            }
        }

        return errorMessage.ToString();
    }

    /// <summary>
    /// Gets the source text for a diagnostic, handling multiple source files.
    /// </summary>
    private static string? GetSourceForDiagnostic(
        Diagnostic diagnostic,
        string primarySource,
        string[] additionalSources)
    {
        var tree = diagnostic.Location.SourceTree;
        if (tree == null)
            return null;

        // Try to match the source tree to one of our sources
        var treeText = tree.GetText().ToString();
        
        if (treeText == primarySource)
            return primarySource;

        foreach (var additionalSource in additionalSources)
        {
            if (treeText == additionalSource)
                return additionalSource;
        }

        return treeText;
    }
}

/// <summary>
/// Exception thrown when generated code fails to compile.
/// </summary>
public class CompilationFailedException : Exception
{
    public CompilationFailedException(string message) : base(message)
    {
    }
}
