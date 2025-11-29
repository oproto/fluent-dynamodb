using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

[Trait("Category", "Unit")]
public class SecurityDiagnosticsTests
{
    [Fact]
    public void EncryptedAttribute_WithoutEncryptionKmsPackage_EmitsWarning()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [DynamoDbAttribute(""pk"")]
        [PartitionKey]
        public string Id { get; set; }

        [DynamoDbAttribute(""secret"")]
        [Encrypted]
        public string SecretData { get; set; }
    }
}";

        // Act
        var diagnostics = GetDiagnostics(source, includeEncryptionKms: false);

        // Assert
        var warning = diagnostics.FirstOrDefault(d => d.Id == "SEC001");
        warning.Should().NotBeNull("should emit SEC001 diagnostic");
        warning!.Severity.Should().Be(DiagnosticSeverity.Warning,
            "should be a warning, not an error");
        warning.GetMessage().Should().Contain("SecretData",
            "should mention the property name");
        warning.GetMessage().Should().Contain("TestEntity",
            "should mention the entity name");
        warning.GetMessage().Should().Contain("Oproto.FluentDynamoDb.Encryption.Kms",
            "should mention the required package");
    }

    [Fact]
    public void EncryptedAttribute_WithEncryptionKmsPackage_DoesNotEmitWarning()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [DynamoDbAttribute(""pk"")]
        [PartitionKey]
        public string Id { get; set; }

        [DynamoDbAttribute(""secret"")]
        [Encrypted]
        public string SecretData { get; set; }
    }
}";

        // Act
        var diagnostics = GetDiagnostics(source, includeEncryptionKms: true);

        // Assert
        var warning = diagnostics.FirstOrDefault(d => d.Id == "SEC001");
        warning.Should().BeNull("should not emit SEC001 diagnostic when package is referenced");
    }

    [Fact]
    public void MultipleEncryptedProperties_WithoutEncryptionKmsPackage_EmitsWarningForEach()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [DynamoDbAttribute(""pk"")]
        [PartitionKey]
        public string Id { get; set; }

        [DynamoDbAttribute(""secret1"")]
        [Encrypted]
        public string SecretData1 { get; set; }

        [DynamoDbAttribute(""secret2"")]
        [Encrypted]
        public string SecretData2 { get; set; }

        [DynamoDbAttribute(""secret3"")]
        [Encrypted]
        public string SecretData3 { get; set; }
    }
}";

        // Act
        var diagnostics = GetDiagnostics(source, includeEncryptionKms: false);

        // Assert
        var warnings = diagnostics.Where(d => d.Id == "SEC001").ToList();
        warnings.Should().HaveCount(3, "should emit one warning per encrypted property");
        warnings.Select(w => w.GetMessage()).Should().Contain(m => m.Contains("SecretData1"));
        warnings.Select(w => w.GetMessage()).Should().Contain(m => m.Contains("SecretData2"));
        warnings.Select(w => w.GetMessage()).Should().Contain(m => m.Contains("SecretData3"));
    }

    [Fact]
    public void SensitiveAttribute_WithoutEncryptedAttribute_DoesNotEmitWarning()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [DynamoDbAttribute(""pk"")]
        [PartitionKey]
        public string Id { get; set; }

        [DynamoDbAttribute(""sensitive"")]
        [Sensitive]
        public string SensitiveData { get; set; }
    }
}";

        // Act
        var diagnostics = GetDiagnostics(source, includeEncryptionKms: false);

        // Assert
        var warning = diagnostics.FirstOrDefault(d => d.Id == "SEC001");
        warning.Should().BeNull("should not emit warning for Sensitive attribute without Encrypted");
    }

    /// <summary>
    /// Gets diagnostics from running the source generator on the provided source.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static IEnumerable<Diagnostic> GetDiagnostics(string source, bool includeEncryptionKms)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        // Use DynamicCompilationHelper for standard references
        var references = TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

        // Conditionally add Encryption.Kms package reference
        if (includeEncryptionKms)
        {
            // Create a mock assembly reference for Encryption.Kms
            var encryptionKmsAssembly = CreateMockEncryptionKmsAssembly();
            references.Add(MetadataReference.CreateFromImage(encryptionKmsAssembly));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the source generator
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        return generatorDiagnostics;
    }

    private static byte[] CreateMockEncryptionKmsAssembly()
    {
        // Create a minimal assembly that represents Oproto.FluentDynamoDb.Encryption.Kms
        var source = @"
namespace Oproto.FluentDynamoDb.Encryption.Kms
{
    public class AwsEncryptionSdkFieldEncryptor { }
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var compilation = CSharpCompilation.Create(
            "Oproto.FluentDynamoDb.Encryption.Kms",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetStandardReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new System.IO.MemoryStream();
        var emitResult = compilation.Emit(ms);
        
        if (!emitResult.Success)
        {
            throw new InvalidOperationException("Failed to create mock Encryption.Kms assembly");
        }

        return ms.ToArray();
    }
}
