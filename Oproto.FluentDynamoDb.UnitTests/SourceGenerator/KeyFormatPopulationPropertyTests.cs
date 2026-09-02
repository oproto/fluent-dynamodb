using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for NormalizedKeyFormat population completeness.
/// Feature: unify-prefix-computed-discriminator, Property 10: NormalizedKeyFormat Population Completeness
/// </summary>
public class KeyFormatPopulationPropertyTests
{
    private static readonly MethodInfo ComputeNormalizedKeyFormatsMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ComputeNormalizedKeyFormats",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Creates an EntityAnalyzer instance without calling the constructor,
    /// avoiding the Roslyn assembly dependency that fails at runtime in this test project.
    /// </summary>
    private static object CreateAnalyzer()
    {
        return FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
    }

    /// <summary>
    /// Invokes the private instance ComputeNormalizedKeyFormats method via reflection.
    /// </summary>
    private static void InvokeComputeNormalizedKeyFormats(object analyzer, EntityModel entity)
    {
        ComputeNormalizedKeyFormatsMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates non-empty prefix strings safe for format string contexts.
    /// </summary>
    private static Gen<string> GenPrefix()
    {
        return Gen.Elements(
            "ORDER", "USER", "CUSTOMER", "TENANT", "INVOICE",
            "PRODUCT", "EVENT", "SESSION", "ACCT", "META",
            "A", "AB", "PREFIX", "GSI1", "TYPE");
    }

    /// <summary>
    /// Generates separator strings including empty string.
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "_", "-", ":", "|", ".", "~", "::", "##", "__", "/", "@", "");
    }

    /// <summary>
    /// Generates optional KeyFormatModel configurations:
    /// - null (no key format at all)
    /// - with prefix and separator
    /// - with prefix and default separator
    /// - with null/empty prefix (no formatting)
    /// </summary>
    private static Gen<KeyFormatModel?> GenKeyFormat()
    {
        var withPrefix = from prefix in GenPrefix()
                         from separator in GenSeparator()
                         select new KeyFormatModel { Prefix = prefix, Separator = separator };

        var withDefaultSeparator = from prefix in GenPrefix()
                                   select new KeyFormatModel { Prefix = prefix, Separator = "#" };

        var noPrefix = Gen.Constant(new KeyFormatModel { Prefix = null, Separator = "#" });

        var emptyPrefix = Gen.Constant(new KeyFormatModel { Prefix = "", Separator = "#" });

        var nullKeyFormat = Gen.Constant<KeyFormatModel?>(null);

        return Gen.OneOf(
            withPrefix.Select(x => (KeyFormatModel?)x),
            withDefaultSeparator.Select(x => (KeyFormatModel?)x),
            noPrefix.Select(x => (KeyFormatModel?)x),
            emptyPrefix.Select(x => (KeyFormatModel?)x),
            nullKeyFormat);
    }

    /// <summary>
    /// Generates optional ComputedKeyModel configurations:
    /// - null (not computed)
    /// - computed with separator only (no custom format)
    /// - computed with explicit format
    /// </summary>
    private static Gen<ComputedKeyModel?> GenComputedKey()
    {
        var noComputed = Gen.Constant<ComputedKeyModel?>(null);

        var computedWithSeparator = from separator in GenSeparator()
                                    from sourceCount in Gen.Choose(2, 4)
                                    let sources = Enumerable.Range(0, sourceCount)
                                        .Select(i => $"Source{i}")
                                        .ToArray()
                                    select (ComputedKeyModel?)new ComputedKeyModel
                                    {
                                        SourceProperties = sources,
                                        Separator = separator,
                                        Format = null
                                    };

        var computedWithFormat = from prefix in GenPrefix()
                                from separator in GenSeparator()
                                from sourceCount in Gen.Choose(1, 3)
                                let placeholders = string.Join(separator,
                                    Enumerable.Range(0, sourceCount).Select(i => $"{{{i}}}"))
                                let format = $"{prefix}{separator}{placeholders}"
                                let sources = Enumerable.Range(0, sourceCount)
                                    .Select(i => $"Source{i}")
                                    .ToArray()
                                select (ComputedKeyModel?)new ComputedKeyModel
                                {
                                    SourceProperties = sources,
                                    Separator = separator,
                                    Format = format
                                };

        return Gen.OneOf(noComputed, noComputed, computedWithSeparator, computedWithFormat);
    }

    /// <summary>
    /// Generates a key property configuration (PK or SK) with various
    /// combinations of KeyFormat and ComputedKey.
    /// </summary>
    private static Gen<(KeyFormatModel? keyFormat, ComputedKeyModel? computedKey)> GenKeyConfig()
    {
        return from keyFormat in GenKeyFormat()
               from computedKey in GenComputedKey()
               select (keyFormat, computedKey);
    }

    /// <summary>
    /// **Validates: Requirements 11.1, 11.4**
    /// For any entity analyzed by EntityAnalyzer, every property annotated with [PartitionKey]
    /// or [SortKey] SHALL have its NormalizedKeyFormat populated (non-null) after analysis completes,
    /// regardless of whether the property has a prefix, computed attribute, or neither.
    /// 
    /// This test constructs entities with both PK and SK using various configurations
    /// (prefix, no prefix, computed, not computed) and verifies ComputeNormalizedKeyFormats
    /// populates NormalizedKeyFormat for all key properties.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "10")]
    public Property AllKeyProperties_HaveNonNullNormalizedKeyFormat_AfterAnalysis()
    {
        var testCaseGen = from pkConfig in GenKeyConfig()
                          from skConfig in GenKeyConfig()
                          select (pkConfig, skConfig);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (pkConfig, skConfig) = testCase;

                // Arrange: entity with PK and SK using the generated configurations
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true,
                            KeyFormat = pkConfig.keyFormat,
                            ComputedKey = pkConfig.computedKey
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = "sk",
                            PropertyType = "string",
                            IsSortKey = true,
                            KeyFormat = skConfig.keyFormat,
                            ComputedKey = skConfig.computedKey
                        },
                        new PropertyModel
                        {
                            PropertyName = "Name",
                            AttributeName = "name",
                            PropertyType = "string",
                            IsPartitionKey = false,
                            IsSortKey = false
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeComputeNormalizedKeyFormats(analyzer, entity);

                // Assert: Both key properties must have non-null NormalizedKeyFormat
                var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
                var skProperty = entity.Properties.First(p => p.IsSortKey);
                var nonKeyProperty = entity.Properties.First(p => !p.IsPartitionKey && !p.IsSortKey);

                var pkFormatPopulated = pkProperty.NormalizedKeyFormat != null;
                var skFormatPopulated = skProperty.NormalizedKeyFormat != null;
                var nonKeyFormatNull = nonKeyProperty.NormalizedKeyFormat == null;

                return (pkFormatPopulated && skFormatPopulated && nonKeyFormatNull).ToProperty()
                    .Label($"pkKeyFormat={pkConfig.keyFormat?.Prefix ?? "null"}, " +
                           $"pkComputed={pkConfig.computedKey != null}, " +
                           $"skKeyFormat={skConfig.keyFormat?.Prefix ?? "null"}, " +
                           $"skComputed={skConfig.computedKey != null}, " +
                           $"pkFormatPopulated={pkFormatPopulated} (value='{pkProperty.NormalizedKeyFormat}'), " +
                           $"skFormatPopulated={skFormatPopulated} (value='{skProperty.NormalizedKeyFormat}'), " +
                           $"nonKeyFormatNull={nonKeyFormatNull}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 11.1, 11.4**
    /// For any entity with only a partition key (no sort key), the PK property SHALL
    /// have its NormalizedKeyFormat populated (non-null) after analysis.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "10")]
    public Property PkOnlyEntity_HasNonNullNormalizedKeyFormat_AfterAnalysis()
    {
        var testCaseGen = GenKeyConfig();

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            pkConfig =>
            {
                // Arrange: entity with PK only (no SK)
                var entity = new EntityModel
                {
                    ClassName = "PkOnlyEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = "pk",
                            PropertyType = "string",
                            IsPartitionKey = true,
                            KeyFormat = pkConfig.keyFormat,
                            ComputedKey = pkConfig.computedKey
                        },
                        new PropertyModel
                        {
                            PropertyName = "Data",
                            AttributeName = "data",
                            PropertyType = "string",
                            IsPartitionKey = false,
                            IsSortKey = false
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeComputeNormalizedKeyFormats(analyzer, entity);

                // Assert: PK property must have non-null NormalizedKeyFormat
                var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
                var pkFormatPopulated = pkProperty.NormalizedKeyFormat != null;

                return pkFormatPopulated.ToProperty()
                    .Label($"pkKeyFormat={pkConfig.keyFormat?.Prefix ?? "null"}, " +
                           $"pkComputed={pkConfig.computedKey != null}, " +
                           $"pkFormatPopulated={pkFormatPopulated} (value='{pkProperty.NormalizedKeyFormat}')");
            });
    }
}
