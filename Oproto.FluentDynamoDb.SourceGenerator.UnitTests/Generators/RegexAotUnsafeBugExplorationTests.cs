using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Bug Condition Exploration Test: Generated Code Contains AOT-Unsafe Regex.IsMatch
/// 
/// **Property 1: Bug Condition - AOT-Safe Generated Code**
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// 
/// Bug Condition: The source generator emits System.Text.RegularExpressions.Regex.IsMatch
/// calls into generated code for [RelatedEntity] wildcard pattern matching. This breaks
/// Native AOT deployment because the interpreted regex engine relies on runtime code paths
/// that may be trimmed.
/// 
/// These tests are EXPECTED TO FAIL on unfixed code — failure confirms the bug exists.
/// When the fix is applied (replacing Regex.IsMatch with string.Split + segment comparison),
/// these tests will PASS, confirming the generated code is AOT-safe.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
[Trait("Category", "BugExploration")]
public class RegexAotUnsafeBugExplorationTests
{
    /// <summary>
    /// **Property 1: Bug Condition - AOT-Safe Generated Code**
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
    /// 
    /// For any entity with [RelatedEntity] wildcard patterns, the source generator SHALL emit
    /// pattern matching code that uses only string.Split, string.Length, and string equality
    /// comparisons — with zero references to System.Text.RegularExpressions.Regex in the
    /// generated output.
    /// 
    /// On unfixed code, this test FAILS because generated code contains Regex.IsMatch at
    /// all 5 emission sites — proving the bug exists.
    /// </summary>
    [Property(MaxTest = 1, Arbitrary = new[] { typeof(WildcardRelatedEntityArbitrary) })]
    public Property GeneratedCode_ForWildcardRelatedEntity_MustNotContainRegexIsMatch(
        WildcardRelatedEntityConfig config)
    {
        // Arrange: Generate source code for entity with [RelatedEntity] wildcard pattern
        var source = GenerateCompositeEntitySource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: Generated code must NOT contain Regex.IsMatch (expected AOT-safe behavior)
        var containsRegexIsMatch = entityCode.Contains("Regex.IsMatch");
        var containsRegexNamespace = entityCode.Contains("System.Text.RegularExpressions.Regex");

        // The expected behavior is NO Regex.IsMatch references
        var isAotSafe = !containsRegexIsMatch && !containsRegexNamespace;

        return isAotSafe.ToProperty()
            .Label($"Generated code for entity '{config.EntityName}' with " +
                   $"[RelatedEntity(\"{config.RelatedEntityPattern}\")] must NOT contain " +
                   $"Regex.IsMatch (AOT-unsafe). " +
                   $"containsRegexIsMatch={containsRegexIsMatch}, " +
                   $"containsRegexNamespace={containsRegexNamespace}");
    }

