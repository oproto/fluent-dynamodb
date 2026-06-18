using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Bug Condition Exploration Test: Async Multi-Item FromDynamoDbAsync Discards Related Items
/// 
/// **Validates: Requirements 1.1, 1.2, 1.4**
/// 
/// Bug Condition: isBugCondition(entity) = entity.HasEncryptedProperties AND entity.HasRelatedEntityCollections
/// 
/// For entities with [Encrypted] properties AND [RelatedEntity] collections, the generated
/// FromDynamoDbAsync(IList&lt;...&gt;) method is a stub that only processes items[0], discarding
/// all related entity items. The sync FromDynamoDb(IList&lt;...&gt;) has full composite assembly
/// logic (primary entity identification via regex exclusion, related entity pattern matching,
/// collection population) — but the async path does not.
/// 
/// This test is EXPECTED TO FAIL on unfixed code, confirming the bug exists.
/// </summary>
public class CompositeEntityAsyncMultiItemBugExplorationTests
{
    /// <summary>
    /// Property test that verifies the bug condition: for any entity with [Encrypted] properties
    /// AND [RelatedEntity] collections, the generated FromDynamoDbAsync(IList&lt;...&gt;) MUST contain
    /// composite assembly logic (not just items[0] delegation).
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// 
    /// On unfixed code, this test will FAIL because the generated async multi-item method
    /// is a stub that only delegates to FromDynamoDbAsync(items[0], ...).
    /// </summary>
    [Property(MaxTest = 1, Arbitrary = new[] { typeof(EncryptedCompositeEntityArbitrary) })]
    public Property BugCondition_AsyncMultiItemFromDynamoDbAsync_MustContainCompositeAssemblyLogic(
        EncryptedCompositeEntityConfig config)
    {
        // Arrange: Generate source code for entity with [Encrypted] + [RelatedEntity]
        var source = GenerateEntitySource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: The generated FromDynamoDbAsync(IList<...>) method must contain
        // composite assembly logic — NOT just "return await FromDynamoDbAsync<TSelf>(items[0], ...)"
        //
        // Specifically, it should contain:
        // 1. Primary entity identification (regex or pattern matching to exclude related items)
        // 2. Related entity pattern matching  
        // 3. Collection population logic
        //
        // The sync FromDynamoDb(IList<...>) contains this logic. The async version should too.
        var hasCompositeAssemblyLogic = entityCode.Contains("primaryItem")
            || entityCode.Contains("Primary entity")
            || entityCode.Contains("Regex.IsMatch")
            || entityCode.Contains("related entity")
            || (entityCode.Contains("foreach") && entityCode.Contains("items") && entityCode.Contains(config.RelatedEntityPattern));

        // On unfixed code, the async multi-item method just does:
        //   "return await FromDynamoDbAsync<TSelf>(items[0], blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false);"
        // It should instead have composite assembly logic.
        return hasCompositeAssemblyLogic.ToProperty()
            .Label($"FromDynamoDbAsync(IList<...>) for {config.EntityName} with [Encrypted]+[RelatedEntity] " +
                   $"must contain composite assembly logic, not just items[0] delegation. " +
                   $"Pattern: '{config.RelatedEntityPattern}'");
    }

