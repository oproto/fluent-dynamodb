using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Generated code compilation tests for bare <c>[RelatedEntity]</c> with various collection types.
/// These tests verify that the full source generator pipeline produces code that compiles
/// without errors when bare <c>[RelatedEntity]</c> is used on different property type forms.
///
/// **Validates: Requirement 9.2**
/// </summary>
[Trait("Category", "Integration")]
public class BareRelatedEntityCompilationTests
{
    /// <summary>
    /// Verifies that bare <c>[RelatedEntity]</c> on a <c>List&lt;T&gt;</c> property compiles
    /// without errors when the child entity has a prefixed sort key on the same table.
    /// This is the most common collection type for related entities.
    ///
    /// **Validates: Requirement 9.2**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_ListOfT_CompilesWithoutErrors()
    {
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }

        [RelatedEntity]
        public List<OrderLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""OrderId"", ""LineNumber"", Format = ""ORDER#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string OrderId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "Bare [RelatedEntity] on List<OrderLine> should compile without errors. " +
            $"Errors: {string.Join(", ", errors.Select(e => $"{e.Id}: {e.GetMessage()}"))}");
    }

    /// <summary>
    /// Verifies that bare <c>[RelatedEntity]</c> on a <c>List&lt;T&gt;</c> property compiles
    /// without errors when the child entity uses a simple prefixed sort key (no <c>[Computed]</c>).
    /// This tests pattern inference from <c>DerivedDiscriminatorPattern</c> when the sort key
    /// uses only a prefix, producing a pattern like <c>TASK#*</c>.
    ///
    /// Note: <c>IList&lt;T&gt;</c> is not used here because the MapperGenerator produces
    /// <c>List&lt;object&gt;</c> which cannot be assigned to <c>IList&lt;T&gt;</c> — a
    /// pre-existing source generator limitation unrelated to bare <c>[RelatedEntity]</c>.
    ///
    /// **Validates: Requirement 9.2**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_ListOfT_WithPrefixedSortKey_CompilesWithoutErrors()
    {
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""projects"", IsDefault = true)]
    public partial class Project
    {
        [PartitionKey(Prefix = ""ORG"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PROJECT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [RelatedEntity]
        public List<ProjectTask> Tasks { get; set; } = new();
    }

    [DynamoDbTable(""projects"")]
    public partial class ProjectTask
    {
        [PartitionKey(Prefix = ""ORG"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""TASK"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""title"")]
        public string Title { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "Bare [RelatedEntity] on List<ProjectTask> with prefixed sort key should compile without errors. " +
            $"Errors: {string.Join(", ", errors.Select(e => $"{e.Id}: {e.GetMessage()}"))}");
    }

    /// <summary>
    /// Verifies that bare <c>[RelatedEntity]</c> on a nullable non-collection property
    /// (<c>ChildEntity?</c>) compiles without errors. The source generator should unwrap
    /// the nullable annotation and use the underlying type as the child entity type.
    ///
    /// **Validates: Requirement 9.2**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_NullableT_CompilesWithoutErrors()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""accounts"", IsDefault = true)]
    public partial class Account
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ACCOUNT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [RelatedEntity]
        public AccountSettings? Settings { get; set; }
    }

    [DynamoDbTable(""accounts"")]
    public partial class AccountSettings
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""SETTINGS"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""theme"")]
        public string Theme { get; set; } = string.Empty;

        [DynamoDbAttribute(""timezone"")]
        public string Timezone { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty(
            "Bare [RelatedEntity] on AccountSettings? (nullable non-collection) should compile without errors. " +
            $"Errors: {string.Join(", ", errors.Select(e => $"{e.Id}: {e.GetMessage()}"))}");
    }

    #region Helper Methods

    private static Compilation CreateCompilationWithGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            $"BareRelatedEntityCompilationTest_{Guid.NewGuid():N}",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        return outputCompilation;
    }

    #endregion
}
