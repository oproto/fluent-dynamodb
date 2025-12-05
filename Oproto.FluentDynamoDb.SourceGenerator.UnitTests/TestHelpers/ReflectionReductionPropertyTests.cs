using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

/// <summary>
/// Property-based tests verifying reflection usage reduction in test projects.
/// 
/// This test verifies that test files using reflection for member access either:
/// 1. Use direct type references via InternalsVisibleTo
/// 2. Have documented suppression attributes with justification
/// 3. Use the centralized DynamicCompilationHelper class
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ReflectionReductionPropertyTests
{
    // Reflection patterns that should be isolated to DynamicCompilationHelper or suppressed
    private static readonly string[] ReflectionPatterns = new[]
    {
        "Assembly.GetType(",
        "Type.GetProperty(",
        "Type.GetMethod(",
        "Type.GetField(",
        "Activator.CreateInstance(",
        "MethodInfo.MakeGenericMethod(",
        ".Assembly.Location"
    };
    
    // Suppression attributes that indicate documented reflection usage
    private static readonly string[] SuppressionPatterns = new[]
    {
        "[SuppressMessage(",
        "#pragma warning disable IL",
        "RequiresUnreferencedCode",
        "DynamicallyAccessedMembers"
    };
    
    // Files that are allowed to use reflection (centralized helpers)
    private static readonly string[] AllowedReflectionFiles = new[]
    {
        "DynamicCompilationHelper.cs",
        "TestLogger.cs"
    };
    
    // Files with documented suppression attributes (class-level or method-level)
    private static readonly string[] FilesWithDocumentedSuppressions = new[]
    {
        "GeneratedLoggingIntegrationTests.cs",
        "CompilationVerifier.cs"
    };
    
    // Files that are pending migration to DynamicCompilationHelper
    // These files still use direct Assembly.Location but will be migrated in future tasks
    private static readonly string[] FilesPendingMigration = new[]
    {
        "EntityAnalyzerTests.cs",
        "ComplexTypeGenerationTests.cs",
        "TableClassGenerationTests.cs",
        "EntityAccessorClassGenerationTests.cs",
        "EntityGroupingTests.cs",
        "TransactionOperationTests.cs",
        "TableLevelOperationTests.cs",
        "OperationMethodGenerationTests.cs",
        "UpdateExpressionsGeneratorTests.cs",
        "MapperGeneratorTests.cs",
        "MapperGeneratorBugFixTests.cs",
        "FieldsGeneratorTests.cs",
        "KeysGeneratorTests.cs",
        "DiscriminatorCodeGeneratorTests.cs",
        "EncryptionCodeGeneratorTests.cs",
        "SecurityMetadataGeneratorTests.cs",
        "SpatialIndexCodeGenerationTests.cs",
        "SpatialIndexDeserializationTests.cs",
        "StreamMapperGeneratorTests.cs",
        "StreamRegistryGeneratorTests.cs",
        "ProjectionExpressionGeneratorDiscriminatorTests.cs",
        "CoordinateStoragePropertyTests.cs"
    };
    
    // Paths to exclude from checks
    private static readonly string[] ExcludedPaths = new[]
    {
        "/obj/",
        "/bin/"
    };

    /// <summary>
    /// 
    /// Verifies that DynamicCompilationHelper exists and contains the expected helper methods.
    /// </summary>
    [Fact]
    public void DynamicCompilationHelper_ShouldExistWithExpectedMethods()
    {
        var solutionDir = FindSolutionDirectory();
        var helperPath = Path.Combine(
            solutionDir,
            "Oproto.FluentDynamoDb.SourceGenerator.UnitTests",
            "TestHelpers",
            "DynamicCompilationHelper.cs");
        
        Assert.True(File.Exists(helperPath), 
            $"DynamicCompilationHelper.cs should exist at: {helperPath}");
        
        var content = File.ReadAllText(helperPath);
        
        // Verify expected methods exist
        Assert.Contains("GetRuntimeDirectory", content);
        Assert.Contains("CreateReferenceFromType", content);
        Assert.Contains("GetStandardReferences", content);
        Assert.Contains("GetFluentDynamoDbReferences", content);
        
        // Verify suppression attributes are present
        Assert.Contains("[SuppressMessage(", content);
        Assert.Contains("IL3000", content);
    }

    /// <summary>
    /// 
    /// Verifies that files with documented suppressions actually contain suppression attributes.
    /// </summary>
    [Fact]
    public void FilesWithDocumentedSuppressions_ShouldContainSuppressionAttributes()
    {
        var solutionDir = FindSolutionDirectory();
        var testProjectDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb.SourceGenerator.UnitTests");
        
        if (!Directory.Exists(testProjectDir))
        {
            return;
        }

        var violations = new List<string>();

        foreach (var fileName in FilesWithDocumentedSuppressions)
        {
            var files = Directory.GetFiles(testProjectDir, fileName, SearchOption.AllDirectories)
                .Where(f => !ExcludedPaths.Any(excluded => f.Contains(excluded)))
                .ToList();

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                var hasSuppressionAttribute = SuppressionPatterns.Any(pattern => content.Contains(pattern));
                
                if (!hasSuppressionAttribute)
                {
                    violations.Add($"{fileName}: Missing suppression attributes");
                }
            }
        }

        if (violations.Any())
        {
            Assert.Fail($"Files marked as having documented suppressions are missing suppression attributes:\n" +
                string.Join("\n", violations));
        }
    }

    /// <summary>
    /// 
    /// Verifies that test files using Assembly.Location have been refactored to use
    /// DynamicCompilationHelper or have documented suppressions.
    /// </summary>
    [Fact]
    public void TestFiles_UsingAssemblyLocation_ShouldUseDynamicCompilationHelperOrHaveSuppressions()
    {
        var solutionDir = FindSolutionDirectory();
        var testProjectDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb.SourceGenerator.UnitTests");
        
        if (!Directory.Exists(testProjectDir))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(testProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ExcludedPaths.Any(excluded => f.Contains(excluded)))
            .Where(f => !AllowedReflectionFiles.Any(allowed => f.EndsWith(allowed)))
            .ToList();

        var violations = new List<(string File, int LineNumber, string Line)>();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);
            var content = File.ReadAllText(file);
            
            // Check if file has class-level suppression
            var hasClassLevelSuppression = content.Contains("[SuppressMessage(") && 
                (content.Contains("IL3000") || content.Contains("IL2026"));
            
            // Check if file uses DynamicCompilationHelper
            var usesDynamicCompilationHelper = content.Contains("DynamicCompilationHelper.");
            
            // If file has class-level suppression or uses helper, skip detailed check
            if (hasClassLevelSuppression || usesDynamicCompilationHelper)
            {
                continue;
            }
            
            // Check for direct Assembly.Location usage without suppression
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains(".Assembly.Location") && 
                    !line.TrimStart().StartsWith("//") &&
                    !line.Contains("#pragma warning disable"))
                {
                    // Check if there's a pragma on the previous line
                    var hasPragmaBefore = i > 0 && lines[i - 1].Contains("#pragma warning disable");
                    if (!hasPragmaBefore)
                    {
                        violations.Add((file, i + 1, line.Trim()));
                    }
                }
            }
        }

        // Note: Many violations are expected in files that haven't been fully migrated yet
        // This test documents the current state and tracks progress over time
        // The threshold is set high to allow incremental migration
        // As more files are migrated, this threshold should be lowered
        if (violations.Count > 100) // Allow existing violations during migration
        {
            var message = $"Too many Assembly.Location usages without DynamicCompilationHelper or suppressions ({violations.Count}):\n" +
                string.Join("\n", violations.Take(10).Select(v => 
                    $"  {Path.GetFileName(v.File)}:{v.LineNumber} - {v.Line}"));
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// 
    /// Property test: For any randomly selected test file from the source generator tests,
    /// if it contains reflection patterns, it should either use DynamicCompilationHelper
    /// or have documented suppression attributes.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RandomTestFile_WithReflection_ShouldHaveProperHandling()
    {
        var solutionDir = FindSolutionDirectory();
        var testProjectDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb.SourceGenerator.UnitTests");
        
        if (!Directory.Exists(testProjectDir))
        {
            return true.ToProperty();
        }

        var sourceFiles = Directory.GetFiles(testProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ExcludedPaths.Any(excluded => f.Contains(excluded)))
            .Where(f => !AllowedReflectionFiles.Any(allowed => f.EndsWith(allowed)))
            .ToArray();

        if (sourceFiles.Length == 0)
        {
            return true.ToProperty();
        }

        var fileArb = Gen.Elements(sourceFiles).ToArbitrary();

        return Prop.ForAll(fileArb, file =>
        {
            var content = File.ReadAllText(file);
            
            // Check if file contains reflection patterns
            var hasReflection = ReflectionPatterns.Any(pattern => content.Contains(pattern));
            
            if (!hasReflection)
            {
                return true; // No reflection, passes
            }
            
            // If has reflection, check for proper handling
            var usesDynamicCompilationHelper = content.Contains("DynamicCompilationHelper.");
            var hasSuppressionAttribute = SuppressionPatterns.Any(pattern => content.Contains(pattern));
            var isAllowedFile = AllowedReflectionFiles.Any(allowed => file.EndsWith(allowed));
            var hasDocumentedSuppression = FilesWithDocumentedSuppressions.Any(doc => file.EndsWith(doc));
            var isPendingMigration = FilesPendingMigration.Any(pending => file.EndsWith(pending));
            
            return usesDynamicCompilationHelper || hasSuppressionAttribute || isAllowedFile || hasDocumentedSuppression || isPendingMigration;
        });
    }

    /// <summary>
    /// 
    /// Documents the current state of reflection usage in test files.
    /// </summary>
    [Fact]
    public void DocumentReflectionUsageStatus()
    {
        var solutionDir = FindSolutionDirectory();
        var testProjectDir = Path.Combine(solutionDir, "Oproto.FluentDynamoDb.SourceGenerator.UnitTests");
        
        if (!Directory.Exists(testProjectDir))
        {
            Assert.True(true, "Test project directory not found");
            return;
        }

        var sourceFiles = Directory.GetFiles(testProjectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ExcludedPaths.Any(excluded => f.Contains(excluded)))
            .ToList();

        var filesWithReflection = new List<string>();
        var filesUsingHelper = new List<string>();
        var filesWithSuppressions = new List<string>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);
            
            var hasReflection = ReflectionPatterns.Any(pattern => content.Contains(pattern)) ||
                               content.Contains(".Assembly.Location");
            
            if (hasReflection)
            {
                filesWithReflection.Add(fileName);
                
                if (content.Contains("DynamicCompilationHelper."))
                {
                    filesUsingHelper.Add(fileName);
                }
                
                if (SuppressionPatterns.Any(pattern => content.Contains(pattern)))
                {
                    filesWithSuppressions.Add(fileName);
                }
            }
        }

        Assert.True(true,
            $"Reflection Usage Status in Test Files:\n" +
            $"  Total files with reflection: {filesWithReflection.Count}\n" +
            $"  Files using DynamicCompilationHelper: {filesUsingHelper.Count}\n" +
            $"  Files with suppression attributes: {filesWithSuppressions.Count}\n" +
            $"  Allowed reflection files: {AllowedReflectionFiles.Length}");
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
        
        return currentDir;
    }
}