    /// <summary>
    /// Deterministic test case that explicitly demonstrates the bug with a known entity configuration.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 1.4**
    /// 
    /// Creates an entity with [Encrypted] property and [RelatedEntity] collection,
    /// then verifies the generated async multi-item method lacks composite assembly logic.
    /// 
    /// NOTE: For encrypted entities, the SYNC FromDynamoDb methods are stubs that throw
    /// NotSupportedException (by design). The ASYNC FromDynamoDbAsync is the method that
    /// should work — but its multi-item overload is also a stub (the bug).
    /// </summary>
    [Fact]
    public void BugCondition_EncryptedEntityWithRelatedItems_AsyncMultiItemLacksCompositeLogic()
    {
        // Arrange: Entity with [Encrypted] property AND [RelatedEntity] collection
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
    [DynamoDbTable(""secure-orders"")]
    public partial class SecureOrder
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNumber"")]
        public string OrderNumber { get; set; } = string.Empty;

        [Encrypted]
        [DynamoDbAttribute(""paymentDetails"")]
        public string PaymentDetails { get; set; } = string.Empty;

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(SecureOrderLine))]
        public List<SecureOrderLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""secure-orders"")]
    public partial class SecureOrderLine
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

        // Act: Run the source generator
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, "SecureOrder.g.cs");

        // Verify the entity was generated (it should generate despite the bug)
        Assert.NotNull(entityCode);
        Assert.NotEmpty(entityCode);

        // For encrypted entities, the sync FromDynamoDb(IList<...>) is a stub that throws
        // NotSupportedException (by design). The async path is the one that should work.
        // Verify the entity HAS encrypted properties (making sync path throw)
        Assert.Contains("NotSupportedException", entityCode);
        Assert.Contains("encrypted properties and requires async methods", entityCode);

        // Verify the entity IS recognized as multi-item (has relationships metadata)
        Assert.Contains("Relationships = new RelationshipMetadata[]", entityCode);
        Assert.Contains("Lines", entityCode);

        // Now verify: The async FromDynamoDbAsync(IList<...>) should have composite
        // assembly logic — but on unfixed code, it's just a stub delegating to items[0].
        //
        // Find the async multi-item method
        var asyncMultiItemMethodStart = entityCode.IndexOf(
            "public static async Task<TSelf> FromDynamoDbAsync<TSelf>(");

        // Find the specific overload that takes IList (not single Dictionary)
        while (asyncMultiItemMethodStart >= 0)
        {
            var nextChunk = entityCode.Substring(asyncMultiItemMethodStart,
                Math.Min(300, entityCode.Length - asyncMultiItemMethodStart));
            if (nextChunk.Contains("IList<Dictionary<string, AttributeValue>> items"))
                break;
            asyncMultiItemMethodStart = entityCode.IndexOf(
                "public static async Task<TSelf> FromDynamoDbAsync<TSelf>(",
                asyncMultiItemMethodStart + 1);
        }

        Assert.True(asyncMultiItemMethodStart >= 0,
            "Generated code should contain FromDynamoDbAsync<TSelf>(IList<...>) method");

        // Extract the method body (from start to sufficient length to capture the logic)
        var methodBody = entityCode.Substring(asyncMultiItemMethodStart,
            Math.Min(2000, entityCode.Length - asyncMultiItemMethodStart));

        // THE BUG: The async multi-item method should contain composite assembly logic:
        // - Primary entity identification (regex-based or pattern matching)
        // - Related entity pattern matching
        // - Collection population
        //
        // But on unfixed code, it just contains:
        //   "return await FromDynamoDbAsync<TSelf>(items[0], blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false);"
        // This means ALL items after index 0 are discarded — related entities are lost.
        var hasAsyncCompositeLogic = methodBody.Contains("primaryItem")
            || methodBody.Contains("Primary entity")
            || methodBody.Contains("Regex.IsMatch")
            || methodBody.Contains("ORDER#")
            || (methodBody.Contains("foreach") && methodBody.Contains("items") && !methodBody.Contains("items[0]"));

        // This assertion will FAIL on unfixed code — confirming the bug
        Assert.True(hasAsyncCompositeLogic,
            "BUG CONFIRMED: FromDynamoDbAsync(IList<...>) for entity with [Encrypted] + [RelatedEntity] " +
            "does NOT contain composite assembly logic. It only delegates to items[0], " +
            "discarding all related entity items. " +
            "Counterexample: FromDynamoDbAsync([primaryItem, relatedItem1, relatedItem2]) " +
            "returns entity with empty related collections because only items[0] is processed.");
    }

    #region Helper Methods

    private static string GenerateEntitySource(EncryptedCompositeEntityConfig config)
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

        [Encrypted]
        [DynamoDbAttribute(""secretData"")]
        public string SecretData {{ get; set; }} = string.Empty;

        [RelatedEntity(""{config.RelatedEntityPattern}"", EntityType = typeof({config.RelatedEntityName}))]
        public List<{config.RelatedEntityName}> RelatedItems {{ get; set; }} = new();
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

#region Arbitraries for Property-Based Testing

/// <summary>
/// Configuration for generating encrypted composite entity test cases.
/// </summary>
public class EncryptedCompositeEntityConfig
{
    public string TableName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string PkPrefix { get; set; } = string.Empty;
    public string SkPrefix { get; set; } = string.Empty;
    public string RelatedEntityName { get; set; } = string.Empty;
    public string RelatedEntityPattern { get; set; } = string.Empty;

    public override string ToString() =>
        $"Entity={EntityName}, Table={TableName}, Pattern={RelatedEntityPattern}";
}

/// <summary>
/// FsCheck arbitrary for generating valid encrypted composite entity configurations.
/// Generates entities that satisfy the bug condition:
///   isBugCondition(entity) = entity.HasEncryptedProperties AND entity.HasRelatedEntityCollections
/// </summary>
public class EncryptedCompositeEntityArbitrary
{
    public static Arbitrary<EncryptedCompositeEntityConfig> EncryptedCompositeEntityConfig()
    {
        // Generate valid C# identifier names
        var entityNames = Gen.Elements(
            "SecureInvoice", "EncryptedOrder", "ProtectedAccount",
            "SecureTransaction", "EncryptedRecord");

        var relatedNames = Gen.Elements(
            "SecureLineItem", "EncryptedDetail", "ProtectedEntry",
            "SecureAudit", "EncryptedChild");

        var tableNames = Gen.Elements(
            "secure-invoices", "encrypted-orders", "protected-accounts",
            "secure-transactions", "encrypted-records");

        var prefixes = Gen.Elements("TENANT", "CUSTOMER", "ACCOUNT", "ORG", "USER");
        var skPrefixes = Gen.Elements("INVOICE", "ORDER", "TXN", "RECORD", "ITEM");

        // Generate patterns that follow the hierarchical sort key convention
        var patterns = Gen.Elements(
            "INVOICE#*#LINE#*",
            "ORDER#*#DETAIL#*",
            "TXN#*#ENTRY#*",
            "RECORD#*#CHILD#*",
            "ITEM#*#SUB#*");

        var gen = from entityName in entityNames
                  from relatedName in relatedNames
                  from tableName in tableNames
                  from pkPrefix in prefixes
                  from skPrefix in skPrefixes
                  from pattern in patterns
                  select new EncryptedCompositeEntityConfig
                  {
                      EntityName = entityName,
                      RelatedEntityName = relatedName,
                      TableName = tableName,
                      PkPrefix = pkPrefix,
                      SkPrefix = skPrefix,
                      RelatedEntityPattern = pattern
                  };

        return Arb.From(gen);
    }
}

#endregion
