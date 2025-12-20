using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for FluentResults table-level method generation.
/// Verifies that table-level convenience methods are generated correctly based on
/// UseFluentResults and HideGeneratedAsyncMethods settings.
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 3.3, 3.4
/// </summary>
[Trait("Category", "Unit")]
public class FluentResultsTableLevelMethodTests
{
    /// <summary>
    /// Task 4.1: Verify generated table class does not contain GetAsync or DeleteAsync methods
    /// when UseFluentResults is applied with default HideGeneratedAsyncMethods = true.
    /// Requirements: 1.1, 1.2, 2.1, 2.2
    /// </summary>
    [Fact]
    public void UseFluentResults_WithDefaultSettings_DoesNotGenerateTraditionalAsyncMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [UseFluentResults]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Extract table-level operations section (before accessor classes)
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        tableLevelStart.Should().BeGreaterThan(-1, "should have table-level operations section");
        firstAccessorStart.Should().BeGreaterThan(tableLevelStart, "accessor should come after table-level operations");
        
        var tableLevelSection = tableCode.Substring(tableLevelStart, firstAccessorStart - tableLevelStart);
        
        // Should NOT have traditional async methods at table level
        tableLevelSection.Should().NotContain("Task<Order?> GetAsync(",
            "should not generate table-level GetAsync when UseFluentResults with default settings");
        tableLevelSection.Should().NotContain("Task DeleteAsync(",
            "should not generate table-level DeleteAsync when UseFluentResults with default settings");
        
