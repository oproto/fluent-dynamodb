using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Compilation verification tests for composite entity generated code.
/// 
/// These tests run the source generator and then COMPILE the generated output,
/// catching type mismatches and missing method overloads that pattern-matching
/// tests would miss.
/// 
/// Key scenario: encrypted parent entity with non-encrypted child entity.
/// The parent's async composite assembly calls ChildEntity.FromDynamoDbAsync(item, ...)
/// which requires the child to have a single-item FromDynamoDbAsync overload.
/// </summary>
public class CompositeEntityGeneratedCodeCompilationTests
{
    /// <summary>
    /// Verifies that generated code compiles when an encrypted parent entity has a
    /// [RelatedEntity] collection of a non-encrypted child entity.
    /// 
    /// This is the exact scenario that broke in external projects: the parent's generated
    /// async composite assembly calls ChildEntity.FromDynamoDbAsync(item, ...) where item
    /// is a single Dictionary. The child must have a matching single-item overload.
    /// </summary>
    [Fact]
    public void EncryptedParent_WithNonEncryptedChild_GeneratedCodeCompiles()
    {
        // Arrange: Encrypted parent + non-encrypted child (mirrors EmployeeEntity/PayRateEntryEntity)
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
    [DynamoDbTable(""employees"")]
    public partial class EmployeeEntity
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""EMPLOYEE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [Encrypted]
        [DynamoDbAttribute(""ssn"")]
        public string Ssn { get; set; } = string.Empty;

        [RelatedEntity(""EMPLOYEE#*#PAYRATE#*"", EntityType = typeof(PayRateEntity))]
        public List<PayRateEntity> PayRates { get; set; } = new();
    }

    // Child entity WITHOUT encryption - this is the key scenario
    [DynamoDbTable(""employees"")]
    public partial class PayRateEntity
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""rate"")]
        public decimal Rate { get; set; }

        [DynamoDbAttribute(""effectiveDate"")]
        public string EffectiveDate { get; set; } = string.Empty;
    }
}";

        // Act: Run source generator and compile the output
        var compilation = CreateCompilationWithGenerator(source);

        // Assert: No compilation errors
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    /// <summary>
    /// Verifies that generated code compiles when an encrypted parent has multiple
    /// [RelatedEntity] collections with different non-encrypted child types.
    /// </summary>
    [Fact]
    public void EncryptedParent_WithMultipleNonEncryptedChildren_GeneratedCodeCompiles()
    {
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
    [DynamoDbTable(""orders"")]
    public partial class SecureOrderEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Encrypted]
        [DynamoDbAttribute(""paymentToken"")]
        public string PaymentToken { get; set; } = string.Empty;

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(OrderLineEntity))]
        public List<OrderLineEntity> Lines { get; set; } = new();

        [RelatedEntity(""ORDER#*#SHIPPING#*"", EntityType = typeof(ShippingInfoEntity))]
        public List<ShippingInfoEntity> ShippingHistory { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }

        [DynamoDbAttribute(""price"")]
        public decimal Price { get; set; }
    }

    [DynamoDbTable(""orders"")]
    public partial class ShippingInfoEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""carrier"")]
        public string Carrier { get; set; } = string.Empty;

        [DynamoDbAttribute(""trackingNumber"")]
        public string TrackingNumber { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0,
            "Generated code for encrypted parent with multiple non-encrypted children must compile. " +
            "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// Verifies that generated code compiles when both parent and child have encryption.
    /// Both entities get the full async methods generated, so this should always work.
    /// </summary>
    [Fact]
    public void EncryptedParent_WithEncryptedChild_GeneratedCodeCompiles()
    {
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
    [DynamoDbTable(""secure-data"")]
    public partial class SecureParent
    {
        [PartitionKey(Prefix = ""ORG"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PARENT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Encrypted]
        [DynamoDbAttribute(""parentSecret"")]
        public string ParentSecret { get; set; } = string.Empty;

        [RelatedEntity(""PARENT#*#CHILD#*"", EntityType = typeof(SecureChild))]
        public List<SecureChild> Children { get; set; } = new();
    }

    [DynamoDbTable(""secure-data"")]
    public partial class SecureChild
    {
        [PartitionKey(Prefix = ""ORG"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Encrypted]
        [DynamoDbAttribute(""childSecret"")]
        public string ChildSecret { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilationWithGenerator(source);

        // Assert
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0,
            "Generated code for encrypted parent with encrypted child must compile. " +
            "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    /// <summary>
    /// Verifies that generated code compiles for a non-encrypted parent with non-encrypted child.
    /// This is the standard InvoiceManager-style scenario using the sync path.
    /// </summary>
    [Fact]
    public void NonEncryptedParent_WithNonEncryptedChild_GeneratedCodeCompiles()
    {
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
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLineEntity))]
        public List<InvoiceLineEntity> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

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

        Assert.True(errors.Count == 0,
            "Generated code for non-encrypted parent with non-encrypted child must compile. " +
            "Errors:\n" + string.Join("\n", errors.Select(e => e.ToString())));
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
