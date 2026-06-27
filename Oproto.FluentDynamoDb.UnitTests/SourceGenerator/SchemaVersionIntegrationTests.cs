using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// End-to-end integration tests for schema version attribute interaction with the source generator.
/// These tests run the full generator pipeline via CSharpGeneratorDriver and verify both
/// diagnostic output and generated source presence/absence.
/// </summary>
public class SchemaVersionIntegrationTests
{
    #region Test 10.2: Generation without schema version attribute (FDDB110 warning, code still generated)

    /// <summary>
    /// Validates: Requirements 3.1, 3.4
    /// When no [assembly: FluentDynamoDbSchemaVersion] attribute is present,
    /// FDDB110 warning is emitted AND entity code is still generated.
    /// </summary>
    [Fact]
    public void Generation_WithoutSchemaVersionAttribute_EmitsFDDB110Warning_AndStillGeneratesEntityCode()
    {
        // Arrange: A DynamoDB entity with NO schema version attribute
        const string source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestAssembly
{
    [DynamoDbTable(""TestTable"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilationWithRealAttribute(source);

        // Act: Run the source generator
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: FDDB110 warning is present
        diagnostics.Should().Contain(d => d.Id == "FDDB110",
            "FDDB110 warning should be emitted when schema version attribute is missing");

        var fddb110 = diagnostics.First(d => d.Id == "FDDB110");
        fddb110.Severity.Should().Be(DiagnosticSeverity.Warning);

        // Assert: Entity source IS still generated (generation was not halted)
        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .ToList();

        generatedSources.Should().NotBeEmpty(
            "entity code should still be generated even without schema version attribute");

        generatedSources.Should().Contain(
            s => s.FilePath.Contains("TestEntity"),
            "generated sources should include the TestEntity");
    }

    #endregion


    #region Test 10.3: Generation halted with unsupported version

    /// <summary>
    /// Verifies that declaring a version with major &lt; 1 (below the minimum supported version)
    /// halts generation entirely: an error diagnostic is emitted and no entity source is generated.
    /// 
    /// Since MinimumSupported = (1, 0) and the real attribute constructor throws for major &lt; 1,
    /// this test uses a custom attribute definition (without constructor validation) to declare
    /// major=0. This triggers FDDB114 (major validation error) which halts generation.
    /// 
    /// Validates: Requirements 4.1, 4.4
    /// </summary>
    [Fact]
    public void Generation_WithVersionBelowMinimum_HaltsAndEmitsError_NoEntitySourceGenerated()
    {
        // Arrange: Custom attribute without constructor validation allows major=0
        const string attributeDefinition = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class FluentDynamoDbSchemaVersionAttribute : Attribute
    {
        public int Major { get; }
        public int Minor { get; }
        public FluentDynamoDbSchemaVersionAttribute(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public bool IsDefault { get; set; }
        public DynamoDbTableAttribute(string tableName) { TableName = tableName; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class PartitionKeyAttribute : Attribute
    {
        public string? Prefix { get; set; }
        public string Separator { get; set; } = ""#"";
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DynamoDbAttributeAttribute : Attribute
    {
        public string AttributeName { get; }
        public string? Format { get; set; }
        public DynamoDbAttributeAttribute(string attributeName) { AttributeName = attributeName; }
    }
}";

        const string entitySource = @"
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(0, 5)]

namespace TestAssembly
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilationWithCustomAttributes(attributeDefinition, entitySource);

        // Act: Run the source generator
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: FDDB114 error should be emitted (major < 1 validation)
        diagnostics.Should().Contain(d => d.Id == "FDDB114",
            "major=0 should trigger FDDB114 error diagnostic");

        var fddb114 = diagnostics.First(d => d.Id == "FDDB114");
        fddb114.Severity.Should().Be(DiagnosticSeverity.Error);

        // Assert: No entity source should be generated (generation was halted)
        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .ToList();

        generatedSources.Should().BeEmpty(
            "when schema version validation fails, no entity source should be generated");
    }

    #endregion

    #region Test 10.4: Generation halted with future version

    /// <summary>
    /// Verifies that declaring a schema version above Current (2.0 > 1.0) emits FDDB112 error
    /// and halts entity code generation entirely.
    /// 
    /// Validates: Requirements 5.1, 5.4
    /// </summary>
    [Fact]
    public void Generation_WithFutureVersion_EmitsFDDB112Error_AndNoEntitySourceGenerated()
    {
        // Arrange: assembly declares schema version 2.0 which is above Current (1.0)
        const string source = @"
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(2, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilationWithRealAttribute(source);

        // Act: Run the source generator
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: FDDB112 error is present
        diagnostics.Should().Contain(d => d.Id == "FDDB112",
            "version 2.0 is above Current (1.0) and should trigger FDDB112");

        var fddb112 = diagnostics.First(d => d.Id == "FDDB112");
        fddb112.Severity.Should().Be(DiagnosticSeverity.Error);

        // Assert: no entity source is generated (generation was halted)
        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .ToList();

        generatedSources.Should().BeEmpty(
            "generation should be halted when declared version is above current");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a compilation using the real FluentDynamoDb attribute assembly.
    /// Used for tests where the attribute values pass constructor validation.
    /// </summary>
    private static CSharpCompilation CreateCompilationWithRealAttribute(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Oproto.FluentDynamoDb.Attributes.DynamoDbTableAttribute).Assembly.Location),
        };

        // Add System.Runtime reference (needed for .NET Core/5+)
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntimePath = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add System.Collections reference (may be needed for generated code)
        var systemCollectionsPath = Path.Combine(runtimeDir, "System.Collections.dll");
        if (File.Exists(systemCollectionsPath))
        {
            references.Add(MetadataReference.CreateFromFile(systemCollectionsPath));
        }

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a compilation using custom attribute definitions (separate syntax tree)
    /// and the entity usage source. This bypasses the real attribute's constructor validation.
    /// </summary>
    private static CSharpCompilation CreateCompilationWithCustomAttributes(
        string attributeDefinitionSource, string entitySource)
    {
        var attrTree = CSharpSyntaxTree.ParseText(attributeDefinitionSource);
        var entityTree = CSharpSyntaxTree.ParseText(entitySource);

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Where(path => path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("System.Collections.dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { attrTree, entityTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
