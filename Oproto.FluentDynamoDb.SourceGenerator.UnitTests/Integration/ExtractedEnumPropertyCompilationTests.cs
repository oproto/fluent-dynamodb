using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration compilation tests for entities with enum [Extracted] properties.
/// 
/// These tests run the full source generator pipeline and compile the output using
/// in-memory Roslyn compilation, catching type conversion issues that string-matching
/// assertions would miss.
/// 
/// Validates: Requirements 2.1, 2.2
/// </summary>
public class ExtractedEnumPropertyCompilationTests
{
    /// <summary>
    /// Verifies that generated code compiles when an entity has an enum [Extracted] property.
    /// 
    /// This is the exact scenario that triggered the bug: an enum type like SnsSubscriptionTopic
    /// as an [Extracted] property requires Enum.Parse in the generated code. Without the fix,
    /// the generator would emit a bare string assignment causing CS0029.
    /// </summary>
    [Fact]
    public void EnumExtractedProperty_GeneratedCodeCompiles()
    {
        // Arrange: Entity with enum [Extracted] property
        var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace TestNamespace
{
    public enum SnsSubscriptionTopic { Orders, Notifications }

    [DynamoDbTable(""Subscriptions"")]
    public partial class Subscription
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TopicType"", ""TopicId"", Separator = ""#"")]
        public string Topic { get; set; } = string.Empty;

        [Extracted(""Topic"", 0)]
        public SnsSubscriptionTopic TopicType { get; set; }

        [Extracted(""Topic"", 1)]
        public string TopicId { get; set; } = string.Empty;
    }
}";

        // Act: Run source generator and compile the output
        var compilation = CreateCompilationWithGenerator(source);

        // Assert: No compilation errors
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0,
            "Generated code for entity with enum [Extracted] property must compile without errors. " +
            $"Found {errors.Count} error(s):\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// Verifies that generated code compiles when an entity has an enum [Extracted] property
    /// whose name does NOT contain common enum suffixes (Status, Type, Kind, State).
    /// 
    /// This validates that the fix uses Roslyn semantic analysis (PropertyModel.IsEnum)
    /// rather than the old name-based heuristic that only matched certain suffixes.
    /// </summary>
    [Fact]
    public void EnumExtractedProperty_WithNonStandardName_GeneratedCodeCompiles()
    {
        // Arrange: Enum name "Priority" does not contain Status/Type/Kind/State
        var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace TestNamespace
{
    public enum Priority { Low, Medium, High, Critical }

    [DynamoDbTable(""Tasks"")]
    public partial class TaskItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TaskPriority"", ""TaskLabel"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public Priority TaskPriority { get; set; }

        [Extracted(""Pk"", 1)]
        public string TaskLabel { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0,
            "Generated code for enum [Extracted] property with non-standard name must compile. " +
            $"Found {errors.Count} error(s):\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// Verifies that generated code compiles when an entity has multiple extracted properties
    /// of mixed types (enum + string) from the same computed source.
    /// </summary>
    [Fact]
    public void MixedTypeExtractedProperties_GeneratedCodeCompiles()
    {
        // Arrange: Enum at index 0, string at index 1, another enum at index 2
        var source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;

namespace TestNamespace
{
    public enum Region { UsEast, UsWest, EuWest, ApSoutheast }
    public enum Tier { Free, Basic, Pro, Enterprise }

    [DynamoDbTable(""Accounts"")]
    public partial class Account
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""AccountRegion"", ""AccountId"", ""AccountTier"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public Region AccountRegion { get; set; }

        [Extracted(""Pk"", 1)]
        public string AccountId { get; set; } = string.Empty;

        [Extracted(""Pk"", 2)]
        public Tier AccountTier { get; set; }
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0,
            "Generated code for mixed-type [Extracted] properties must compile. " +
            $"Found {errors.Count} error(s):\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    #region Helper Methods

    private static Compilation CreateCompilationWithGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            $"CompilationTest_{Guid.NewGuid():N}",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        return outputCompilation;
    }

    #endregion
}
