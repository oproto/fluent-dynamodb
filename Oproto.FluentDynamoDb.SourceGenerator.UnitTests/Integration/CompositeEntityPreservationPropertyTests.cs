using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Preservation Property Tests: Confirm baseline behavior that must NOT change after the fix.
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// 
/// These tests MUST PASS on unfixed code — they confirm existing correct behavior:
/// 1. Non-encrypted composite entities use sync FromDynamoDb(IList) with full composite assembly
/// 2. Single-item lists return entities with empty related collections (no error)
/// 3. Encrypted entities without [RelatedEntity] correctly delegate to single-item FromDynamoDbAsync
/// </summary>
public class CompositeEntityPreservationPropertyTests
{
    #region Property 1: Non-encrypted composite entities have full sync composite assembly

    /// <summary>
    /// **Validates: Requirements 3.1, 3.5**
    /// 
    /// For all non-encrypted composite entities with [RelatedEntity] collections,
    /// the generated sync FromDynamoDb(IList) method contains full composite assembly logic:
    /// - Primary entity identification via regex exclusion
    /// - Related entity pattern matching
    /// - Collection population
    /// 
    /// This mirrors InvoiceManager behavior and must remain working after the fix.
    /// </summary>
    [Property(MaxTest = 5, Arbitrary = new[] { typeof(NonEncryptedCompositeEntityArbitrary) })]
    public Property NonEncryptedComposite_SyncFromDynamoDb_HasFullCompositeAssemblyLogic(
        NonEncryptedCompositeEntityConfig config)
    {
        // Arrange: Generate source code for entity WITHOUT [Encrypted] but WITH [RelatedEntity]
        var source = GenerateNonEncryptedCompositeSource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: The sync FromDynamoDb(IList) method must have composite assembly logic
        var hasPrimaryEntityIdentification = entityCode.Contains("primaryItem")
            || entityCode.Contains("Primary entity");

        var hasRegexPatternMatching = entityCode.Contains("Regex.IsMatch");

        var hasRelatedEntityMapping = entityCode.Contains("Populate related entity properties")
            || entityCode.Contains($"Map related entity: {config.RelatedCollectionName}");

        var hasFullCompositeLogic = hasPrimaryEntityIdentification
            && hasRegexPatternMatching
            && hasRelatedEntityMapping;

        return hasFullCompositeLogic.ToProperty()
            .Label($"Non-encrypted entity '{config.EntityName}' sync FromDynamoDb(IList) " +
                   $"must have full composite assembly: " +
                   $"primaryIdent={hasPrimaryEntityIdentification}, " +
                   $"regex={hasRegexPatternMatching}, " +
                   $"relatedMapping={hasRelatedEntityMapping}");
    }

    #endregion

    #region Property 2: Single-item lists work without error regardless of encryption

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// 
    /// For all composite entities (encrypted or not), when FromDynamoDbAsync(IList)
    /// is called with a single item (items.Count == 1), the generated code delegates
    /// to the single-item method — no error, entity has empty related collections.
    /// 
    /// On unfixed code, the async multi-item method ALREADY handles single items by
    /// delegating to items[0], which is correct for single-item case.
    /// </summary>
    [Property(MaxTest = 5, Arbitrary = new[] { typeof(EncryptedCompositeEntityArbitrary) })]
    public Property SingleItemList_AsyncPath_DelegatesToSingleItemMethod(
        EncryptedCompositeEntityConfig config)
    {
        // Arrange: Generate source code for encrypted entity with [RelatedEntity]
        var source = GenerateEncryptedCompositeSource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: The async multi-item method contains items[0] delegation
        // which handles the single-item case correctly
        var asyncMethodStart = FindAsyncMultiItemMethodStart(entityCode);
        var methodBody = entityCode.Substring(asyncMethodStart,
            Math.Min(2000, entityCode.Length - asyncMethodStart));

        // The method must contain items[0] delegation (handles single-item correctly)
        var hasSingleItemDelegation = methodBody.Contains("items[0]");

        return hasSingleItemDelegation.ToProperty()
            .Label($"Encrypted entity '{config.EntityName}' FromDynamoDbAsync(IList) " +
                   $"must delegate to items[0] for single-item case");
    }

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// 
    /// Same property for non-encrypted entities: sync FromDynamoDb(IList) with a single
    /// item should work and populate only primary entity properties (empty related collections).
    /// The generated code handles this via primaryItem identification — if only one item exists,
    /// it becomes the primary item and no related items are found.
    /// </summary>
    [Property(MaxTest = 5, Arbitrary = new[] { typeof(NonEncryptedCompositeEntityArbitrary) })]
    public Property SingleItemList_SyncPath_PopulatesPrimaryEntityOnly(
        NonEncryptedCompositeEntityConfig config)
    {
        // Arrange: Generate source code for non-encrypted entity with [RelatedEntity]
        var source = GenerateNonEncryptedCompositeSource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: The sync multi-item method iterates items to find primary
        // and handles the case where no related items exist gracefully
        var hasForeachLoop = entityCode.Contains("foreach (var item in items)");
        var hasRelatedItemsList = entityCode.Contains($"var {config.RelatedCollectionName.ToLowerInvariant()}Items = new List<");

        // The sync path correctly handles single items by iterating and finding
        // the primary entity, while the related items list remains empty
        var hasGracefulEmptyHandling = hasForeachLoop && hasRelatedItemsList;

        return hasGracefulEmptyHandling.ToProperty()
            .Label($"Non-encrypted entity '{config.EntityName}' sync FromDynamoDb(IList) " +
                   $"gracefully handles single-item (empty related collections)");
    }

