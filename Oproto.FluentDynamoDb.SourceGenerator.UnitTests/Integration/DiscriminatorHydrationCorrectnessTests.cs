using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying hydration correctness for valid discriminator hierarchies.
/// Tests compile entities with overlapping discriminator patterns and verify MatchesEntity
/// produces mutually exclusive results across multiple entity types.
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class DiscriminatorHydrationCorrectnessTests
{
    #region Entity Source Templates

    private const string FourEntityServiceAccountSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""accounts"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SVCACCT#*"")]
    public partial class ServiceAccount
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""accounts"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SVCACCT#*#ROLE#*"")]
    public partial class ServiceAccountRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""accounts"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""accounts"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""USER#*#ROLE#*"")]
    public partial class UserRole
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    private const string ThreeLevelInvoiceHierarchySource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*"")]
    public partial class Invoice
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*"")]
    public partial class InvoiceLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*#ADJ#*"")]
    public partial class InvoiceLineAdjustment
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

    #endregion

    #region Test 1: Four Entity Service Account Table Mutual Exclusivity

    [Fact]
    public void FourEntityServiceAccountTable_MutualExclusivity()
    {
        // Arrange
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            FourEntityServiceAccountSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var serviceAccountType = compilationResult.Assembly.GetType("TestNamespace.ServiceAccount")
            ?? throw new InvalidOperationException("ServiceAccount type not found");
        var serviceAccountRoleType = compilationResult.Assembly.GetType("TestNamespace.ServiceAccountRole")
            ?? throw new InvalidOperationException("ServiceAccountRole type not found");
        var userType = compilationResult.Assembly.GetType("TestNamespace.User")
            ?? throw new InvalidOperationException("User type not found");
        var userRoleType = compilationResult.Assembly.GetType("TestNamespace.UserRole")
            ?? throw new InvalidOperationException("UserRole type not found");

        var entityTypes = new[] { serviceAccountType, serviceAccountRoleType, userType, userRoleType };

        var testCases = new[]
        {
            ("SVCACCT#SA001", "ServiceAccount key"),
            ("SVCACCT#SA001#ROLE#admin", "ServiceAccountRole key"),
            ("USER#U001", "User key"),
            ("USER#U001#ROLE#editor", "UserRole key"),
        };

        // Act & Assert
        foreach (var (sk, description) in testCases)
        {
            var item = CreateItem("PK#1", sk);
            var matchCount = 0;
            var matchingEntities = new List<string>();

            foreach (var entityType in entityTypes)
            {
                if (InvokeMatchesEntity(entityType, item))
                {
                    matchCount++;
                    matchingEntities.Add(entityType.Name);
                }
            }

            matchCount.Should().Be(1,
                $"exactly one entity should match '{sk}' ({description}), " +
                $"but matched: [{string.Join(", ", matchingEntities)}]");
        }
    }

    #endregion

    #region Test 2: Invoice Hierarchy Three Levels Mutual Exclusivity

    [Fact]
    public void InvoiceHierarchy_ThreeLevels_MutualExclusivity()
    {
        // Arrange
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            ThreeLevelInvoiceHierarchySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var invoiceType = compilationResult.Assembly.GetType("TestNamespace.Invoice")
            ?? throw new InvalidOperationException("Invoice type not found");
        var invoiceLineType = compilationResult.Assembly.GetType("TestNamespace.InvoiceLine")
            ?? throw new InvalidOperationException("InvoiceLine type not found");
        var invoiceLineAdjustmentType = compilationResult.Assembly.GetType("TestNamespace.InvoiceLineAdjustment")
            ?? throw new InvalidOperationException("InvoiceLineAdjustment type not found");

        var entityTypes = new[] { invoiceType, invoiceLineType, invoiceLineAdjustmentType };

        var testCases = new[]
        {
            ("INVOICE#001", "Invoice key"),
            ("INVOICE#001#LINE#1", "InvoiceLine key"),
            ("INVOICE#001#LINE#1#ADJ#A", "InvoiceLineAdjustment key"),
        };

        // Act & Assert — each test key should match exactly one entity
        foreach (var (sk, description) in testCases)
        {
            var item = CreateItem("PK#1", sk);
            var matchCount = 0;
            var matchingEntities = new List<string>();

            foreach (var entityType in entityTypes)
            {
                if (InvokeMatchesEntity(entityType, item))
                {
                    matchCount++;
                    matchingEntities.Add(entityType.Name);
                }
            }

            matchCount.Should().Be(1,
                $"exactly one entity should match '{sk}' ({description}), " +
                $"but matched: [{string.Join(", ", matchingEntities)}]");
        }

        // Also test unrelated key — all should return false
        var unrelatedItem = CreateItem("PK#1", "ORDER#123");
        foreach (var entityType in entityTypes)
        {
            InvokeMatchesEntity(entityType, unrelatedItem).Should().BeFalse(
                $"{entityType.Name} should not match unrelated key 'ORDER#123'");
        }
    }

    #endregion

    #region Test 3: Missing Discriminator Property Returns False

    [Fact]
    public void MatchesEntity_MissingDiscriminatorProperty_ReturnsFalse()
    {
        // Arrange — use Invoice/InvoiceLine hierarchy
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            ThreeLevelInvoiceHierarchySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var invoiceType = compilationResult.Assembly.GetType("TestNamespace.Invoice")
            ?? throw new InvalidOperationException("Invoice type not found");
        var invoiceLineType = compilationResult.Assembly.GetType("TestNamespace.InvoiceLine")
            ?? throw new InvalidOperationException("InvoiceLine type not found");

        // Item without the "sk" attribute at all (only "pk")
        var itemWithoutSk = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "PK#1" }
        };

        // Act & Assert
        InvokeMatchesEntity(invoiceType, itemWithoutSk).Should().BeFalse(
            "Invoice should return false when discriminator property 'sk' is missing");
        InvokeMatchesEntity(invoiceLineType, itemWithoutSk).Should().BeFalse(
            "InvoiceLine should return false when discriminator property 'sk' is missing");
    }

    #endregion

    #region Test 4: Null Discriminator Value Returns False

    [Fact]
    public void MatchesEntity_NullDiscriminatorValue_ReturnsFalse()
    {
        // Arrange — use Invoice/InvoiceLine hierarchy
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            ThreeLevelInvoiceHierarchySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var invoiceType = compilationResult.Assembly.GetType("TestNamespace.Invoice")
            ?? throw new InvalidOperationException("Invoice type not found");
        var invoiceLineType = compilationResult.Assembly.GetType("TestNamespace.InvoiceLine")
            ?? throw new InvalidOperationException("InvoiceLine type not found");

        // Item where "sk" has NULL = true instead of a string value
        var itemWithNullSk = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "PK#1" },
            ["sk"] = new AttributeValue { NULL = true }
        };

        // Act & Assert
        InvokeMatchesEntity(invoiceType, itemWithNullSk).Should().BeFalse(
            "Invoice should return false when discriminator property 'sk' has NULL value");
        InvokeMatchesEntity(invoiceLineType, itemWithNullSk).Should().BeFalse(
            "InvoiceLine should return false when discriminator property 'sk' has NULL value");
    }

    #endregion

    #region Helper Methods

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

        return (bool)method.Invoke(null, new object[] { item })!;
    }

    #endregion
}
