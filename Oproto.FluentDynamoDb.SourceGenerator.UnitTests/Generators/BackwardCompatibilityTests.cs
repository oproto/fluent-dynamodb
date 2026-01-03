// ============================================================================
// Backward Compatibility Tests
// ============================================================================
// These tests verify that the hydration architecture consolidation changes
// maintain backward compatibility with existing entity patterns.
//
// Requirements: 8.1, 8.2, 8.3, 8.4 from hydration-architecture-consolidation spec
// ============================================================================

using System.Collections.Immutable;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for backward compatibility of the hydration architecture consolidation.
/// Verifies that existing entity patterns continue to work after the changes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "hydration-architecture-consolidation")]
public class BackwardCompatibilityTests
{
    #region Task 10.1: JsonBlob Instead of DynamoDbMap Tests

    /// <summary>
    /// Verifies that composite entity assembly still works when using [JsonBlob]
    /// instead of [DynamoDbMap] for nested objects.
    /// 
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Fact]
    public void CompositeEntity_WithJsonBlobInsteadOfDynamoDbMap_AssemblesCorrectly()
    {
        // Arrange - Create composite entity with [JsonBlob] property
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class OrderWithJsonBlob
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNumber"")]
        public string OrderNumber { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""shippingAddress"")]
        public ShippingAddress? ShippingAddress { get; set; }

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(OrderLineEntity))]
        public List<OrderLineEntity> Lines { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLineEntity
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""productId"")]
        public string ProductId { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }

    public class ShippingAddress
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }
}";

        // Act - Generate code with System.Text.Json reference
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for composite entity with JsonBlob");

        // Get the generated code
        var orderCode = GetGeneratedSource(result, "OrderWithJsonBlob.g.cs");
        var lineCode = GetGeneratedSource(result, "OrderLineEntity.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(orderCode, source, lineCode);

        // Verify JsonBlob uses JSON serialization
        orderCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.ShippingAddress>",
            "JsonBlob property should use JSON serialization");
        
        // Verify related entity mapping works
        orderCode.Should().Contain("OrderLineEntity.FromDynamoDb<OrderLineEntity>(item, options)",
            "related entity mapping should work with JsonBlob parent");
        
        // Verify try/catch error handling is present
        orderCode.Should().Contain("try",
            "error handling should be present for related entity mapping");
    }


    /// <summary>
    /// Verifies that child entities with [JsonBlob] properties work correctly
    /// when populated via [RelatedEntity] collections.
    /// 
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Fact]
    public void CompositeEntity_WithJsonBlobInChildEntity_AssemblesCorrectly()
    {
        // Arrange - Create composite entity where child has [JsonBlob] property
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""products"", IsDefault = true)]
    public partial class ProductEntity
    {
        [PartitionKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""productName"")]
        public string ProductName { get; set; } = string.Empty;

        [RelatedEntity(""PRODUCT#*#VARIANT#*"", EntityType = typeof(ProductVariant))]
        public List<ProductVariant> Variants { get; set; } = new();
    }

    [DynamoDbTable(""products"")]
    public partial class ProductVariant
    {
        [PartitionKey(Prefix = ""PRODUCT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""sku"")]
        public string Sku { get; set; } = string.Empty;

        [JsonBlob]
        [DynamoDbAttribute(""attributes"")]
        public VariantAttributes? Attributes { get; set; }
    }

    public class VariantAttributes
    {
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source, includeSystemTextJson: true);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var productCode = GetGeneratedSource(result, "ProductEntity.g.cs");
        var variantCode = GetGeneratedSource(result, "ProductVariant.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(productCode, source, variantCode);

        // Verify child entity uses JSON serialization for JsonBlob
        variantCode.Should().Contain("options.JsonSerializer.Deserialize<TestNamespace.VariantAttributes>",
            "child entity should use JSON serialization for JsonBlob property");
        
        // Verify parent passes options to child
        productCode.Should().Contain("ProductVariant.FromDynamoDb<ProductVariant>(item, options)",
            "parent should pass options to child for JSON serialization");
    }

    #endregion


    #region Task 10.2: Entity Without DynamoDbMap Properties Tests

    /// <summary>
    /// Verifies that composite entity assembly behavior is unchanged for entities
    /// that have no [DynamoDbMap] properties.
    /// 
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Fact]
    public void CompositeEntity_WithoutDynamoDbMapProperties_BehaviorUnchanged()
    {
        // Arrange - Create composite entity with only primitive properties
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""customers"", IsDefault = true)]
    public partial class CustomerEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""customerName"")]
        public string CustomerName { get; set; } = string.Empty;

        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;

        [DynamoDbAttribute(""age"")]
        public int Age { get; set; }

        [DynamoDbAttribute(""isActive"")]
        public bool IsActive { get; set; }

        [RelatedEntity(""CUSTOMER#*#ORDER#*"", EntityType = typeof(CustomerOrder))]
        public List<CustomerOrder> Orders { get; set; } = new();
    }

    [DynamoDbTable(""customers"")]
    public partial class CustomerOrder
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }

        [DynamoDbAttribute(""orderDate"")]
        public DateTime OrderDate { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No compilation errors
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error,
            "source generator should not produce errors for simple composite entity");

        var customerCode = GetGeneratedSource(result, "CustomerEntity.g.cs");
        var orderCode = GetGeneratedSource(result, "CustomerOrder.g.cs");
        
        // Verify compilation
        CompilationVerifier.AssertGeneratedCodeCompiles(customerCode, source, orderCode);

        // Verify related entity mapping works
        customerCode.Should().Contain("CustomerOrder.FromDynamoDb<CustomerOrder>(item, options)",
            "related entity mapping should work for simple entities");
        
        // Verify try/catch error handling
        customerCode.Should().Contain("try",
            "error handling should be present");
        
        // Verify MatchesEntity is NOT used for related entity mapping
        customerCode.Should().NotContain("CustomerOrder.MatchesEntity(item)",
            "MatchesEntity should NOT be used for related entity mapping");
    }

    #endregion


    #region Task 10.3: Existing RelatedEntity Patterns Tests

    /// <summary>
    /// Verifies that existing [RelatedEntity] patterns with wildcard sort key patterns
    /// continue to work correctly.
    /// 
    /// **Validates: Requirements 8.3, 8.4**
    /// </summary>
    [Fact]
    public void ExistingRelatedEntityPatterns_WithWildcardSortKey_ContinueToWork()
    {
        // Arrange - Create composite entity with various wildcard patterns
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
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

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLineEntity))]
        public List<InvoiceLineEntity> Lines { get; set; } = new();

        [RelatedEntity(""INVOICE#*#PAYMENT#*"", EntityType = typeof(PaymentEntity))]
        public List<PaymentEntity> Payments { get; set; } = new();
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

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""lineDescription"")]
        public string LineDescription { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }

    [DynamoDbTable(""invoices"")]
    public partial class PaymentEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""paymentId"")]
        public string PaymentId { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }

        [DynamoDbAttribute(""paymentDate"")]
        public DateTime PaymentDate { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var invoiceCode = GetGeneratedSource(result, "InvoiceEntity.g.cs");
        var lineCode = GetGeneratedSource(result, "InvoiceLineEntity.g.cs");
        var paymentCode = GetGeneratedSource(result, "PaymentEntity.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(invoiceCode, source, lineCode, paymentCode);

        // Verify both related entity mappings are generated
        invoiceCode.Should().Contain("InvoiceLineEntity.FromDynamoDb<InvoiceLineEntity>(item, options)",
            "InvoiceLine related entity mapping should be generated");
        invoiceCode.Should().Contain("PaymentEntity.FromDynamoDb<PaymentEntity>(item, options)",
            "Payment related entity mapping should be generated");
        
        // Verify MatchesEntity is NOT used (the fix)
        invoiceCode.Should().NotContain("InvoiceLineEntity.MatchesEntity(item)",
            "MatchesEntity should NOT be used for related entity mapping");
        invoiceCode.Should().NotContain("PaymentEntity.MatchesEntity(item)",
            "MatchesEntity should NOT be used for related entity mapping");
    }


    /// <summary>
    /// Verifies that existing discriminator pattern definitions continue to work
    /// with the hydration architecture changes.
    /// 
    /// **Validates: Requirements 8.3, 8.4**
    /// </summary>
    [Fact]
    public void ExistingDiscriminatorPatterns_ContinueToWork()
    {
        // Arrange - Create entities with discriminator patterns
        // Note: DiscriminatorPattern requires DiscriminatorProperty to be specified
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""locations"", IsDefault = true, DiscriminatorProperty = ""sk"", DiscriminatorPattern = ""LOCATION#*"")]
    public partial class LocationEntity
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LOCATION"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""locationName"")]
        public string LocationName { get; set; } = string.Empty;

        [RelatedEntity(""LOCATION#*#HOURS#*"", EntityType = typeof(OperatingHoursEntity))]
        public List<OperatingHoursEntity> OperatingHours { get; set; } = new();
    }

    [DynamoDbTable(""locations"", DiscriminatorProperty = ""sk"", DiscriminatorPattern = ""*#HOURS#*"")]
    public partial class OperatingHoursEntity
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""dayOfWeek"")]
        public string DayOfWeek { get; set; } = string.Empty;

        [DynamoDbAttribute(""openTime"")]
        public string OpenTime { get; set; } = string.Empty;

        [DynamoDbAttribute(""closeTime"")]
        public string CloseTime { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var locationCode = GetGeneratedSource(result, "LocationEntity.g.cs");
        var hoursCode = GetGeneratedSource(result, "OperatingHoursEntity.g.cs");
        
        CompilationVerifier.AssertGeneratedCodeCompiles(locationCode, source, hoursCode);

        // Verify related entity mapping works with overlapping discriminator patterns
        locationCode.Should().Contain("OperatingHoursEntity.FromDynamoDb<OperatingHoursEntity>(item, options)",
            "related entity mapping should work with overlapping discriminator patterns");
        
        // Verify MatchesEntity is NOT used (this was the bug with overlapping patterns)
        locationCode.Should().NotContain("OperatingHoursEntity.MatchesEntity(item)",
            "MatchesEntity should NOT be used - this was the bug with overlapping patterns");
        
        // Verify MatchesEntity method is still generated for the entity itself
        locationCode.Should().Contain("public static bool MatchesEntity",
            "MatchesEntity method should still be generated for entity identification");
        hoursCode.Should().Contain("public static bool MatchesEntity",
            "MatchesEntity method should still be generated for child entity");
    }

    #endregion


    #region Helper Methods

    /// <summary>
    /// Creates a mock assembly for testing optional package references.
    /// </summary>
    private static MetadataReference CreateMockAssembly(string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText("// Mock assembly") },
            DynamicCompilationHelper.GetStandardReferences().Take(1),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException($"Failed to create mock assembly {assemblyName}");
        }
        ms.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(ms);
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static BackwardCompatibilityTestResult GenerateCode(string source, bool includeSystemTextJson = false)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();
        
        if (includeSystemTextJson)
        {
            references.Add(CreateMockAssembly("Oproto.FluentDynamoDb.SystemTextJson"));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new BackwardCompatibilityGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new BackwardCompatibilityTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(BackwardCompatibilityTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        source.Should().NotBeNull($"Expected to find generated source containing '{fileNamePart}'");
        return source!.SourceText.ToString();
    }

    #endregion
}

/// <summary>
/// Result from running the source generator for backward compatibility tests.
/// </summary>
internal class BackwardCompatibilityTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required BackwardCompatibilityGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for backward compatibility tests.
/// </summary>
internal class BackwardCompatibilityGeneratedSource
{
    public BackwardCompatibilityGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}
