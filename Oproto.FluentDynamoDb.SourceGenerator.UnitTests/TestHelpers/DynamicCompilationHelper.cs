using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

/// <summary>
/// Helper class for source generator test compilation verification.
/// Contains reflection-based code that is unavoidable for dynamic assembly testing.
/// 
/// This class isolates all reflection usage required for testing source generators,
/// which need to dynamically load and verify generated code at runtime.
/// </summary>
/// <remarks>
/// IL3000: Assembly.Location returns empty string in single-file apps.
/// This is acceptable in test code as tests are not published as single-file apps.
/// 
/// IL2026: RequiresUnreferencedCode - Dynamic assembly loading requires reflection.
/// This is unavoidable for source generator testing where we need to verify generated code.
/// 
/// IL2060/IL2070/IL2072/IL2075: Reflection-based member access warnings.
/// These are unavoidable when testing dynamically generated types.
/// </remarks>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator tests require dynamic assembly loading for verification")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode",
    Justification = "Source generator tests require dynamic type instantiation")]
[SuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file",
    Justification = "Test code is not published as single-file; Assembly.Location is valid in test context")]
public static class DynamicCompilationHelper
{
    /// <summary>
    /// Gets the base directory for resolving runtime assemblies.
    /// Prefers AppContext.BaseDirectory but falls back to Assembly.Location for test scenarios.
    /// </summary>
    public static string GetRuntimeDirectory()
    {
        // First try AppContext.BaseDirectory (works in single-file scenarios)
        var baseDir = AppContext.BaseDirectory;
        var runtimeDll = Path.Combine(baseDir, "System.Runtime.dll");
        
        if (File.Exists(runtimeDll))
        {
            return baseDir;
        }
        
        // Fall back to Assembly.Location (works in test scenarios)
        // This is suppressed via class-level attribute as it's unavoidable for tests
#pragma warning disable IL3000
        var assemblyLocation = typeof(object).Assembly.Location;
#pragma warning restore IL3000
        
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            return Path.GetDirectoryName(assemblyLocation)!;
        }
        
        throw new InvalidOperationException(
            "Unable to determine runtime directory. Neither AppContext.BaseDirectory nor Assembly.Location provided valid paths.");
    }

    /// <summary>
    /// Creates a metadata reference from a type's assembly.
    /// </summary>
    /// <param name="type">The type whose assembly should be referenced.</param>
    /// <returns>A metadata reference for the assembly.</returns>
#pragma warning disable IL3000
    public static MetadataReference CreateReferenceFromType(Type type)
    {
        return MetadataReference.CreateFromFile(type.Assembly.Location);
    }