    #endregion

    #region Property 3: Encrypted entities WITHOUT [RelatedEntity] delegate correctly

    /// <summary>
    /// **Validates: Requirements 3.4**
    /// 
    /// For all entities with [Encrypted] but NO [RelatedEntity] properties,
    /// the multi-item FromDynamoDbAsync delegates to single-item method (items[0]).
    /// This is correct behavior — no composite assembly is needed.
    /// </summary>
    [Property(MaxTest = 5, Arbitrary = new[] { typeof(EncryptedNoRelationshipEntityArbitrary) })]
    public Property EncryptedNoRelationship_AsyncMultiItem_DelegatesToSingleItem(
        EncryptedNoRelationshipEntityConfig config)
    {
        // Arrange: Generate source code for entity with [Encrypted] but NO [RelatedEntity]
        var source = GenerateEncryptedNoRelationshipSource(config);
        var result = RunSourceGenerator(source);

        // Act: Get the generated entity code
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert: The async multi-item method delegates to items[0]
        // which is correct when there are no relationships
        var asyncMethodStart = FindAsyncMultiItemMethodStart(entityCode);
        var methodBody = entityCode.Substring(asyncMethodStart,
            Math.Min(1500, entityCode.Length - asyncMethodStart));

        var hasSingleItemDelegation = methodBody.Contains("items[0]");
        // Also confirm it does NOT have composite assembly logic (no relationships = no need)
        var hasCompositeAssembly = methodBody.Contains("primaryItem")
            || methodBody.Contains("Regex.IsMatch")
            || methodBody.Contains("Populate related entity");
        
        var isCorrectDelegation = hasSingleItemDelegation && !hasCompositeAssembly;

        return isCorrectDelegation.ToProperty()
            .Label($"Encrypted entity '{config.EntityName}' without [RelatedEntity] " +
                   $"delegates to items[0] (no composite assembly needed). " +
                   $"delegation={hasSingleItemDelegation}, noAssembly={!hasCompositeAssembly}");
    }

    #endregion

    #region Property 4: Sync multi-item code structure is unchanged

    /// <summary>
    /// **Validates: Requirements 3.1, 3.5**
    /// 
    /// Verify that the generated sync multi-item FromDynamoDb(IList) for non-encrypted
    /// composite entities contains specific structural elements that prove the assembly
    /// logic is intact: regex pattern matching, primary entity identification, and
    /// return (TSelf)(object)entity pattern.
    /// </summary>
    [Property(MaxTest = 5, Arbitrary = new[] { typeof(NonEncryptedCompositeEntityArbitrary) })]
    public Property SyncMultiItem_StructuralIntegrity_PreservedForNonEncryptedEntities(
        NonEncryptedCompositeEntityConfig config)
    {
        // Arrange
        var source = GenerateNonEncryptedCompositeSource(config);
        var result = RunSourceGenerator(source);
        var entityCode = GetGeneratedEntitySource(result, $"{config.EntityName}.g.cs");

        // Assert structural elements of the sync multi-item method
        var hasMultiItemComment = entityCode.Contains("Multi-item entity: combine all items into a single entity");
        var hasEntityConstruction = entityCode.Contains($"var entity = new {config.EntityName}()");
        var hasCastReturn = entityCode.Contains("return (TSelf)(object)entity;");
        var hasPatternComment = entityCode.Contains("Populate related entity properties based on sort key patterns");

        var structuralIntegrity = hasMultiItemComment && hasEntityConstruction
            && hasCastReturn && hasPatternComment;

        return structuralIntegrity.ToProperty()
            .Label($"Non-encrypted entity '{config.EntityName}' sync multi-item structural integrity: " +
                   $"comment={hasMultiItemComment}, construction={hasEntityConstruction}, " +
                   $"cast={hasCastReturn}, patterns={hasPatternComment}");
    }

    #endregion

    #region Helper Methods

    private static string GenerateNonEncryptedCompositeSource(NonEncryptedCompositeEntityConfig config)
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

