using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.UnitTests.Requests;

/// <summary>
/// Property-based tests verifying no AOT-unsafe reflection usage in main library.
/// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
/// **Validates: Requirements 2.1**
/// 
/// IMPORTANT: This test distinguishes between two types of reflection:
/// 
/// 1. AOT-UNSAFE reflection (breaks trimming/AOT):
///    - Assembly.GetType(), Type.GetType() - runtime type discovery
///    - type.GetMethod(), type.GetProperty(), type.GetField() - runtime member discovery
///    - Activator.CreateInstance() - dynamic instantiation
///    - BindingFlags usage - indicates runtime member lookup
///    
/// 2. AOT-SAFE reflection (works with trimming/AOT):
///    - MemberExpression.Member (FieldInfo/PropertyInfo from expression trees)
///    - Accessing MemberInfo that was captured at compile time
///    - These are safe because the types are known at compile time
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class NoReflectionPropertyTests
{
    // AOT-UNSAFE reflection patterns - these break trimming and AOT compilation
    // These patterns indicate runtime type/member discovery which cannot be statically analyzed
    private static readonly string[] AotUnsafeReflectionPatterns = new[]
    {
        "using System.Reflection;",      // Importing reflection namespace (usually indicates unsafe usage)
        "BindingFlags.",                  // Runtime member lookup flags
        "Assembly.GetType(",              // Runtime type discovery from assembly
        "Type.GetType(",                  // Runtime type discovery by name
        "Activator.CreateInstance(",      // Dynamic instantiation
        ".GetMethod(",                    // Runtime method discovery (when on Type, not MemberInfo)
        ".GetProperty(",                  // Runtime property discovery (when on Type, not MemberInfo)
        ".GetField(",                     // Runtime field discovery (when on Type, not MemberInfo)
    };
    
    // Patterns that are AOT-SAFE when used on expression tree MemberInfo
    // These access compile-time captured member information, not runtime discovery
    // Example: memberExpression.Member is FieldInfo field -> field.GetValue(obj)
    // This is safe because the FieldInfo was captured when the expression was compiled
    private static readonly string[] AotSafeExpressionTreePatterns = new[]
    {
        ".GetValue(",   // Safe when called on MemberInfo from expression tree
        ".SetValue(",   // Safe when called on MemberInfo from expression tree
        ".Invoke(",     // Safe when called on MethodInfo from expression tree (e.g., delegate invocation)
    };

    // Paths to exclude from reflection checks - only build artifacts, not source code
    private static readonly string[] ExcludedBuildArtifactPaths = new[]
    {
        "/obj/",
        "/bin/"
    };
    
    // Files with AOT-UNSAFE reflection that need architectural changes to fix
    // 
    // NOTE: The following files have been refactored and no longer contain AOT-unsafe reflection:
    // - UpdateExpressionTranslator.cs: Now uses ICollectionFormatterRegistry instead of Activator.CreateInstance
    // - DynamoDbResponseExtensions.cs: Now uses IEntityHydratorRegistry instead of GetMethod()
    // - MappingErrorHandler.cs: Removed unused ValidateRequiredProperties method that used GetProperty()
    // - EnhancedExecuteAsyncExtensions.cs: Now uses IEntityHydratorRegistry instead of GetMethod()
    // - ExpressionTranslator.cs: Now uses MemberExpression.Member directly (AOT-safe) and IGeospatialProvider
    //
    // ALL MAIN LIBRARY FILES ARE NOW AOT-SAFE!
    // The array below should remain empty. Any new AOT-unsafe reflection should be added here
    // and tracked for refactoring.
    private static readonly string[] FilesWithAotUnsafeReflection = Array.Empty<string>();
    
    // Files with AOT-SAFE reflection only (expression tree member access)
    // These files use FieldInfo/PropertyInfo from MemberExpression.Member which is AOT-safe
    // because the member info was captured at compile time when the expression was created.
    private static readonly string[] FilesWithAotSafeReflectionOnly = new[]
    {
        // Event invocation pattern - _contextAssigned?.Invoke() is delegate invocation, not reflection
        "DynamoDbOperationContextDiagnostics.cs",
    };

    /// <summary>
    /// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
    /// **Validates: Requirements 2.1**
    /// 
    /// Verifies that WithClientExtensions.cs has been removed from the codebase.
    /// This file previously used reflection to copy builder state.
    /// </summary>
    [Fact]
    public void WithClientExtensions_ShouldNotExist()
    {
        var solutionDir = FindSolutionDirectory();
        var withClientExtensionsPath = Path.Combine(
            solutionDir, 
            "Oproto.FluentDynamoDb", 
            "Requests", 
            "Extensions", 
            "WithClientExtensions.cs");
        
        Assert.False(
            File.Exists(withClientExtensionsPath), 
            $"WithClientExtensions.cs should have been deleted but still exists at: {withClientExtensionsPath}");
    }
    
    /// <summary>
    /// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any source file in the main library (Oproto.FluentDynamoDb), 
    /// excluding files with known AOT-unsafe reflection,
    /// the file SHALL NOT contain AOT-unsafe reflection patterns.
    /// </summary>
    [Fact]
    public void MainLibrary_NewFilesShouldNotContainAotUnsafeReflection()
    {
        var solutionDir = FindSolutionDirectory();
        var mainLibraryDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb");
        
        if (!Directory.Exists(mainLibraryDir))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(mainLibraryDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ExcludedBuildArtifactPaths.Any(excluded => f.Contains(excluded)))
            .Where(f => !FilesWithAotUnsafeReflection.Any(known => f.EndsWith(known)))
            .ToList();

        var violations = new List<(string File, string Pattern, int LineNumber, string Line)>();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var pattern in AotUnsafeReflectionPatterns)
                {
                    if (line.Contains(pattern))
                    {
                        violations.Add((file, pattern, i + 1, line.Trim()));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var message = "AOT-unsafe reflection found in main library (excluding known files):\n" +
                string.Join("\n", violations.Select(v => 
                    $"  {Path.GetFileName(v.File)}:{v.LineNumber} - Pattern '{v.Pattern}' in: {v.Line}"));
            Assert.Fail(message);
        }
    }
    
    /// <summary>
    /// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
    /// **Validates: Requirements 2.1**
    /// 
    /// Verifies that files marked as "AOT-safe reflection only" do not contain
    /// AOT-unsafe patterns. This ensures we correctly categorized these files.
    /// </summary>
    [Fact]
    public void FilesWithAotSafeReflection_ShouldNotContainAotUnsafePatterns()
    {
        var solutionDir = FindSolutionDirectory();
        var mainLibraryDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb");
        
        if (!Directory.Exists(mainLibraryDir))
        {
            return;
        }

        var violations = new List<(string File, string Pattern, int LineNumber, string Line)>();

        foreach (var fileName in FilesWithAotSafeReflectionOnly)
        {
            var files = Directory.GetFiles(mainLibraryDir, fileName, SearchOption.AllDirectories)
                .Where(f => !ExcludedBuildArtifactPaths.Any(excluded => f.Contains(excluded)))
                .ToList();

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (var pattern in AotUnsafeReflectionPatterns)
                    {
                        if (line.Contains(pattern))
                        {
                            violations.Add((file, pattern, i + 1, line.Trim()));
                        }
                    }
                }
            }
        }

        if (violations.Any())
        {
            var message = "Files marked as AOT-safe contain AOT-unsafe reflection patterns!\n" +
                "These files should be moved to FilesWithAotUnsafeReflection:\n" +
                string.Join("\n", violations.Select(v => 
                    $"  {Path.GetFileName(v.File)}:{v.LineNumber} - Pattern '{v.Pattern}' in: {v.Line}"));
            Assert.Fail(message);
        }
    }

    // Extension library files with known reflection (to be addressed in later tasks)
    private static readonly string[] ExtensionFilesWithKnownReflection = new[]
    {
        "SpatialQueryExtensions.cs",
        "TypedStreamProcessor.cs",
        "TypeHandlerRegistration.cs"
    };
    
    /// <summary>
    /// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any source file in extension libraries, excluding files with known reflection,
    /// the file SHALL NOT contain AOT-unsafe reflection patterns.
    /// </summary>
    [Fact]
    public void ExtensionLibraries_NewFilesShouldNotContainAotUnsafeReflection()
    {
        var solutionDir = FindSolutionDirectory();
        
        var extensionDirs = new[]
        {
            "Oproto.FluentDynamoDb.BlobStorage.S3",
            "Oproto.FluentDynamoDb.Encryption.Kms",
            "Oproto.FluentDynamoDb.FluentResults",
            "Oproto.FluentDynamoDb.Geospatial",
            "Oproto.FluentDynamoDb.Logging.Extensions",
            "Oproto.FluentDynamoDb.NewtonsoftJson",
            "Oproto.FluentDynamoDb.Streams",
            "Oproto.FluentDynamoDb.SystemTextJson"
        };

        var violations = new List<(string File, string Pattern, int LineNumber, string Line)>();

        foreach (var extDir in extensionDirs)
        {
            var fullPath = Path.Combine(solutionDir, extDir);
            if (!Directory.Exists(fullPath))
                continue;

            var sourceFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !ExcludedBuildArtifactPaths.Any(excluded => f.Contains(excluded)))
                .Where(f => !ExtensionFilesWithKnownReflection.Any(known => f.EndsWith(known)))
                .ToList();

            foreach (var file in sourceFiles)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (var pattern in AotUnsafeReflectionPatterns)
                    {
                        if (line.Contains(pattern))
                        {
                            violations.Add((file, pattern, i + 1, line.Trim()));
                        }
                    }
                }
            }
        }

        if (violations.Any())
        {
            var message = "AOT-unsafe reflection found in extension libraries (excluding known files):\n" +
                string.Join("\n", violations.Select(v => 
                    $"  {Path.GetFileName(v.File)}:{v.LineNumber} - Pattern '{v.Pattern}' in: {v.Line}"));
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// **Feature: code-cleanup-warnings-reflection, Property 1: No Reflection in Main Library**
    /// **Validates: Requirements 2.1**
    /// 
    /// Property test: For any randomly selected source file from the main library
    /// (excluding files with known AOT-unsafe reflection), it should not contain
    /// AOT-unsafe reflection patterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RandomSourceFile_ShouldNotContainAotUnsafeReflection()
    {
        var solutionDir = FindSolutionDirectory();
        var mainLibraryDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb");
        
        if (!Directory.Exists(mainLibraryDir))
        {
            return true.ToProperty();
        }

        var sourceFiles = Directory.GetFiles(mainLibraryDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ExcludedBuildArtifactPaths.Any(excluded => f.Contains(excluded)))
            .Where(f => !FilesWithAotUnsafeReflection.Any(known => f.EndsWith(known)))
            .ToArray();

        if (sourceFiles.Length == 0)
        {
            return true.ToProperty();
        }

        var fileArb = Gen.Elements(sourceFiles).ToArbitrary();

        return Prop.ForAll(fileArb, file =>
        {
            var content = File.ReadAllText(file);
            var hasAotUnsafeReflection = AotUnsafeReflectionPatterns.Any(pattern => content.Contains(pattern));
            return !hasAotUnsafeReflection;
        });
    }
    
    /// <summary>
    /// Documents the count of files with AOT-unsafe reflection for tracking progress.
    /// This test always passes but outputs the current state.
    /// </summary>
    [Fact]
    public void DocumentAotUnsafeReflectionStatus()
    {
        // This test documents the current state of AOT-unsafe reflection in the codebase
        // As we fix these files, they should be removed from FilesWithAotUnsafeReflection
        
        var aotUnsafeCount = FilesWithAotUnsafeReflection.Length;
        var aotSafeCount = FilesWithAotSafeReflectionOnly.Length;
        
        // Output for visibility in test results
        Assert.True(true, 
            $"AOT Reflection Status:\n" +
            $"  Files with AOT-UNSAFE reflection (need fixing): {aotUnsafeCount}\n" +
            $"    - {string.Join("\n    - ", FilesWithAotUnsafeReflection)}\n" +
            $"  Files with AOT-SAFE reflection only: {aotSafeCount}\n" +
            $"    - {string.Join("\n    - ", FilesWithAotSafeReflectionOnly)}");
    }

    private static string FindSolutionDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        var assemblyLocation = typeof(NoReflectionPropertyTests).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            dir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);
            while (dir != null)
            {
                if (dir.GetFiles("*.sln").Any())
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }
        
        return currentDir;
    }
}
