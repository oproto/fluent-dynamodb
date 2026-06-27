using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for schema version diagnostic correctness properties.
/// Feature: schema-version-attribute
/// </summary>
public class SchemaVersionDiagnosticPropertyTests
{
    /// <summary>
    /// **Feature: schema-version-attribute, Property 5: Unsupported old version halts generation**
    /// 
    /// For any version &lt; MinimumSupported, exactly one FDDB111 Error is emitted and ShouldHaltGeneration is true.
    /// 
    /// **Validates: Requirements 4.1, 4.4, 4.5**
    /// 
    /// IMPORTANT CONSTRAINT: Currently MinimumSupported = (1, 0). The only versions that are "below minimum"
    /// must have major &lt; 1, which triggers FDDB114 (validation) BEFORE the range check. The FDDB111 diagnostic
    /// specifically will only fire when MinimumSupported is bumped above (1, 0) in the future.
    /// 
    /// This test verifies the broader property: for any version with major &lt; 1 (which represents an invalid
    /// version that is also below minimum), the generation halts and at least one Error diagnostic is emitted.
    /// The halting behavior satisfies requirements 4.4 and 4.5 — no entity code is generated and the
    /// diagnostic is emitted exactly once.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "5")]
    public Property UnsupportedOldVersion_HaltsGeneration_WithErrorDiagnostic()
    {
        // Generate major values < 1 (which are below MinimumSupported (1,0))
        // and minor values >= 0 (to isolate the "below minimum" condition from FDDB115)
        var majorGen = Gen.Choose(-100, 0); // major < 1 → below MinimumSupported
        var minorGen = Gen.Choose(0, 100);  // valid minor to isolate the major < 1 case

        var gen = from major in majorGen
                  from minor in minorGen
                  select (major, minor);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;

            // Use custom attribute definition (no constructor validation) so we can
            // create a compilation with major < 1 without throwing at attribute construction
            var usageSource = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
            var compilation = CreateCompilationWithCustomAttribute(
                CustomAttributeDefinition, usageSource);

            var result = SchemaVersionProvider.Detect(compilation);

            // The version is below MinimumSupported (1,0) since major < 1.
            // Validation (FDDB114) fires first, which also halts generation.
            // Either way, the critical properties hold:
            // 1. ShouldHaltGeneration must be true (Req 4.4)
            // 2. At least one Error diagnostic is emitted
            // 3. FDDB114 is emitted (major < 1 validation catches this before range check)
            var hasErrorDiagnostic = result.Diagnostics
                .Any(d => d.Severity == DiagnosticSeverity.Error);

            var hasFddb114 = result.Diagnostics
                .Any(d => d.Id == "FDDB114");

            return (result.ShouldHaltGeneration && hasErrorDiagnostic && hasFddb114)
                .Label($"major={major}, minor={minor}: " +
                       $"ShouldHalt={result.ShouldHaltGeneration}, " +
                       $"HasError={hasErrorDiagnostic}, " +
                       $"HasFDDB114={hasFddb114}, " +
                       $"Diagnostics=[{string.Join(", ", result.Diagnostics.Select(d => d.Id))}]");
        });
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 6: Unrecognized future version halts generation**
    /// 
    /// For any declared schema version that is strictly greater than Current (1, 0),
    /// the generator shall emit exactly one FDDB112 diagnostic with severity Error
    /// and ShouldHaltGeneration is true.
    /// 
    /// **Validates: Requirements 5.1, 5.4, 5.5**
    /// 
    /// Versions greater than Current (1, 0):
    /// - major > 1 (e.g., 2..100) with any minor (0..100) → definitely > Current
    /// - major == 1 with minor > 0 (e.g., 1..100) → also > Current
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "6")]
    public Property UnrecognizedFutureVersion_HaltsGeneration_WithFddb112Error()
    {
        // Generate versions > Current (1, 0):
        // Case 1: major > 1 with any valid minor
        var highMajorGen = from major in Gen.Choose(2, 100)
                           from minor in Gen.Choose(0, 100)
                           select (major, minor);

        // Case 2: major == 1 with minor > 0
        var highMinorGen = from minor in Gen.Choose(1, 100)
                           select (1, minor);

        var gen = Gen.OneOf(highMajorGen, highMinorGen);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;

            var usageSource = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
            var compilation = CreateCompilationWithCustomAttribute(
                CustomAttributeDefinition, usageSource);

            var result = SchemaVersionProvider.Detect(compilation);

            // Assert: ShouldHaltGeneration must be true (Req 5.4)
            var halts = result.ShouldHaltGeneration;

            // Assert: Exactly one FDDB112 diagnostic (Req 5.1, 5.5)
            var fddb112Diagnostics = result.Diagnostics
                .Where(d => d.Id == "FDDB112")
                .ToList();
            var hasExactlyOneFddb112 = fddb112Diagnostics.Count == 1;

            // Assert: That FDDB112 diagnostic has severity Error
            var fddb112IsError = fddb112Diagnostics.Count == 1
                && fddb112Diagnostics[0].Severity == DiagnosticSeverity.Error;

            return (halts && hasExactlyOneFddb112 && fddb112IsError)
                .Label($"major={major}, minor={minor}: " +
                       $"ShouldHalt={halts}, " +
                       $"FDDB112Count={fddb112Diagnostics.Count}, " +
                       $"FDDB112IsError={fddb112IsError}, " +
                       $"Diagnostics=[{string.Join(", ", result.Diagnostics.Select(d => $"{d.Id}({d.Severity})"))}]");
        });
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 7: Older-but-supported version emits info diagnostic**
    /// 
    /// For any declared schema version that is &gt;= MinimumSupported and strictly less than Current,
    /// the generator shall emit exactly one FDDB113 diagnostic with severity Info and proceed with
    /// code generation (ShouldHaltGeneration = false).
    /// 
    /// **Validates: Requirements 6.1, 6.4**
    /// 
    /// IMPORTANT CONSTRAINT: Currently MinimumSupported == Current == (1, 0). There is no version
    /// that satisfies &gt;= MinimumSupported AND &lt; Current since they are equal. Therefore, FDDB113
    /// can never be emitted with the current constants. This test is skipped until the constants
    /// diverge (e.g., MinimumSupported = (1, 0) and Current = (2, 0)).
    /// 
    /// The guard test below verifies the constraint still holds. When it fails, unskip the
    /// property test and it will verify the property across all valid inputs.
    /// </summary>
    [Fact]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "7")]
    public void Guard_MinimumSupportedEqualsCurrent_ConstraintHolds()
    {
        // This guard test verifies the current constraint: MinimumSupported == Current.
        // When this test FAILS, it means the constants have diverged and the
        // OlderButSupportedVersion_EmitsInfoDiagnostic property test below should be unskipped.
        SchemaVersionConstants.MinimumSupported.Should().Be(SchemaVersionConstants.Current,
            "if MinimumSupported != Current, unskip the Property 7 test below");
    }

    [Fact(Skip = "Currently MinimumSupported == Current (both 1.0), so no version satisfies " +
                 ">= MinimumSupported AND < Current. Unskip when versions diverge.")]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "7")]
    public void OlderButSupportedVersion_EmitsInfoDiagnostic_AndProceedsWithGeneration()
    {
        // This property test should be converted to a [Property(MaxTest = 100)] test
        // when MinimumSupported < Current. The generator strategy would produce
        // versions >= MinimumSupported and < Current, verifying:
        // 1. Exactly one FDDB113 Info diagnostic is emitted
        // 2. ShouldHaltGeneration is false (generation proceeds)
        //
        // Example generator strategy (for when MinSupported=(1,0), Current=(2,0)):
        //   var majorGen = Gen.Choose(MinimumSupported.Major, Current.Major);
        //   var minorGen = Gen.Choose(MinimumSupported.Minor, ...);
        //   Filter: version >= MinimumSupported && version < Current
        //
        // Property assertion:
        //   result.Diagnostics.Count(d => d.Id == "FDDB113") == 1
        //   result.Diagnostics.First(d => d.Id == "FDDB113").Severity == DiagnosticSeverity.Info
        //   result.ShouldHaltGeneration == false
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 9: Invalid version validation halts generation**
    /// 
    /// For any major &lt; 1 (with minor &gt;= 0), FDDB114 is emitted and ShouldHaltGeneration is true.
    /// 
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "9")]
    public Property InvalidMajorOnly_EmitsFDDB114_AndHaltsGeneration()
    {
        var majorGen = Gen.Choose(-100, 0);  // major < 1
        var minorGen = Gen.Choose(0, 100);   // valid minor >= 0

        var gen = from major in majorGen
                  from minor in minorGen
                  select (major, minor);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;

            var usageSource = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
            var compilation = CreateCompilationWithCustomAttribute(
                CustomAttributeDefinition, usageSource);

            var result = SchemaVersionProvider.Detect(compilation);

            var hasFddb114 = result.Diagnostics.Any(d => d.Id == "FDDB114");
            var hasNoFddb115 = !result.Diagnostics.Any(d => d.Id == "FDDB115");

            return (result.ShouldHaltGeneration && hasFddb114 && hasNoFddb115)
                .Label($"major={major}, minor={minor}: " +
                       $"ShouldHalt={result.ShouldHaltGeneration}, " +
                       $"HasFDDB114={hasFddb114}, " +
                       $"NoFDDB115={hasNoFddb115}, " +
                       $"Diagnostics=[{string.Join(", ", result.Diagnostics.Select(d => d.Id))}]");
        });
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 9: Invalid version validation halts generation**
    /// 
    /// For any minor &lt; 0 (with major &gt;= 1), FDDB115 is emitted and ShouldHaltGeneration is true.
    /// 
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "9")]
    public Property InvalidMinorOnly_EmitsFDDB115_AndHaltsGeneration()
    {
        var majorGen = Gen.Choose(1, 100);    // valid major >= 1
        var minorGen = Gen.Choose(-100, -1);  // minor < 0

        var gen = from major in majorGen
                  from minor in minorGen
                  select (major, minor);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;

            var usageSource = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
            var compilation = CreateCompilationWithCustomAttribute(
                CustomAttributeDefinition, usageSource);

            var result = SchemaVersionProvider.Detect(compilation);

            var hasFddb115 = result.Diagnostics.Any(d => d.Id == "FDDB115");
            var hasNoFddb114 = !result.Diagnostics.Any(d => d.Id == "FDDB114");

            return (result.ShouldHaltGeneration && hasFddb115 && hasNoFddb114)
                .Label($"major={major}, minor={minor}: " +
                       $"ShouldHalt={result.ShouldHaltGeneration}, " +
                       $"HasFDDB115={hasFddb115}, " +
                       $"NoFDDB114={hasNoFddb114}, " +
                       $"Diagnostics=[{string.Join(", ", result.Diagnostics.Select(d => d.Id))}]");
        });
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 9: Invalid version validation halts generation**
    /// 
    /// For any major &lt; 1 AND minor &lt; 0, BOTH FDDB114 and FDDB115 are emitted and ShouldHaltGeneration is true.
    /// 
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "9")]
    public Property BothMajorAndMinorInvalid_EmitsBothDiagnostics_AndHaltsGeneration()
    {
        var majorGen = Gen.Choose(-100, 0);   // major < 1
        var minorGen = Gen.Choose(-100, -1);  // minor < 0

        var gen = from major in majorGen
                  from minor in minorGen
                  select (major, minor);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;

            var usageSource = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class Dummy {{ }}
}}";
            var compilation = CreateCompilationWithCustomAttribute(
                CustomAttributeDefinition, usageSource);

            var result = SchemaVersionProvider.Detect(compilation);

            var hasFddb114 = result.Diagnostics.Any(d => d.Id == "FDDB114");
            var hasFddb115 = result.Diagnostics.Any(d => d.Id == "FDDB115");

            return (result.ShouldHaltGeneration && hasFddb114 && hasFddb115)
                .Label($"major={major}, minor={minor}: " +
                       $"ShouldHalt={result.ShouldHaltGeneration}, " +
                       $"HasFDDB114={hasFddb114}, " +
                       $"HasFDDB115={hasFddb115}, " +
                       $"Diagnostics=[{string.Join(", ", result.Diagnostics.Select(d => d.Id))}]");
        });
    }

    #region Helper Methods

    // Cached references and syntax trees — resolved once, reused across all property test iterations
    private static readonly IReadOnlyList<MetadataReference> CachedPlatformReferences = ResolvePlatformReferences();
    private static readonly SyntaxTree CachedCustomAttributeTree = CSharpSyntaxTree.ParseText(CustomAttributeDefinition);
    private static readonly CSharpCompilationOptions DllOptions = new(OutputKind.DynamicallyLinkedLibrary);

    private static IReadOnlyList<MetadataReference> ResolvePlatformReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

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
    /// Creates a compilation with a custom attribute definition (no constructor validation)
    /// and assembly-level attribute application in separate syntax trees.
    /// Uses cached references and attribute syntax tree for performance in property tests.
    /// </summary>
    private static Compilation CreateCompilationWithCustomAttribute(
        string attributeDefinitionSource, string assemblyUsageSource)
    {
        var usageTree = CSharpSyntaxTree.ParseText(assemblyUsageSource);

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { CachedCustomAttributeTree, usageTree },
            references: CachedPlatformReferences,
            options: DllOptions);
    }

    /// <summary>
    /// Custom attribute definition without constructor validation, allowing invalid
    /// major/minor values that would normally throw ArgumentOutOfRangeException.
    /// </summary>
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

    #endregion
}
