using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for schema version detection correctness properties.
/// Feature: schema-version-attribute
/// </summary>
public class SchemaVersionDetectionPropertyTests
{
    /// <summary>
    /// **Feature: schema-version-attribute, Property 4: Missing attribute diagnostic exclusivity**
    /// For any compilation without the attribute, FDDB110 is emitted exactly once.
    /// For any compilation with the attribute, FDDB110 is NOT emitted.
    /// **Validates: Requirements 3.1, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "4")]
    public Property WithoutAttribute_FDDB110_EmittedExactlyOnce()
    {
        var classNameGen = from prefix in Gen.Elements("My", "Test", "Sample", "Demo", "App")
                           from suffix in Gen.Elements("Class", "Entity", "Service", "Model", "Handler")
                           from id in Gen.Choose(1, 9999)
                           select $"{prefix}{suffix}{id}";

        var namespaceGen = from part1 in Gen.Elements("TestApp", "MyProject", "Demo", "Sample", "Acme")
                           from part2 in Gen.Elements("Core", "Domain", "Services", "Data", "Models")
                           select $"{part1}.{part2}";

        var gen = from className in classNameGen
                  from ns in namespaceGen
                  select (className, ns);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (className, ns) = pair;
            var source = $@"
namespace {ns}
{{
    public class {className} {{ }}
}}";
            var compilation = CreateCompilation(source);
            var result = SchemaVersionProvider.Detect(compilation);

            var fddb110Count = result.Diagnostics.Count(d => d.Id == "FDDB110");

            return (fddb110Count == 1)
                .Label($"Expected exactly 1 FDDB110 diagnostic but got {fddb110Count} for class {className} in {ns}");
        });
    }

    /// <summary>
    /// **Feature: schema-version-attribute, Property 4: Missing attribute diagnostic exclusivity (with attribute)**
    /// For any compilation with the attribute present, FDDB110 is NOT emitted.
    /// **Validates: Requirements 3.1, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "schema-version-attribute")]
    [Trait("Property", "4")]
    public Property WithAttribute_FDDB110_NotEmitted()
    {
        var majorGen = Gen.Choose(1, 100);
        var minorGen = Gen.Choose(0, 100);

        var gen = from major in majorGen
                  from minor in minorGen
                  select (major, minor);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (major, minor) = pair;
            var source = $@"
using Oproto.FluentDynamoDb.Attributes;
[assembly: FluentDynamoDbSchemaVersion({major}, {minor})]
namespace TestAssembly
{{
    public class TestEntity {{ }}
}}";
            var compilation = CreateCompilation(source);
            var result = SchemaVersionProvider.Detect(compilation);

            var fddb110Count = result.Diagnostics.Count(d => d.Id == "FDDB110");

            return (fddb110Count == 0)
                .Label($"Expected 0 FDDB110 diagnostics but got {fddb110Count} for version ({major}, {minor}). " +
                       $"Other diagnostics: [{string.Join(", ", result.Diagnostics.Select(d => d.Id))}]");
        });
    }

    #region Helper Methods

    // Cached references — resolved once, reused across all 200+ property test iterations
    private static readonly IReadOnlyList<MetadataReference> CachedReferences = ResolveReferences();
    private static readonly CSharpCompilationOptions DllOptions = new(OutputKind.DynamicallyLinkedLibrary);

    private static IReadOnlyList<MetadataReference> ResolveReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersionAttribute).Assembly.Location),
        };

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntimePath = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        return references.ToArray();
    }

    /// <summary>
    /// Creates a Roslyn compilation from the given source code, including a reference
    /// to the Oproto.FluentDynamoDb assembly (which contains FluentDynamoDbSchemaVersionAttribute).
    /// Uses cached references for performance in property tests.
    /// </summary>
    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: CachedReferences,
            options: DllOptions);
    }

    #endregion
}