#pragma warning restore IL3000

    /// <summary>
    /// Creates a metadata reference from a file path relative to the runtime directory.
    /// </summary>
    /// <param name="fileName">The file name (e.g., "System.Runtime.dll").</param>
    /// <returns>A metadata reference for the assembly.</returns>
    public static MetadataReference CreateReferenceFromRuntime(string fileName)
    {
        var runtimePath = GetRuntimeDirectory();
        return MetadataReference.CreateFromFile(Path.Combine(runtimePath, fileName));
    }

    /// <summary>
    /// Gets the standard set of metadata references needed for compilation testing.
    /// </summary>
    /// <returns>A collection of metadata references for common .NET types.</returns>
    public static IEnumerable<MetadataReference> GetStandardReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET references
            CreateReferenceFromType(typeof(object)),
            CreateReferenceFromType(typeof(System.Collections.Generic.List<>)),
            CreateReferenceFromType(typeof(System.Attribute)),
            CreateReferenceFromType(typeof(System.Linq.Enumerable)),
            CreateReferenceFromType(typeof(System.IO.Stream)),
            CreateReferenceFromType(typeof(System.Threading.Tasks.Task)),
            CreateReferenceFromType(typeof(System.Threading.CancellationToken)),
        };

        // Add runtime assemblies
        references.Add(CreateReferenceFromRuntime("System.Runtime.dll"));
        references.Add(CreateReferenceFromRuntime("netstandard.dll"));
        references.Add(CreateReferenceFromRuntime("System.Collections.dll"));
        references.Add(CreateReferenceFromRuntime("System.Linq.Expressions.dll"));

        return references;
    }

    /// <summary>
    /// Gets metadata references including FluentDynamoDb library types.
    /// </summary>
    /// <returns>A collection of metadata references including library types.</returns>
    public static IEnumerable<MetadataReference> GetFluentDynamoDbReferences()
    {
        var references = GetStandardReferences().ToList();
        
        // JSON serialization references
        references.Add(CreateReferenceFromType(typeof(System.Text.Json.JsonSerializer)));
        references.Add(CreateReferenceFromType(typeof(System.Text.Json.Serialization.JsonSerializerContext)));
        references.Add(CreateReferenceFromType(typeof(Newtonsoft.Json.JsonConvert)));
        
        // AWS SDK references
        references.Add(CreateReferenceFromType(typeof(Amazon.DynamoDBv2.Model.AttributeValue)));
        
        // FluentDynamoDb library references
        references.Add(CreateReferenceFromType(typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute)));
        references.Add(CreateReferenceFromType(typeof(Oproto.FluentDynamoDb.Storage.IDynamoDbEntity)));

        return references;
    }

    /// <summary>
    /// Gets metadata references including logging types for integration tests.
    /// </summary>
    /// <returns>A collection of metadata references including logging types.</returns>
    public static IEnumerable<MetadataReference> GetLoggingIntegrationReferences()
    {
        var references = GetFluentDynamoDbReferences().ToList();
        
        // Logging references
        references.Add(CreateReferenceFromType(typeof(Oproto.FluentDynamoDb.Logging.IDynamoDbLogger)));
        references.Add(CreateReferenceFromType(typeof(Oproto.FluentDynamoDb.Logging.LogLevel)));
        references.Add(CreateReferenceFromType(typeof(Oproto.FluentDynamoDb.Logging.LogEventIds)));

        return references.DistinctBy(r => r.Display).ToList();
    }

    /// <summary>
    /// Gets metadata references including Lambda package for stream conversion tests.
    /// </summary>
    /// <returns>A collection of metadata references including Lambda types.</returns>
    public static IEnumerable<MetadataReference> GetLambdaReferences()
    {
        var references = GetFluentDynamoDbReferences().ToList();
        
        // Lambda package reference
        references.Add(CreateReferenceFromType(typeof(Amazon.Lambda.DynamoDBEvents.DynamoDBEvent)));

        return references;
    }

    /// <summary>
    /// Compiles source code and loads the resulting assembly dynamically.
    /// </summary>
    /// <param name="source">The C# source code to compile.</param>
    /// <param name="references">The metadata references to use.</param>
    /// <param name="generator">Optional source generator to run.</param>
    /// <param name="preprocessorSymbols">Optional preprocessor symbols.</param>
    /// <returns>The compilation result containing the loaded assembly.</returns>
    [RequiresUnreferencedCode("Dynamic assembly loading requires reflection")]
    public static DynamicCompilationResult CompileAndLoad(
        string source,
        IEnumerable<MetadataReference> references,
        IIncrementalGenerator? generator = null,
        params string[] preprocessorSymbols)
    {
        var parseOptions = preprocessorSymbols.Length > 0
            ? CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols)
            : CSharpParseOptions.Default;
            
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        
        var compilation = CSharpCompilation.Create(
            $"TestAssembly_{Guid.NewGuid():N}",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ImmutableArray<Diagnostic> generatorDiagnostics = ImmutableArray<Diagnostic>.Empty;
        
        if (generator != null)
        {
            var driver = CSharpGeneratorDriver.Create(generator);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out generatorDiagnostics);
            compilation = (CSharpCompilation)outputCompilation;
        }

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new CompilationFailedException($"Compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        return new DynamicCompilationResult
        {
            Assembly = assembly,
            Diagnostics = generatorDiagnostics,
            EmitDiagnostics = emitResult.Diagnostics
        };
    }

    /// <summary>
    /// Gets a generic method from a type and makes it generic with the type itself.
    /// </summary>
    /// <param name="type">The type to search for the method.</param>
    /// <param name="methodName">The name of the method.</param>
    /// <param name="bindingFlags">The binding flags for method search.</param>
    /// <returns>The generic method instantiated with the type.</returns>
    [RequiresUnreferencedCode("Reflection-based method lookup")]
    public static MethodInfo GetGenericMethod(Type type, string methodName, BindingFlags bindingFlags)
    {
        var method = type.GetMethods(bindingFlags)
            .FirstOrDefault(m => m.Name == methodName && m.IsGenericMethod);
        
        if (method == null)
        {
            throw new InvalidOperationException($"Generic method '{methodName}' not found on type '{type.Name}'");
        }
        
        return method.MakeGenericMethod(type);
    }

    /// <summary>
    /// Creates an instance of a dynamically loaded type.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>A new instance of the type.</returns>
    [RequiresUnreferencedCode("Dynamic type instantiation")]
    public static object CreateInstance(Type type)
    {
        var instance = Activator.CreateInstance(type);
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of type '{type.Name}'");
        }
        return instance;
    }

    /// <summary>
    /// Sets a property value on a dynamically loaded object.
    /// </summary>
    /// <param name="instance">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value to set.</param>
    [RequiresUnreferencedCode("Reflection-based property access")]
    public static void SetProperty(object instance, string propertyName, object? value)
    {
        var type = instance.GetType();
        var property = type.GetProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type '{type.Name}'");
        }
        property.SetValue(instance, value);
    }

    /// <summary>
    /// Gets a property value from a dynamically loaded object.
    /// </summary>
    /// <param name="instance">The object instance.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value.</returns>
    [RequiresUnreferencedCode("Reflection-based property access")]
    public static object? GetProperty(object instance, string propertyName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type '{type.Name}'");
        }
        return property.GetValue(instance);
    }
}

/// <summary>
/// Result of a dynamic compilation operation.
/// </summary>
public class DynamicCompilationResult
{
    /// <summary>
    /// The loaded assembly containing the compiled code.
    /// </summary>
    public required Assembly Assembly { get; init; }
    
    /// <summary>
    /// Diagnostics from the source generator, if any.
    /// </summary>
    public ImmutableArray<Diagnostic> Diagnostics { get; init; }
    
    /// <summary>
    /// Diagnostics from the emit operation.
    /// </summary>
    public ImmutableArray<Diagnostic> EmitDiagnostics { get; init; }
}