    private static string GenerateEncryptedCompositeSource(EncryptedCompositeEntityConfig config)
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

    private static string GenerateEncryptedNoRelationshipSource(EncryptedNoRelationshipEntityConfig config)
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

        [DynamoDbAttribute(""status"")]
        public string Status {{ get; set; }} = string.Empty;
    }}
}}";
    }

    private static int FindAsyncMultiItemMethodStart(string entityCode)
    {
        var asyncMethodStart = entityCode.IndexOf(
            "public static async Task<TSelf> FromDynamoDbAsync<TSelf>(");

        // Find the specific overload that takes IList (not single Dictionary)
        while (asyncMethodStart >= 0)
        {
            var nextChunk = entityCode.Substring(asyncMethodStart,
                Math.Min(300, entityCode.Length - asyncMethodStart));
            if (nextChunk.Contains("IList<Dictionary<string, AttributeValue>> items"))
                break;
            asyncMethodStart = entityCode.IndexOf(
                "public static async Task<TSelf> FromDynamoDbAsync<TSelf>(",
                asyncMethodStart + 1);
        }

        if (asyncMethodStart < 0)
            throw new InvalidOperationException(
                "Generated code should contain FromDynamoDbAsync<TSelf>(IList<...>) method");

        return asyncMethodStart;
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

#region Arbitraries for Preservation Property Tests

/// <summary>
/// Configuration for non-encrypted composite entity test cases.
/// </summary>
public class NonEncryptedCompositeEntityConfig
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
/// Configuration for encrypted entity without relationships.
/// </summary>
public class EncryptedNoRelationshipEntityConfig
{
    public string TableName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string PkPrefix { get; set; } = string.Empty;
    public string SkPrefix { get; set; } = string.Empty;

    public override string ToString() =>
        $"Entity={EntityName}, Table={TableName}";
}

/// <summary>
/// FsCheck arbitrary for generating valid non-encrypted composite entity configurations.
/// These entities have [RelatedEntity] but NO [Encrypted] properties.
/// </summary>
public class NonEncryptedCompositeEntityArbitrary
{
    public static Arbitrary<NonEncryptedCompositeEntityConfig> NonEncryptedCompositeEntityConfig()
    {
        var entityNames = Gen.Elements(
            "Invoice", "PlainOrder", "PublicAccount",
            "OpenRecord", "BasicTransaction");

        var relatedNames = Gen.Elements(
            "InvoiceLine", "OrderDetail", "AccountEntry",
            "RecordChild", "TransactionItem");

        var collectionNames = Gen.Elements(
            "Lines", "Details", "Entries", "Children", "Items");

        var tableNames = Gen.Elements(
            "invoices", "orders", "accounts",
            "records", "transactions");

        var prefixes = Gen.Elements("TENANT", "CUSTOMER", "ACCOUNT", "ORG", "USER");
        var skPrefixes = Gen.Elements("INVOICE", "ORDER", "TXN", "RECORD", "ITEM");

        var patterns = Gen.Elements(
            "INVOICE#*#LINE#*",
            "ORDER#*#DETAIL#*",
            "TXN#*#ENTRY#*",
            "RECORD#*#CHILD#*",
            "ITEM#*#SUB#*");

        var gen = from entityName in entityNames
                  from relatedName in relatedNames
                  from collectionName in collectionNames
                  from tableName in tableNames
                  from pkPrefix in prefixes
                  from skPrefix in skPrefixes
                  from pattern in patterns
                  select new NonEncryptedCompositeEntityConfig
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

/// <summary>
/// FsCheck arbitrary for generating encrypted entities WITHOUT relationships.
/// These entities have [Encrypted] but NO [RelatedEntity] properties.
/// </summary>
public class EncryptedNoRelationshipEntityArbitrary
{
    public static Arbitrary<EncryptedNoRelationshipEntityConfig> EncryptedNoRelationshipEntityConfig()
    {
        var entityNames = Gen.Elements(
            "SecureUser", "EncryptedProfile", "ProtectedConfig",
            "SecureSession", "EncryptedToken");

        var tableNames = Gen.Elements(
            "secure-users", "encrypted-profiles", "protected-configs",
            "secure-sessions", "encrypted-tokens");

        var prefixes = Gen.Elements("TENANT", "CUSTOMER", "ACCOUNT", "ORG", "USER");
        var skPrefixes = Gen.Elements("META", "PROFILE", "CONFIG", "SESSION", "TOKEN");

        var gen = from entityName in entityNames
                  from tableName in tableNames
                  from pkPrefix in prefixes
                  from skPrefix in skPrefixes
                  select new EncryptedNoRelationshipEntityConfig
                  {
                      EntityName = entityName,
                      TableName = tableName,
                      PkPrefix = pkPrefix,
                      SkPrefix = skPrefix
                  };

        return Arb.From(gen);
    }
}

#endregion
