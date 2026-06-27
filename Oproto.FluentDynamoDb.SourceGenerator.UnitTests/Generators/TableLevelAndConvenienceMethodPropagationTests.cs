using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for table-level and convenience method propagation.
/// Verifies:
/// - Table-level typed overloads delegate to entity accessor
/// - Table-level KeyInputMode parameter pass-through
/// - GetAsync/DeleteAsync convenience methods get KeyInputMode when eligible
///
/// **Validates: Requirements 6.1, 6.2, 6.3, 7.1, 7.2**
/// </summary>
[Trait("Category", "Unit")]
public class TableLevelAndConvenienceMethodPropagationTests
{
    #region Table-Level Typed Overload Delegation (Req 6.1, 6.2)

    /// <summary>
    /// Verifies that table-level typed overloads are generated for an entity
    /// with a computed PK that qualifies for typed overloads.
    /// </summary>
    [Fact]
    public void TableLevel_ComputedPkEntity_GeneratesTypedOverloads()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events-table"", IsDefault = true)]
    public partial class Event
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        public int Month { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "EventsTableTable.g.cs");

        // Table-level typed overloads should exist
        tableCode.Should().Contain("Get(int year, int month, string sK)",
            "table-level typed Get overload should be generated");
        tableCode.Should().Contain("Delete(int year, int month, string sK)",
            "table-level typed Delete overload should be generated");
        tableCode.Should().Contain("Update(int year, int month, string sK)",
            "table-level typed Update overload should be generated");
        tableCode.Should().Contain("ConditionCheck(int year, int month, string sK)",
            "table-level typed ConditionCheck overload should be generated");
    }

    /// <summary>
    /// Verifies that table-level typed overloads delegate to entity accessor methods.
    /// </summary>
    [Fact]
    public void TableLevel_TypedOverloads_DelegateToEntityAccessor()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events-table"", IsDefault = true)]
    public partial class Event
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        public int Month { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "EventsTableTable.g.cs");

        // Typed overloads should delegate to entity accessor
        tableCode.Should().Contain("Events.Get(year, month, sK)",
            "typed Get should delegate to entity accessor");
        tableCode.Should().Contain("Events.Delete(year, month, sK)",
            "typed Delete should delegate to entity accessor");
        tableCode.Should().Contain("Events.Update(year, month, sK)",
            "typed Update should delegate to entity accessor");
        tableCode.Should().Contain("Events.ConditionCheck(year, month, sK)",
            "typed ConditionCheck should delegate to entity accessor");
    }

    #endregion

    #region Table-Level KeyInputMode Pass-Through (Req 6.1, 6.2, 6.3)

    /// <summary>
    /// Verifies that table-level Get overload includes KeyInputMode parameter
    /// when the entity has a string key with prefix and no typed overload.
    /// </summary>
    [Fact]
    public void TableLevel_StringKeyWithPrefix_GetIncludesKeyInputMode()
    {
        // Arrange - entity with string PK that has prefix but no computed key
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level Get should include KeyInputMode parameter
        tableCode.Should().Contain("Get(string pk, string sk, KeyInputMode mode = KeyInputMode.Default)",
            "table-level Get should include KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that table-level Delete overload includes KeyInputMode parameter
    /// when the entity has a string key with prefix and no typed overload.
    /// </summary>
    [Fact]
    public void TableLevel_StringKeyWithPrefix_DeleteIncludesKeyInputMode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level Delete should include KeyInputMode parameter
        tableCode.Should().Contain("Delete(string pk, string sk, KeyInputMode mode = KeyInputMode.Default)",
            "table-level Delete should include KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that table-level methods pass the mode parameter through
    /// to entity accessor unchanged.
    /// </summary>
    [Fact]
    public void TableLevel_KeyInputMode_PassesThroughToEntityAccessor()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level Get should pass mode to entity accessor
        tableCode.Should().Contain("Orders.Get(pk, sk, mode)",
            "table-level Get should pass mode parameter through to entity accessor");
    }

    /// <summary>
    /// Verifies that table-level Update includes KeyInputMode when eligible.
    /// </summary>
    [Fact]
    public void TableLevel_StringKeyWithPrefix_UpdateIncludesKeyInputMode()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level Update should include KeyInputMode parameter
        tableCode.Should().Contain("Update(string pk, string sk, KeyInputMode mode = KeyInputMode.Default, KeyCondition keyCondition = KeyCondition.None)",
            "table-level Update should include KeyInputMode parameter");
    }

    /// <summary>
    /// Verifies that table-level methods do NOT include KeyInputMode
    /// when a typed overload is generated (per Req 4 AC 2).
    /// </summary>
    [Fact]
    public void TableLevel_WithTypedOverload_NoKeyInputModeOnStandardOverloads()
    {
        // Arrange - entity with computed key that qualifies for typed overloads
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events-table"", IsDefault = true)]
    public partial class Event
    {
        [PartitionKey(Prefix = ""EVT"")]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        public int Month { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "EventsTableTable.g.cs");

        // Table-level section should NOT contain KeyInputMode since typed overloads exist
        // Extract table-level operations section (before accessor class definitions)
        var tableLevelSection = GetTableLevelSection(tableCode);
        tableLevelSection.Should().NotContain("KeyInputMode mode = KeyInputMode.Default",
            "table-level methods should not have KeyInputMode when typed overloads exist");
    }

    #endregion

    #region GetAsync/DeleteAsync Convenience Methods (Req 7.1, 7.2)

    /// <summary>
    /// Verifies that GetAsync convenience method includes KeyInputMode parameter
    /// when the entity has a string key with prefix and no typed overload.
    /// </summary>
    [Fact]
    public void ConvenienceMethod_GetAsync_IncludesKeyInputModeWhenEligible()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // GetAsync on entity accessor should include KeyInputMode
        tableCode.Should().Contain("GetAsync(string pk, string sk, KeyInputMode mode = KeyInputMode.Default",
            "GetAsync convenience method should include KeyInputMode parameter when eligible");
    }

    /// <summary>
    /// Verifies that DeleteAsync convenience method includes KeyInputMode parameter
    /// when the entity has a string key with prefix and no typed overload.
    /// </summary>
    [Fact]
    public void ConvenienceMethod_DeleteAsync_IncludesKeyInputModeWhenEligible()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // DeleteAsync on entity accessor should include KeyInputMode
        tableCode.Should().Contain("DeleteAsync(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default",
            "DeleteAsync convenience method should include KeyInputMode parameter when eligible");
    }

    /// <summary>
    /// Verifies that table-level GetAsync convenience method includes KeyInputMode
    /// and passes it through to entity accessor.
    /// </summary>
    [Fact]
    public void TableLevel_GetAsync_IncludesKeyInputModeAndDelegates()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level GetAsync should exist with KeyInputMode and delegate to entity accessor
        tableCode.Should().Contain("GetAsync(string pk, string sk, KeyInputMode mode = KeyInputMode.Default",
            "table-level GetAsync should include KeyInputMode parameter");
        tableCode.Should().Contain("Orders.GetAsync(pk, sk, mode",
            "table-level GetAsync should delegate to entity accessor with mode");
    }

    /// <summary>
    /// Verifies that table-level DeleteAsync convenience method includes KeyInputMode
    /// and passes it through to entity accessor.
    /// </summary>
    [Fact]
    public void TableLevel_DeleteAsync_IncludesKeyInputModeAndDelegates()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "OrdersTableTable.g.cs");

        // Table-level DeleteAsync should exist with KeyInputMode and delegate to entity accessor
        tableCode.Should().Contain("DeleteAsync(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default",
            "table-level DeleteAsync should include KeyInputMode parameter");
        tableCode.Should().Contain("Orders.DeleteAsync(pk, sk, keyCondition, mode",
            "table-level DeleteAsync should delegate to entity accessor with mode");
    }

    /// <summary>
    /// Verifies that GetAsync does NOT include KeyInputMode when entity has typed overloads
    /// (because typed overloads handle raw-value access and no KeyInputMode is needed).
    /// </summary>
    [Fact]
    public void ConvenienceMethod_GetAsync_NoKeyInputModeWithTypedOverloads()
    {
        // Arrange - entity with computed key that qualifies for typed overloads
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events-table"", IsDefault = true)]
    public partial class Event
    {
        [PartitionKey(Prefix = ""EVT"")]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        public int Month { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "EventsTableTable.g.cs");

        // GetAsync should NOT contain KeyInputMode when typed overloads exist
        tableCode.Should().NotContain("GetAsync(string pk, string sk, KeyInputMode",
            "GetAsync should not include KeyInputMode when typed overloads exist");
    }

    /// <summary>
    /// Verifies a PK-only entity table-level GetAsync includes KeyInputMode when prefix exists.
    /// </summary>
    [Fact]
    public void TableLevel_PkOnly_GetAsync_IncludesKeyInputModeWhenEligible()
    {
        // Arrange - entity with PK only, with prefix
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"", IsDefault = true)]
    public partial class User
    {
        [PartitionKey(Prefix = ""USER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "UsersTableTable.g.cs");

        // Table-level GetAsync should include KeyInputMode for PK-only entity with prefix
        tableCode.Should().Contain("GetAsync(string pk, KeyInputMode mode = KeyInputMode.Default",
            "table-level GetAsync for PK-only entity should include KeyInputMode when prefix exists");
    }

    /// <summary>
    /// Verifies a PK-only entity table-level DeleteAsync includes KeyInputMode when prefix exists.
    /// </summary>
    [Fact]
    public void TableLevel_PkOnly_DeleteAsync_IncludesKeyInputModeWhenEligible()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"", IsDefault = true)]
    public partial class User
    {
        [PartitionKey(Prefix = ""USER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableCode = GetTableCode(result, "UsersTableTable.g.cs");

        // Table-level DeleteAsync should include KeyInputMode for PK-only entity with prefix
        tableCode.Should().Contain("DeleteAsync(string pk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default",
            "table-level DeleteAsync for PK-only entity should include KeyInputMode when prefix exists");
    }

    #endregion

    #region Helpers

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
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

    private static string GetTableCode(GeneratorTestResult result, string tableFileName)
    {
        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains(tableFileName))
            .ToArray();

        tableFiles.Should().HaveCount(1,
            $"expected exactly one generated file matching '{tableFileName}'");

        return tableFiles[0].SourceText.ToString();
    }

    private static string GetTableLevelSection(string tableCode)
    {
        // Extract the section before the first accessor class definition
        var accessorStart = tableCode.IndexOf("public class ", tableCode.IndexOf("partial class ") + 1);
        if (accessorStart < 0)
            accessorStart = tableCode.Length;

        return tableCode.Substring(0, accessorStart);
    }

    #endregion
}
