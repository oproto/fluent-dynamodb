using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for SchemaVersionProvider.Detect() using Roslyn in-memory compilations.
/// Validates: Requirements 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 4.1, 4.4, 4.5, 5.1, 5.4, 5.5, 6.1, 6.4, 9.1, 9.2, 9.3, 9.4, 9.5
/// </summary>
public class SchemaVersionProviderTests
{
    #region Missing Attribute (FDDB110)

    [Fact]
    public void Detect_MissingAttribute_ReturnsDefaultVersion_AndFDDB110Warning()
    {
        // Arrange: source code with no schema version attribute
        const string source = @"
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.Version.Should().Be(SchemaVersionConstants.Default);
        result.ShouldHaltGeneration.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle();
        result.Diagnostics[0].Id.Should().Be("FDDB110");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region Valid Attribute (1,0)

    [Fact]
    public void Detect_ValidAttribute_1_0_ReturnsCorrectVersion_WithNoDiagnostics()
    {
        // Arrange: source with valid attribute matching Current version
        const string source = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.Version.Major.Should().Be(1);
        result.Version.Minor.Should().Be(0);
        result.ShouldHaltGeneration.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Should().BeEmpty();
    }

    #endregion

    #region Version Above Current (FDDB112)

    [Fact]
    public void Detect_VersionAboveCurrent_ReturnsFDDB112Error_AndShouldHalt()
    {
        // Arrange: version 2.0 is above Current (1.0)
        const string source = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(2, 0)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.Version.Major.Should().Be(2);
        result.Version.Minor.Should().Be(0);
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle();
        result.Diagnostics[0].Id.Should().Be("FDDB112");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Detect_VersionAboveCurrent_MinorHigher_ReturnsFDDB112Error_AndShouldHalt()
    {
        // Arrange: version 1.1 is above Current (1.0)
        const string source = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(1, 1)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilation(source);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.Version.Major.Should().Be(1);
        result.Version.Minor.Should().Be(1);
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle();
        result.Diagnostics[0].Id.Should().Be("FDDB112");
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Version Equal to Current

    [Fact]
    public void Detect_VersionEqualToCurrent_ReturnsVersion_WithNoDiagnostics()
    {
        // Arrange: version matching Current exactly
        var currentMajor = SchemaVersionConstants.Current.Major;
        var currentMinor = SchemaVersionConstants.Current.Minor;
        var source = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({currentMajor}, {currentMinor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
        var compilation = CreateCompilation(source);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.Version.Major.Should().Be(currentMajor);
        result.Version.Minor.Should().Be(currentMinor);
        result.ShouldHaltGeneration.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Should().BeEmpty();
    }

    #endregion

    #region Version Between Minimum and Current (FDDB113)

    [Fact(Skip = "Currently MinimumSupported == Current (both 1.0), so no version can be between them. " +
                 "This test documents the constraint and should be unskipped when versions diverge.")]
    public void Detect_VersionBetweenMinAndCurrent_ReturnsFDDB113Info()
    {
        // This test can only work when MinimumSupported < Current.
        // Currently both are (1, 0), so this scenario is impossible.
        // When the version range diverges, use a version like (MinimumSupported.Major, MinimumSupported.Minor)
        // that is >= MinimumSupported but < Current.
    }

    #endregion

    #region Major < 1 (FDDB114)

    [Fact]
    public void Detect_MajorLessThan1_ReturnsFDDB114Error_AndShouldHalt()
    {
        // Arrange: major = 0. Uses a custom attribute definition (separate syntax tree)
        // to bypass constructor validation that would throw at runtime.
        const string usageSource = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(0, 5)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilationWithCustomAttribute(
            CustomAttributeDefinition, usageSource);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB114");
        result.Diagnostics.First(d => d.Id == "FDDB114").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Minor < 0 (FDDB115)

    [Fact]
    public void Detect_MinorLessThan0_ReturnsFDDB115Error_AndShouldHalt()
    {
        // Arrange: minor = -1. Uses custom attribute definition to bypass constructor validation.
        const string usageSource = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(1, -1)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilationWithCustomAttribute(
            CustomAttributeDefinition, usageSource);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB115");
        result.Diagnostics.First(d => d.Id == "FDDB115").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Both Major < 1 and Minor < 0 (FDDB114 + FDDB115)

    [Fact]
    public void Detect_BothMajorAndMinorInvalid_ReturnsBothFDDB114AndFDDB115()
    {
        // Arrange: major = 0, minor = -1
        const string usageSource = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(0, -1)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilationWithCustomAttribute(
            CustomAttributeDefinition, usageSource);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB114");
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB115");
    }

    #endregion

    #region Multiple Attributes (FDDB116)

    [Fact]
    public void Detect_MultipleAttributes_EmitsFDDB116Error_AndShouldHalt()
    {
        // Arrange: multiple attributes (AllowMultiple = true to simulate IL manipulation)
        const string usageSource = @"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
[assembly: FluentDynamoDbSchemaVersion(2, 0)]
namespace TestAssembly
{
    public class Dummy { }
}";
        var compilation = CreateCompilationWithCustomAttribute(
            CustomAttributeDefinitionAllowMultiple, usageSource);

        // Act
        var result = SchemaVersionProvider.Detect(compilation);

        // Assert
        result.ShouldHaltGeneration.Should().BeTrue();
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB116");
        result.Diagnostics.First(d => d.Id == "FDDB116").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Helper Methods

    // Cached references and syntax trees — resolved once, reused across all test methods
    private static readonly IReadOnlyList<MetadataReference> CachedRealAttrReferences = ResolveRealAttrReferences();
    private static readonly IReadOnlyList<MetadataReference> CachedPlatformReferences = ResolvePlatformReferences();
    private static readonly SyntaxTree CachedCustomAttributeTree = CSharpSyntaxTree.ParseText(CustomAttributeDefinition);
    private static readonly SyntaxTree CachedCustomAttributeAllowMultipleTree = CSharpSyntaxTree.ParseText(CustomAttributeDefinitionAllowMultiple);
    private static readonly CSharpCompilationOptions DllOptions = new(OutputKind.DynamicallyLinkedLibrary);

    private static IReadOnlyList<MetadataReference> ResolveRealAttrReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersionAttribute).Assembly.Location),
        };

        var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntimePath = System.IO.Path.Combine(runtimeDir, "System.Runtime.dll");
        if (System.IO.File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        return references.ToArray();
    }

    private static IReadOnlyList<MetadataReference> ResolvePlatformReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator);

        return trustedAssemblies
            .Where(path => path.EndsWith("System.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("mscorlib.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || path.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToArray();
    }

    /// <summary>
    /// Creates a Roslyn compilation from the given source code, including a reference
    /// to the Oproto.FluentDynamoDb assembly (which contains FluentDynamoDbSchemaVersionAttribute).
    /// </summary>
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: CachedRealAttrReferences,
            options: DllOptions);
    }

    /// <summary>
    /// Creates a compilation with a custom attribute definition in one syntax tree and
    /// assembly-level attribute application in another.
    /// </summary>
    private static Compilation CreateCompilationWithCustomAttribute(
        string attributeDefinitionSource, string assemblyUsageSource)
    {
        var cachedAttrTree = attributeDefinitionSource == CustomAttributeDefinition
            ? CachedCustomAttributeTree
            : attributeDefinitionSource == CustomAttributeDefinitionAllowMultiple
                ? CachedCustomAttributeAllowMultipleTree
                : CSharpSyntaxTree.ParseText(attributeDefinitionSource);

        var usageTree = CSharpSyntaxTree.ParseText(assemblyUsageSource);

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { cachedAttrTree, usageTree },
            references: CachedPlatformReferences,
            options: DllOptions);
    }

    private const string CustomAttributeDefinition = @"
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
}";

    private const string CustomAttributeDefinitionAllowMultiple = @"
using System;

namespace Oproto.FluentDynamoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
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
}";

    #endregion
}