    /// <summary>
    /// Deterministic test: Single wildcard pattern (e.g., "audit#*")
    /// 
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// Generates code for entity with [RelatedEntity("AUDIT#*")] and asserts the generated
    /// code does NOT contain Regex.IsMatch.
    /// 
    /// On unfixed code, this FAILS — the generated code contains:
    /// Regex.IsMatch(sortKey, @"^AUDIT\#[^#]*$")
    /// </summary>
    [Fact]
    public void BugCondition_SingleWildcardPattern_GeneratedCodeMustNotContainRegexIsMatch()
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

namespace TestNamespace
{
    [DynamoDbTable(""orders"")]
    public partial class OrderEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [RelatedEntity(""AUDIT#*"", EntityType = typeof(AuditEntry))]
        public List<AuditEntry> AuditEntries { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class AuditEntry
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""action"")]
        public string Action { get; set; } = string.Empty;
    }
}";

        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "OrderEntity.g.cs");

        // On unfixed code, this assertion FAILS — proving the bug exists
        Assert.DoesNotContain("Regex.IsMatch", entityCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Deterministic test: Multi-wildcard pattern (e.g., "INVOICE#*#LINE#*")
    /// 
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// Generates code for entity with [RelatedEntity("INVOICE#*#LINE#*")] and asserts
    /// the generated code does NOT contain Regex.IsMatch.
    /// 
    /// On unfixed code, this FAILS — the generated code contains:
    /// Regex.IsMatch(sortKey, @"^INVOICE\#[^#]*\#LINE\#[^#]*$")
    /// </summary>
    [Fact]
    public void BugCondition_MultiWildcardPattern_GeneratedCodeMustNotContainRegexIsMatch()
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

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "InvoiceEntity.g.cs");

        // On unfixed code, this assertion FAILS — proving the bug exists
        Assert.DoesNotContain("Regex.IsMatch", entityCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Deterministic test: Custom delimiter pattern (e.g., "TYPE_*_SUB_*" with underscore)
    /// 
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// Generates code for entity with [RelatedEntity("TYPE_*_SUB_*")] and asserts
    /// the generated code does NOT contain Regex.IsMatch.
    /// 
    /// On unfixed code, this FAILS — the generated code contains:
    /// Regex.IsMatch(sortKey, @"^TYPE\_[^\_]*\_SUB\_[^\_]*$")
    /// </summary>
    [Fact]
    public void BugCondition_CustomDelimiterPattern_GeneratedCodeMustNotContainRegexIsMatch()
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

namespace TestNamespace
{
    [DynamoDbTable(""typed_items"")]
    public partial class TypedItem
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ITEM"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [RelatedEntity(""TYPE_*_SUB_*"", EntityType = typeof(SubItem))]
        public List<SubItem> SubItems { get; set; } = new();
    }

    [DynamoDbTable(""typed_items"")]
    public partial class SubItem
    {
        [PartitionKey(Prefix = ""TENANT"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""value"")]
        public string Value { get; set; } = string.Empty;
    }
}";

        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "TypedItem.g.cs");

        // On unfixed code, this assertion FAILS — proving the bug exists
        Assert.DoesNotContain("Regex.IsMatch", entityCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Deterministic test: Multiple [RelatedEntity] attributes on the same entity
    /// 
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// Generates code for entity with multiple [RelatedEntity] wildcard patterns and
    /// asserts the generated code does NOT contain any Regex.IsMatch references.
    /// 
    /// On unfixed code, this FAILS — all pattern checks use Regex.IsMatch.
    /// </summary>
    [Fact]
    public void BugCondition_MultipleRelatedEntities_GeneratedCodeMustNotContainRegexIsMatch()
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

namespace TestNamespace
{
    [DynamoDbTable(""complex_orders"")]
    public partial class ComplexOrder
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNumber"")]
        public string OrderNumber { get; set; } = string.Empty;

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(OrderLine))]
        public List<OrderLine> Lines { get; set; } = new();

        [RelatedEntity(""ORDER#*#NOTE#*"", EntityType = typeof(OrderNote))]
        public List<OrderNote> Notes { get; set; } = new();
    }

    [DynamoDbTable(""complex_orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }
    }

    [DynamoDbTable(""complex_orders"")]
    public partial class OrderNote
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""noteText"")]
        public string NoteText { get; set; } = string.Empty;
    }
}";

        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "ComplexOrder.g.cs");

        // On unfixed code, this assertion FAILS — proving the bug exists
        // The generated code will contain Regex.IsMatch for both "ORDER#*#LINE#*" and "ORDER#*#NOTE#*"
        Assert.DoesNotContain("Regex.IsMatch", entityCode,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Deterministic test: Verify generated code does NOT contain any
    /// System.Text.RegularExpressions namespace reference in pattern matching context.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// 
    /// This verifies that the full namespace reference is absent, not just the method call.
    /// On unfixed code, this FAILS — the generated code uses the Regex class.
    /// </summary>
    [Fact]
    public void BugCondition_GeneratedCode_MustNotReferenceRegexNamespace()
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

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceDoc
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""docNumber"")]
        public string DocNumber { get; set; } = string.Empty;

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceDocLine))]
        public List<InvoiceDocLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceDocLine
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

        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "InvoiceDoc.g.cs");

        // On unfixed code, this assertion FAILS — the generated code contains
        // "System.Text.RegularExpressions.Regex.IsMatch" which is AOT-unsafe
        Assert.DoesNotContain("System.Text.RegularExpressions", entityCode,
            StringComparison.Ordinal);
    }

    #region Helper Methods

    private static string GenerateCompositeEntitySource(WildcardRelatedEntityConfig config)
    {
        return $@"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Providers.BlobStorage;

namespace TestNamespace
{{
    [DynamoDbTable(""{config.TableName}"")]
    public partial class {config.EntityName}
    {{
        [PartitionKey(Prefix = ""{config.PkPrefix}"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""{config.SkPrefix}"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        [RelatedEntity(""{config.RelatedEntityPattern}"", EntityType = typeof({config.RelatedEntityName}))]
        public List<{config.RelatedEntityName}> {config.RelatedCollectionName} {{ get; set; }} = new();
    }}

    [DynamoDbTable(""{config.TableName}"")]
    public partial class {config.RelatedEntityName}
    {{
        [PartitionKey(Prefix = ""{config.PkPrefix}"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""value"")]
        public string Value {{ get; set; }} = string.Empty;
    }}
}}";
    }

    private static GeneratorTestResult RunSourceGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedEntitySource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        Assert.NotNull(source);
        return source!.SourceText.ToString();
    }

    #endregion
}

#region Configuration and Arbitrary for Bug Exploration

/// <summary>
/// Configuration for wildcard related entity test cases used in bug exploration.
/// </summary>
public class WildcardRelatedEntityConfig
{
    public string TableName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string PkPrefix { get; set; } = string.Empty;
    public string SkPrefix { get; set; } = string.Empty;
    public string RelatedEntityName { get; set; } = string.Empty;
    public string RelatedEntityPattern { get; set; } = string.Empty;
    public string RelatedCollectionName { get; set; } = string.Empty;

    public override string ToString() =>
        $"Entity={EntityName}, Table={TableName}, Pattern={RelatedEntityPattern}";
}

/// <summary>
/// FsCheck arbitrary for generating entity configurations with [RelatedEntity] wildcard patterns.
/// Generates entities that satisfy the bug condition:
///   isBugCondition(input) = input.entityHasRelatedEntityAttribute
///                           AND input.relatedEntityPattern.contains("*")
///                           AND input.generatedCode.contains("Regex.IsMatch")
/// </summary>
public class WildcardRelatedEntityArbitrary
{
    public static Arbitrary<WildcardRelatedEntityConfig> WildcardRelatedEntityConfig()
    {
        var entityNames = Gen.Elements(
            "TestInvoice", "TestOrder", "TestAccount",
            "TestTransaction", "TestRecord");

        var relatedNames = Gen.Elements(
            "TestLineItem", "TestDetail", "TestEntry",
            "TestAudit", "TestChild");

        var collectionNames = Gen.Elements(
            "Lines", "Details", "Entries", "AuditTrail", "Children");

        var tableNames = Gen.Elements(
            "test-invoices", "test-orders", "test-accounts",
            "test-transactions", "test-records");

        var prefixes = Gen.Elements("TENANT", "CUSTOMER", "ACCOUNT", "ORG", "USER");
        var skPrefixes = Gen.Elements("INVOICE", "ORDER", "TXN", "RECORD", "ITEM");

        // Wildcard patterns that cover different scenarios:
        // - Multi-wildcard with # delimiter
        // - Single wildcard with # delimiter
        // - Custom underscore delimiter
        var patterns = Gen.Elements(
            "INVOICE#*#LINE#*",
            "ORDER#*#DETAIL#*",
            "AUDIT#*",
            "TXN#*#ENTRY#*",
            "TYPE_*_SUB_*");

        var gen = from entityName in entityNames
                  from relatedName in relatedNames
                  from collectionName in collectionNames
                  from tableName in tableNames
                  from pkPrefix in prefixes
                  from skPrefix in skPrefixes
                  from pattern in patterns
                  select new WildcardRelatedEntityConfig
                  {
                      EntityName = entityName,
                      RelatedEntityName = relatedName,
                      RelatedCollectionName = collectionName,
                      TableName = tableName,
                      PkPrefix = pkPrefix,
                      SkPrefix = skPrefix,
                      RelatedEntityPattern = pattern
                  };

        return Arb.From(gen);
    }
}

#endregion