        // Should have Result-returning methods at table level
        tableLevelSection.Should().Contain("GetAsyncResult(",
            "should generate table-level GetAsyncResult when UseFluentResults is enabled");
        tableLevelSection.Should().Contain("DeleteAsyncResult(",
            "should generate table-level DeleteAsyncResult when UseFluentResults is enabled");
    }

    /// <summary>
    /// Task 4.1: Verify generated table class contains GetAsyncResult and DeleteAsyncResult methods
    /// when UseFluentResults is applied.
    /// Requirements: 2.1, 2.2
    /// </summary>
    [Fact]
    public void UseFluentResults_GeneratesResultReturningMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [UseFluentResults]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have Result-returning methods
        tableCode.Should().Contain("public System.Threading.Tasks.Task<global::FluentResults.Result<Order?>> GetAsyncResult(",
            "should generate GetAsyncResult with correct return type");
        tableCode.Should().Contain("public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult(",
            "should generate DeleteAsyncResult with correct return type");
        tableCode.Should().Contain("public System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult(",
            "should generate PutAsyncResult with correct return type");
        tableCode.Should().Contain("public System.Threading.Tasks.Task<global::FluentResults.Result<System.Collections.Generic.List<Order>>> QueryAsyncResult(",
            "should generate QueryAsyncResult with correct return type");
    }

    /// <summary>
    /// Task 4.2: Verify generated table class contains both traditional and Result-returning methods
    /// when UseFluentResults is applied with HideGeneratedAsyncMethods = false.
    /// Requirements: 1.3, 1.4, 3.4
    /// </summary>
    [Fact]
    public void UseFluentResults_WithHideGeneratedAsyncMethodsFalse_GeneratesBothMethodTypes()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [UseFluentResults(HideGeneratedAsyncMethods = false)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Extract table-level operations section
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        tableLevelStart.Should().BeGreaterThan(-1, "should have table-level operations section");
        firstAccessorStart.Should().BeGreaterThan(tableLevelStart, "accessor should come after table-level operations");
        
        var tableLevelSection = tableCode.Substring(tableLevelStart, firstAccessorStart - tableLevelStart);
        
        // Should have BOTH traditional async methods
        tableLevelSection.Should().Contain("Task<Order?> GetAsync(",
            "should generate table-level GetAsync when HideGeneratedAsyncMethods = false");
        tableLevelSection.Should().Contain("Task DeleteAsync(",
            "should generate table-level DeleteAsync when HideGeneratedAsyncMethods = false");
        
        // AND Result-returning methods
        tableLevelSection.Should().Contain("GetAsyncResult(",
            "should generate table-level GetAsyncResult when UseFluentResults is enabled");
        tableLevelSection.Should().Contain("DeleteAsyncResult(",
            "should generate table-level DeleteAsyncResult when UseFluentResults is enabled");
    }

    /// <summary>
    /// Task 4.3: Verify generated table class contains traditional async methods
    /// and does not contain Result-returning methods when UseFluentResults is not applied.
    /// Requirements: 1.5
    /// </summary>
    [Fact]
    public void WithoutUseFluentResults_GeneratesTraditionalAsyncMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Extract table-level operations section
        var tableLevelStart = tableCode.IndexOf("// Table-level operations");
        var firstAccessorStart = tableCode.IndexOf("public class OrderAccessor");
        
        tableLevelStart.Should().BeGreaterThan(-1, "should have table-level operations section");
        firstAccessorStart.Should().BeGreaterThan(tableLevelStart, "accessor should come after table-level operations");
        
        var tableLevelSection = tableCode.Substring(tableLevelStart, firstAccessorStart - tableLevelStart);
        
        // Should have traditional async methods
        tableLevelSection.Should().Contain("Task<Order?> GetAsync(",
            "should generate table-level GetAsync when UseFluentResults is not applied");
        tableLevelSection.Should().Contain("Task DeleteAsync(",
            "should generate table-level DeleteAsync when UseFluentResults is not applied");
        
        // Should NOT have Result-returning methods
        tableLevelSection.Should().NotContain("GetAsyncResult(",
            "should not generate table-level GetAsyncResult when UseFluentResults is not applied");
        tableLevelSection.Should().NotContain("DeleteAsyncResult(",
            "should not generate table-level DeleteAsyncResult when UseFluentResults is not applied");
    }

    /// <summary>
    /// Task 4.3: Verify generated table class does not contain Result-returning methods
    /// when UseFluentResults is not applied.
    /// Requirements: 1.5
    /// </summary>
    [Fact]
    public void WithoutUseFluentResults_DoesNotGenerateResultReturningMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should NOT have any Result-returning methods anywhere in the table class
        tableCode.Should().NotContain("GetAsyncResult(",
            "should not generate GetAsyncResult when UseFluentResults is not applied");
        tableCode.Should().NotContain("DeleteAsyncResult(",
            "should not generate DeleteAsyncResult when UseFluentResults is not applied");
        tableCode.Should().NotContain("PutAsyncResult(",
            "should not generate PutAsyncResult when UseFluentResults is not applied");
        tableCode.Should().NotContain("QueryAsyncResult(",
            "should not generate QueryAsyncResult when UseFluentResults is not applied");
    }

    /// <summary>
    /// Verify that table-level Result-returning methods delegate to accessor methods.
    /// Requirements: 2.1, 2.2, 2.3, 2.4
    /// </summary>
    [Fact]
    public void UseFluentResults_ResultReturningMethods_DelegateToAccessor()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [UseFluentResults]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Result-returning methods should delegate to accessor
        // Note: The generated code uses pk, cancellationToken parameters
        tableCode.Should().Contain("Orders.GetAsyncResult(pk, cancellationToken)",
            "table-level GetAsyncResult should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.DeleteAsyncResult(pk, cancellationToken)",
            "table-level DeleteAsyncResult should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.PutAsyncResult(entity, cancellationToken)",
            "table-level PutAsyncResult should delegate to Orders accessor");
        tableCode.Should().Contain("Orders.QueryAsyncResult(keyCondition, cancellationToken)",
            "table-level QueryAsyncResult should delegate to Orders accessor");
    }

    /// <summary>
    /// Verify that composite key entities generate correct Result-returning method signatures.
    /// Requirements: 2.1, 2.2
    /// </summary>
    [Fact]
    public void UseFluentResults_WithCompositeKey_GeneratesCorrectMethodSignatures()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""app-table"", IsDefault = true)]
    [UseFluentResults]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""app-table"")]
    public partial class OrderLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("AppTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have Result-returning methods with composite key parameters
        // Note: The generated code includes cancellationToken parameter
        tableCode.Should().Contain("GetAsyncResult(string pk, string sk,",
            "should generate GetAsyncResult with composite key parameters");
        tableCode.Should().Contain("DeleteAsyncResult(string pk, string sk,",
            "should generate DeleteAsyncResult with composite key parameters");
        
        // Should delegate with both parameters and cancellationToken
        tableCode.Should().Contain("Orders.GetAsyncResult(pk, sk, cancellationToken)",
            "table-level GetAsyncResult should delegate with both key parameters");
        tableCode.Should().Contain("Orders.DeleteAsyncResult(pk, sk, cancellationToken)",
            "table-level DeleteAsyncResult should delegate with both key parameters");
    }

    /// <summary>
    /// Verify that single entity tables also generate Result-returning methods correctly.
    /// Requirements: 2.1, 2.2, 2.3, 2.4
    /// </summary>
    [Fact]
    public void UseFluentResults_SingleEntity_GeneratesResultReturningMethods()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    [UseFluentResults]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);
        
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();
        
        tableFiles.Should().HaveCount(1);
        
        var tableCode = tableFiles[0].SourceText.ToString();
        
        // Should have Result-returning methods for single entity table
        tableCode.Should().Contain("GetAsyncResult(",
            "should generate GetAsyncResult for single entity table");
        tableCode.Should().Contain("DeleteAsyncResult(",
            "should generate DeleteAsyncResult for single entity table");
        tableCode.Should().Contain("PutAsyncResult(",
            "should generate PutAsyncResult for single entity table");
        tableCode.Should().Contain("QueryAsyncResult(",
            "should generate QueryAsyncResult for single entity table");
        
        // Should NOT have traditional async methods
        tableCode.Should().NotContain("Task<Order?> GetAsync(",
            "should not generate GetAsync for single entity table with UseFluentResults");
        tableCode.Should().NotContain("Task DeleteAsync(",
            "should not generate DeleteAsync for single entity table with UseFluentResults");
    }

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source)
            },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }
}
