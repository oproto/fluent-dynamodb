using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying end-to-end source generation for entities whose
/// discriminator pattern ends with a trailing literal (bare separator) after the
/// last wildcard — e.g., "EMPLOYEE#*#" derived from [Computed("EmployeeId", Format = "EMPLOYEE#{0}#")].
///
/// This is the exact bug scenario from the trailing-bare-separator fix:
/// the generated MatchesEntity code must accept values where the separator IS
/// the terminal character (e.g., "EMPLOYEE#abc123#").
///
/// Scenario 1: Single trailing-literal entity (Employee) alongside a non-trailing entity (EmployeeRecord)
/// Scenario 2: Two trailing-literal entities with exclusion interaction (Order vs OrderItem)
/// Scenario 3: Generated code string verification for Employee
/// Scenario 4: Multi-character trailing literal (TenantAccess vs TenantInternal) — verifies
///             that multi-character trailing literals like "#EXTERNAL_ACCESS" use the Contains()
///             branch, not the bare-separator IndexOf branch, confirming that code path is unaffected
///
/// Validates: Requirements 2.1, 2.2, 2.3, 2.4
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "computed-trailing-literal-matches-entity-fix")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class TrailingLiteralPatternIntegrationTests
{
    #region Entity Source Definitions

    private const string EmployeeEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""employees"", IsDefault = true)]
    public partial class Employee
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""EmployeeId"", Format = ""EMPLOYEE#{0}#"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string EmployeeId { get; set; } = string.Empty;
    }

    [DynamoDbTable(""employees"")]
    public partial class EmployeeRecord
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""RECORD"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    private const string OrderEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""OrderId"", Format = ""ORDER#{0}#"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string OrderId { get; set; } = string.Empty;
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderItem
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""OrderId"", ""ItemId"", Format = ""ORDER#{0}#ITEM#{1}#"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string OrderId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public string ItemId { get; set; } = string.Empty;
    }
}";

    private const string TenantEntitySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""tenants"", IsDefault = true)]
    public partial class TenantAccess
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""TenantId"", Format = ""TENANT#{0}#EXTERNAL_ACCESS"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string TenantId { get; set; } = string.Empty;
    }

    [DynamoDbTable(""tenants"")]
    public partial class TenantInternal
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""TenantId"", Format = ""TENANT#{0}#INTERNAL"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string TenantId { get; set; } = string.Empty;
    }
}";

    #endregion

    #region Scenario 1: Employee + EmployeeRecord (trailing literal vs simple prefix)

    [Fact]
    public void Employee_MatchesEntity_ReturnsTrue_ForTrailingLiteralSortKey()
    {
        // Arrange — this is THE bug fix case: separator IS the terminal character
        var (employeeType, _) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "EMPLOYEE#abc123#");

        // Act
        var result = InvokeMatchesEntity(employeeType, item);

        // Assert
        result.Should().BeTrue(
            "Employee should match SK 'EMPLOYEE#abc123#' — the trailing '#' is the expected terminal literal");
    }

    [Fact]
    public void Employee_MatchesEntity_ReturnsTrue_ForMinimalOneCharWildcard()
    {
        // Arrange — minimal 1-char wildcard between separators
        var (employeeType, _) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "EMPLOYEE#x#");

        // Act
        var result = InvokeMatchesEntity(employeeType, item);

        // Assert
        result.Should().BeTrue(
            "Employee should match SK 'EMPLOYEE#x#' — minimal 1-char wildcard is valid");
    }

    [Fact]
    public void Employee_MatchesEntity_ReturnsFalse_ForUnrelatedEntity()
    {
        // Arrange
        var (employeeType, _) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "RECORD#abc");

        // Act
        var result = InvokeMatchesEntity(employeeType, item);

        // Assert
        result.Should().BeFalse(
            "Employee should NOT match SK 'RECORD#abc' — different prefix entirely");
    }

    [Fact]
    public void Employee_MatchesEntity_ReturnsFalse_ForEmptyWildcard()
    {
        // Arrange — no content between separators (empty wildcard)
        var (employeeType, _) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "EMPLOYEE##");

        // Act
        var result = InvokeMatchesEntity(employeeType, item);

        // Assert
        result.Should().BeFalse(
            "Employee should NOT match SK 'EMPLOYEE##' — empty wildcard (no content between separators)");
    }

    [Fact]
    public void EmployeeRecord_MatchesEntity_ReturnsTrue_ForRecordSortKey()
    {
        // Arrange
        var (_, employeeRecordType) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "RECORD#abc");

        // Act
        var result = InvokeMatchesEntity(employeeRecordType, item);

        // Assert
        result.Should().BeTrue(
            "EmployeeRecord should match SK 'RECORD#abc'");
    }

    [Fact]
    public void EmployeeRecord_MatchesEntity_ReturnsFalse_ForEmployeeSortKey()
    {
        // Arrange
        var (_, employeeRecordType) = CompileAndLoadEmployeeEntities();
        var item = CreateItem("TENANT#t1", "EMPLOYEE#abc123#");

        // Act
        var result = InvokeMatchesEntity(employeeRecordType, item);

        // Assert
        result.Should().BeFalse(
            "EmployeeRecord should NOT match SK 'EMPLOYEE#abc123#'");
    }

    #endregion

    #region Scenario 2: Order + OrderItem (two trailing literals with exclusion)

    [Fact]
    public void OrderItem_MatchesEntity_ReturnsTrue_ForOrderItemSortKey()
    {
        // Arrange
        var (_, orderItemType) = CompileAndLoadOrderEntities();
        var item = CreateItem("CUSTOMER#c1", "ORDER#abc#ITEM#1#");

        // Act
        var result = InvokeMatchesEntity(orderItemType, item);

        // Assert
        result.Should().BeTrue(
            "OrderItem should match SK 'ORDER#abc#ITEM#1#'");
    }

    [Fact]
    public void OrderItem_MatchesEntity_ReturnsFalse_ForPlainOrderSortKey()
    {
        // Arrange
        var (_, orderItemType) = CompileAndLoadOrderEntities();
        var item = CreateItem("CUSTOMER#c1", "ORDER#abc#");

        // Act
        var result = InvokeMatchesEntity(orderItemType, item);

        // Assert
        result.Should().BeFalse(
            "OrderItem should NOT match SK 'ORDER#abc#' — missing ITEM segment");
    }

    [Fact]
    public void Order_MatchesEntity_ReturnsTrue_ForPlainOrderSortKey()
    {
        // Arrange
        var (orderType, _) = CompileAndLoadOrderEntities();
        var item = CreateItem("CUSTOMER#c1", "ORDER#abc#");

        // Act
        var result = InvokeMatchesEntity(orderType, item);

        // Assert
        result.Should().BeTrue(
            "Order should match SK 'ORDER#abc#' — trailing literal fix case");
    }

    [Fact]
    public void Order_MatchesEntity_ReturnsFalse_ForOrderItemSortKey()
    {
        // Arrange — exclusion guard should reject the more-specific OrderItem pattern
        var (orderType, _) = CompileAndLoadOrderEntities();
        var item = CreateItem("CUSTOMER#c1", "ORDER#abc#ITEM#1#");

        // Act
        var result = InvokeMatchesEntity(orderType, item);

        // Assert
        result.Should().BeFalse(
            "Order should NOT match SK 'ORDER#abc#ITEM#1#' — exclusion guard for OrderItem should fire");
    }

    #endregion

    #region Scenario 3: Generated code string verification

    [Fact]
    public void GeneratedEmployeeCode_ContainsCorrectIndexOfCheck()
    {
        // Arrange & Act — run the source generator and inspect the generated code string
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EmployeeEntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var employeeTree = outputCompilation.SyntaxTrees
            .Skip(1) // skip original source
            .FirstOrDefault(t => t.FilePath.Contains("Employee.g.cs") && !t.FilePath.Contains("EmployeeRecord"));

        employeeTree.Should().NotBeNull("Employee.g.cs should be generated");
        var employeeCode = employeeTree!.GetText().ToString();

        // Assert — the generated code should use IndexOf("#", offset) >= 0
        // without the buggy < Length - 1 constraint
        employeeCode.Should().Contain("IndexOf(\"#\"",
            "Employee MatchesEntity should contain an IndexOf(\"#\", ...) check for the trailing separator");
    }

    [Fact]
    public void GeneratedEmployeeCode_DoesNotContainLengthMinusOneConstraint()
    {
        // Arrange & Act
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EmployeeEntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var employeeTree = outputCompilation.SyntaxTrees
            .Skip(1)
            .FirstOrDefault(t => t.FilePath.Contains("Employee.g.cs") && !t.FilePath.Contains("EmployeeRecord"));

        employeeTree.Should().NotBeNull("Employee.g.cs should be generated");
        var employeeCode = employeeTree!.GetText().ToString();

        // Assert — the buggy pattern should NOT be present for trailing bare-separator
        employeeCode.Should().NotContain("< discriminatorValue.S.Length - 1",
            "Employee's trailing bare-separator check should NOT have the < Length - 1 constraint (this was the bug)");
    }

    #endregion

    #region Scenario 4: Multi-character trailing literal (Contains branch)

    [Fact]
    public void TenantAccess_MatchesEntity_ReturnsTrue_ForExternalAccessSortKey()
    {
        // Arrange
        var (tenantAccessType, _) = CompileAndLoadTenantEntities();
        var item = CreateItem("pk1", "TENANT#abc#EXTERNAL_ACCESS");

        // Act
        var result = InvokeMatchesEntity(tenantAccessType, item);

        // Assert
        result.Should().BeTrue(
            "TenantAccess should match SK 'TENANT#abc#EXTERNAL_ACCESS' — multi-character trailing literal");
    }

    [Fact]
    public void TenantAccess_MatchesEntity_ReturnsFalse_ForInternalSortKey()
    {
        // Arrange — exclusion guard should reject TenantInternal's pattern
        var (tenantAccessType, _) = CompileAndLoadTenantEntities();
        var item = CreateItem("pk1", "TENANT#abc#INTERNAL");

        // Act
        var result = InvokeMatchesEntity(tenantAccessType, item);

        // Assert
        result.Should().BeFalse(
            "TenantAccess should NOT match SK 'TENANT#abc#INTERNAL' — exclusion guard for TenantInternal should fire");
    }

    [Fact]
    public void TenantAccess_MatchesEntity_ReturnsFalse_ForUnrelatedSortKey()
    {
        // Arrange
        var (tenantAccessType, _) = CompileAndLoadTenantEntities();
        var item = CreateItem("pk1", "OTHER#abc");

        // Act
        var result = InvokeMatchesEntity(tenantAccessType, item);

        // Assert
        result.Should().BeFalse(
            "TenantAccess should NOT match SK 'OTHER#abc' — different prefix entirely");
    }

    [Fact]
    public void TenantInternal_MatchesEntity_ReturnsTrue_ForInternalSortKey()
    {
        // Arrange
        var (_, tenantInternalType) = CompileAndLoadTenantEntities();
        var item = CreateItem("pk1", "TENANT#abc#INTERNAL");

        // Act
        var result = InvokeMatchesEntity(tenantInternalType, item);

        // Assert
        result.Should().BeTrue(
            "TenantInternal should match SK 'TENANT#abc#INTERNAL' — multi-character trailing literal");
    }

    [Fact]
    public void TenantInternal_MatchesEntity_ReturnsFalse_ForExternalAccessSortKey()
    {
        // Arrange — exclusion guard should reject TenantAccess's pattern
        var (_, tenantInternalType) = CompileAndLoadTenantEntities();
        var item = CreateItem("pk1", "TENANT#abc#EXTERNAL_ACCESS");

        // Act
        var result = InvokeMatchesEntity(tenantInternalType, item);

        // Assert
        result.Should().BeFalse(
            "TenantInternal should NOT match SK 'TENANT#abc#EXTERNAL_ACCESS' — exclusion guard for TenantAccess should fire");
    }

    [Fact]
    public void GeneratedTenantAccessCode_UsesContainsNotIndexOf()
    {
        // Arrange & Act — run the source generator and inspect the generated code string
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(TenantEntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var tenantAccessTree = outputCompilation.SyntaxTrees
            .Skip(1) // skip original source
            .FirstOrDefault(t => t.FilePath.Contains("TenantAccess.g.cs") && !t.FilePath.Contains("TenantInternal"));

        tenantAccessTree.Should().NotBeNull("TenantAccess.g.cs should be generated");
        var tenantAccessCode = tenantAccessTree!.GetText().ToString();

        // Assert — multi-character trailing literal should use Contains(), not IndexOf()
        tenantAccessCode.Should().Contain("Contains(\"#EXTERNAL_ACCESS\")",
            "TenantAccess should use Contains(\"#EXTERNAL_ACCESS\") for the multi-character trailing literal — " +
            "this confirms it takes the Contains branch, not the bare-separator IndexOf branch");

        tenantAccessCode.Should().NotContain("IndexOf(\"#EXTERNAL_ACCESS\"",
            "TenantAccess should NOT use IndexOf for the multi-character trailing literal '#EXTERNAL_ACCESS'");
    }

    #endregion

    #region Helper Methods

    private static (Type employeeType, Type employeeRecordType) CompileAndLoadEmployeeEntities()
    {
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            EmployeeEntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var employeeType = compilationResult.Assembly.GetType("TestNamespace.Employee")
            ?? throw new InvalidOperationException("Employee type not found in compiled assembly");
        var employeeRecordType = compilationResult.Assembly.GetType("TestNamespace.EmployeeRecord")
            ?? throw new InvalidOperationException("EmployeeRecord type not found in compiled assembly");

        return (employeeType, employeeRecordType);
    }

    private static (Type orderType, Type orderItemType) CompileAndLoadOrderEntities()
    {
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            OrderEntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var orderType = compilationResult.Assembly.GetType("TestNamespace.Order")
            ?? throw new InvalidOperationException("Order type not found in compiled assembly");
        var orderItemType = compilationResult.Assembly.GetType("TestNamespace.OrderItem")
            ?? throw new InvalidOperationException("OrderItem type not found in compiled assembly");

        return (orderType, orderItemType);
    }

    private static (Type tenantAccessType, Type tenantInternalType) CompileAndLoadTenantEntities()
    {
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            TenantEntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var tenantAccessType = compilationResult.Assembly.GetType("TestNamespace.TenantAccess")
            ?? throw new InvalidOperationException("TenantAccess type not found in compiled assembly");
        var tenantInternalType = compilationResult.Assembly.GetType("TestNamespace.TenantInternal")
            ?? throw new InvalidOperationException("TenantInternal type not found in compiled assembly");

        return (tenantAccessType, tenantInternalType);
    }

    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'. " +
                "Ensure the source generator produced the expected code.");
        }

        var result = method.Invoke(null, new object[] { item });
        return (bool)result!;
    }

    #endregion
}
