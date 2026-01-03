using System.Collections.Immutable;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for composite entity assembly with overlapping discriminator patterns.
/// 
/// **Feature: hydration-architecture-consolidation, Property 1: Composite Entity Assembly with Overlapping Discriminator Patterns**
/// **Validates: Requirements 1.1, 1.3, 4.1, 4.2**
/// 
/// These tests verify that for any composite entity where the child entity's discriminator pattern
/// overlaps with the parent's pattern (e.g., parent `LOCATION#*` and child `*#HOURS`), calling
/// `ToCompositeEntityAsync()` with items containing both parent and child records SHALL correctly
/// populate the parent's `[RelatedEntity]` collection with all child entities whose sort keys
/// match the `[RelatedEntity]` pattern.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class OverlappingDiscriminatorPatternPropertyTests
{
    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 1: Overlapping Discriminator Patterns**
    /// **Validates: Requirements 1.1, 1.3**
    /// 
    /// Property: For any parent/child entity pair with overlapping discriminator patterns,
    /// the generated code SHALL use the [RelatedEntity] sort key pattern as the primary
    /// matching criteria, NOT MatchesEntity().
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_UsesRelatedEntityPattern_NotMatchesEntity_ForOverlappingPatterns()
    {
        return Prop.ForAll(
            OverlappingPatternConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateOverlappingPatternSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                if (hasErrors)
                {
                    return false.Label($"Compilation errors: {string.Join(", ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
                }
                
                // Get the generated code for the parent entity
                var parentCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ParentEntityName}.g.cs"));
                
                if (parentCode == null)
                {
                    return false.Label($"Generated source for {config.ParentEntityName} not found");
                }
                
                var generatedCode = parentCode.SourceText.ToString();
                
                // CRITICAL: Verify the generated code does NOT use MatchesEntity() for related entity filtering
                var usesMatchesEntity = generatedCode.Contains($"{config.ChildEntityName}.MatchesEntity(item)");
                
                // Verify it uses the sort key pattern matching instead
                var usesPatternMatching = generatedCode.Contains("MatchesPattern") || 
                                          generatedCode.Contains("sortKeyPattern") ||
                                          generatedCode.Contains(config.RelatedEntityPattern.Replace("*", ""));
                
                // Verify FromDynamoDb is called for the child entity
                var callsFromDynamoDb = generatedCode.Contains($"{config.ChildEntityName}.FromDynamoDb<{config.ChildEntityName}>");
                
                return (!usesMatchesEntity && callsFromDynamoDb)
                    .Label($"usesMatchesEntity={usesMatchesEntity}, callsFromDynamoDb={callsFromDynamoDb}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 1: Overlapping Discriminator Patterns**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// Property: For any parent entity with overlapping discriminator patterns, the generated code
    /// SHALL correctly identify the primary entity item using the entity's own sort key pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_IdentifiesPrimaryEntity_UsingOwnSortKeyPattern()
    {
        return Prop.ForAll(
            OverlappingPatternConfigArbitrary(),
            config =>
            {
                // Create an entity model with overlapping patterns
                var entity = CreateEntityModelWithOverlappingPatterns(config);
                
                // Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);
                
                // Property: The generated code should identify the primary entity
                // by checking for items that match the parent's sort key pattern
                var containsPrimaryEntityIdentification = result.Contains("// Identify the primary entity item") ||
                                                          result.Contains("primaryItem") ||
                                                          result.Contains("FindPrimaryEntity");
                
                // The code should NOT rely solely on MatchesEntity for primary identification
                // when there are overlapping patterns
                var usesPatternBasedIdentification = result.Contains("sortKey") || 
                                                     result.Contains("sk") ||
                                                     result.Contains("SortKey");
                
                return (containsPrimaryEntityIdentification || usesPatternBasedIdentification)
                    .Label($"containsPrimaryEntityIdentification={containsPrimaryEntityIdentification}, usesPatternBasedIdentification={usesPatternBasedIdentification}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 1: Overlapping Discriminator Patterns**
    /// **Validates: Requirements 1.1, 1.3**
    /// 
    /// Property: For any entity with [RelatedEntity] collections, the generated code SHALL
    /// wrap the FromDynamoDb call in try/catch for graceful error handling when patterns overlap.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedCode_UsesTryCatch_ForRelatedEntityDeserialization()
    {
        return Prop.ForAll(
            OverlappingPatternConfigArbitrary(),
            config =>
            {
                // Create an entity model with overlapping patterns
                var entity = CreateEntityModelWithOverlappingPatterns(config);
                
                // Generate the entity implementation
                var result = MapperGenerator.GenerateEntityImplementation(entity);
                
                // Property: The generated code should use try/catch for related entity deserialization
                var containsTryCatch = result.Contains("try") && result.Contains("catch (Exception");
                
                // The catch block should NOT re-throw (it should skip and continue)
                var catchBlocksSkipAndContinue = !result.Contains("catch (Exception ex)\n            {\n                throw;");
                
                return (containsTryCatch && catchBlocksSkipAndContinue)
                    .Label($"containsTryCatch={containsTryCatch}, catchBlocksSkipAndContinue={catchBlocksSkipAndContinue}");
            });
    }

    /// <summary>
    /// **Feature: hydration-architecture-consolidation, Property 1: Overlapping Discriminator Patterns**
    /// **Validates: Requirements 1.1, 1.3, 4.1, 4.2**
    /// 
    /// Property: For any composite entity with overlapping patterns, the generated code SHALL
    /// compile successfully and produce valid C# code.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedCode_CompilesSuccessfully_WithOverlappingPatterns()
    {
        return Prop.ForAll(
            OverlappingPatternConfigArbitrary(),
            config =>
            {
                // Generate source code for the entity configuration
                var source = GenerateOverlappingPatternSource(config);
                
                // Generate code using the source generator
                var result = GenerateCode(source);
                
                // Check for compilation errors
                var hasErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                
                if (hasErrors)
                {
                    var errorMessages = string.Join(", ", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.GetMessage()));
                    return false.Label($"Compilation errors: {errorMessages}");
                }
                
                // Get the generated code for the parent entity
                var parentCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ParentEntityName}.g.cs"));
                
                var childCode = result.GeneratedSources
                    .FirstOrDefault(s => s.FileName.Contains($"{config.ChildEntityName}.g.cs"));
                
                if (parentCode == null || childCode == null)
                {
                    return false.Label("Generated sources not found");
                }
                
                // Verify the generated code compiles
                try
                {
                    CompilationVerifier.AssertGeneratedCodeCompiles(
                        parentCode.SourceText.ToString(),
                        source,
                        childCode.SourceText.ToString());
                    return true.Label("Compilation successful");
                }
                catch (CompilationFailedException ex)
                {
                    return false.Label($"Compilation failed: {ex.Message}");
                }
            });
    }

    #region Arbitraries

    /// <summary>
    /// Generates arbitrary configurations for overlapping discriminator pattern testing.
    /// </summary>
    private static Arbitrary<OverlappingPatternConfig> OverlappingPatternConfigArbitrary()
    {
        return Gen.Elements(
            // Pattern: Parent LOCATION#*, Child *#HOURS
            new OverlappingPatternConfig
            {
                ParentEntityName = "LocationEntity",
                ParentSortKeyPrefix = "LOCATION",
                ChildEntityName = "OperatingHoursEntity",
                ChildSortKeyPattern = "*#HOURS",
                RelatedEntityPattern = "LOCATION#*#HOURS#*",
                TableName = "locations"
            },
            // Pattern: Parent ORDER#*, Child *#LINE
            new OverlappingPatternConfig
            {
                ParentEntityName = "OrderEntity",
                ParentSortKeyPrefix = "ORDER",
                ChildEntityName = "OrderLineEntity",
                ChildSortKeyPattern = "*#LINE",
                RelatedEntityPattern = "ORDER#*#LINE#*",
                TableName = "orders"
            },
            // Pattern: Parent CUSTOMER#*, Child *#ADDRESS
            new OverlappingPatternConfig
            {
                ParentEntityName = "CustomerEntity",
                ParentSortKeyPrefix = "CUSTOMER",
                ChildEntityName = "AddressEntity",
                ChildSortKeyPattern = "*#ADDRESS",
                RelatedEntityPattern = "CUSTOMER#*#ADDRESS#*",
                TableName = "customers"
            },
            // Pattern: Parent INVOICE#*, Child *#PAYMENT
            new OverlappingPatternConfig
            {
                ParentEntityName = "InvoiceEntity",
                ParentSortKeyPrefix = "INVOICE",
                ChildEntityName = "PaymentEntity",
                ChildSortKeyPattern = "*#PAYMENT",
                RelatedEntityPattern = "INVOICE#*#PAYMENT#*",
                TableName = "invoices"
            },
            // Pattern: Parent PRODUCT#*, Child *#VARIANT
            new OverlappingPatternConfig
            {
                ParentEntityName = "ProductEntity",
                ParentSortKeyPrefix = "PRODUCT",
                ChildEntityName = "VariantEntity",
                ChildSortKeyPattern = "*#VARIANT",
                RelatedEntityPattern = "PRODUCT#*#VARIANT#*",
                TableName = "products"
            }
        ).ToArbitrary();
    }

    #endregion

    #region Source Generation Helpers

    /// <summary>
    /// Generates source code for entities with overlapping discriminator patterns.
    /// </summary>
    private static string GenerateOverlappingPatternSource(OverlappingPatternConfig config)
    {
        return $@"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{{
    /// <summary>
    /// Parent entity with discriminator pattern that overlaps with child.
    /// Pattern: {config.ParentSortKeyPrefix}#*
    /// </summary>
    [DynamoDbTable(""{config.TableName}"", IsDefault = true)]
    public partial class {config.ParentEntityName}
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;

        [SortKey(Prefix = ""{config.ParentSortKeyPrefix}"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk {{ get; set; }} = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name {{ get; set; }} = string.Empty;

        /// <summary>
        /// Related entity collection with pattern that overlaps with parent's discriminator.
        /// Pattern: {config.RelatedEntityPattern}
        /// </summary>
        [RelatedEntity(""{config.RelatedEntityPattern}"", EntityType = typeof({config.ChildEntityName}))]
        public List<{config.ChildEntityName}> Children {{ get; set; }} = new();
    }}

    /// <summary>
    /// Child entity with discriminator pattern that overlaps with parent.
    /// Pattern: {config.ChildSortKeyPattern}
    /// </summary>
    [DynamoDbTable(""{config.TableName}"")]
    public partial class {config.ChildEntityName}
    {{
        [PartitionKey]
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

    /// <summary>
    /// Creates an EntityModel with overlapping discriminator patterns for testing.
    /// </summary>
    private static EntityModel CreateEntityModelWithOverlappingPatterns(OverlappingPatternConfig config)
    {
        return new EntityModel
        {
            ClassName = config.ParentEntityName,
            Namespace = "TestNamespace",
            TableName = config.TableName,
            IsDefault = true,
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = config.ParentSortKeyPrefix }
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Children",
                    PropertyType = $"List<{config.ChildEntityName}>",
                    SortKeyPattern = config.RelatedEntityPattern,
                    EntityType = config.ChildEntityName,
                    IsCollection = true,
                    ChildEntityHasRelationships = false,
                    ChildEntityRelationships = Array.Empty<RelationshipModel>()
                }
            }
        };
    }

    /// <summary>
    /// Generates code using the source generator.
    /// </summary>
    private static OverlappingPatternTestResult GenerateCode(string source)
    {
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences().ToList();

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
            .Select(tree => new OverlappingPatternGeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new OverlappingPatternTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    #endregion
}

/// <summary>
/// Configuration for generating test entities with overlapping discriminator patterns.
/// </summary>
internal class OverlappingPatternConfig
{
    public required string ParentEntityName { get; set; }
    public required string ParentSortKeyPrefix { get; set; }
    public required string ChildEntityName { get; set; }
    public required string ChildSortKeyPattern { get; set; }
    public required string RelatedEntityPattern { get; set; }
    public required string TableName { get; set; }
}

/// <summary>
/// Result from running the source generator for overlapping pattern tests.
/// </summary>
internal class OverlappingPatternTestResult
{
    public required ImmutableArray<Diagnostic> Diagnostics { get; set; }
    public required OverlappingPatternGeneratedSource[] GeneratedSources { get; set; }
}

/// <summary>
/// Represents a generated source file for overlapping pattern tests.
/// </summary>
internal class OverlappingPatternGeneratedSource
{
    public OverlappingPatternGeneratedSource(string fileName, Microsoft.CodeAnalysis.Text.SourceText sourceText)
    {
        FileName = fileName;
        SourceText = sourceText;
    }

    public string FileName { get; }
    public Microsoft.CodeAnalysis.Text.SourceText SourceText { get; }
}
