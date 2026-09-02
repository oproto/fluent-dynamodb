using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for EntityAnalyzer.DetectRedundantExplicitDiscriminator (FDDB103).
/// Feature: unify-prefix-computed-discriminator, Property 6: FDDB103 Redundancy Detection
/// </summary>
public class FDDB103RedundancyPropertyTests
{
    private static readonly MethodInfo DetectRedundantExplicitDiscriminatorMethod =
        typeof(EntityAnalyzer).GetMethod(
            "DetectRedundantExplicitDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo DiagnosticsField =
        typeof(EntityAnalyzer).GetField(
            "_diagnostics",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Creates an EntityAnalyzer instance without calling the constructor,
    /// then initializes the _diagnostics field so ReportDiagnostic can work.
    /// </summary>
    private static object CreateAnalyzerWithDiagnostics()
    {
        var analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
        DiagnosticsField.SetValue(analyzer, new List<Diagnostic>());
        return analyzer;
    }

    /// <summary>
    /// Gets the diagnostics list from the analyzer via reflection.
    /// </summary>
    private static IReadOnlyList<Diagnostic> GetDiagnostics(object analyzer)
    {
        return (IReadOnlyList<Diagnostic>)typeof(EntityAnalyzer)
            .GetProperty("Diagnostics", BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(analyzer)!;
    }

    /// <summary>
    /// Invokes the private instance DetectRedundantExplicitDiscriminator method via reflection.
    /// </summary>
    private static void InvokeDetectRedundantExplicitDiscriminator(object analyzer, EntityModel entity)
    {
        DetectRedundantExplicitDiscriminatorMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates DynamoDB attribute names for key properties.
    /// </summary>
    private static Gen<string> GenAttributeName()
    {
        return Gen.Elements(
            "sk", "pk", "sortKey", "partitionKey", "gsi1sk",
            "range", "hash", "entitySort", "itemType",
            "skValue", "pkValue", "compositeSort");
    }

    /// <summary>
    /// Generates non-null derived discriminator patterns (patterns with literal prefixes).
    /// </summary>
    private static Gen<string> GenNonNullDerivedPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*");
    }

    /// <summary>
    /// Generates patterns that differ from another given pattern.
    /// </summary>
    private static Gen<string> GenDifferentPattern(string excludePattern)
    {
        return GenNonNullDerivedPattern().Where(p => p != excludePattern);
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.4, 6.6**
    /// For any entity with explicit DiscriminatorPattern (not DiscriminatorValue) matching a
    /// key property's attribute name, FDDB103 is emitted when the explicit pattern exactly
    /// matches the derived pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "6")]
    public Property FDDB103Emitted_WhenExplicitPatternMatchesDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from pattern in GenNonNullDerivedPattern()
                          select (attrName, pattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, pattern) = testCase;

                // Arrange: entity with explicit discriminator pattern that exactly matches
                // the derived pattern on the key property it references
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = pattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern),
                        IsAutoDerived = false // Explicit discriminator
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = attrName,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = pattern // Same as explicit
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeDetectRedundantExplicitDiscriminator(analyzer, entity);

                // Assert: FDDB103 should be emitted
                var diagnostics = GetDiagnostics(analyzer);
                var hasFddb103 = diagnostics.Any(d => d.Id == "FDDB103");

                return hasFddb103.ToProperty()
                    .Label($"attrName='{attrName}', pattern='{pattern}', " +
                           $"hasFddb103={hasFddb103}, diagnosticCount={diagnostics.Count}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.4, 6.6**
    /// For any entity with explicit DiscriminatorPattern (not DiscriminatorValue) matching a
    /// key property's attribute name, FDDB103 is NOT emitted when the explicit pattern
    /// differs from the derived pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "6")]
    public Property FDDB103NotEmitted_WhenExplicitPatternDiffersFromDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from derivedPattern in GenNonNullDerivedPattern()
                          from explicitPattern in GenDifferentPattern(derivedPattern)
                          select (attrName, derivedPattern, explicitPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, derivedPattern, explicitPattern) = testCase;

                // Arrange: entity with explicit discriminator pattern that differs from derived
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = explicitPattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(explicitPattern),
                        IsAutoDerived = false
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = attrName,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = derivedPattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeDetectRedundantExplicitDiscriminator(analyzer, entity);

                // Assert: FDDB103 should NOT be emitted (patterns differ)
                var diagnostics = GetDiagnostics(analyzer);
                var hasFddb103 = diagnostics.Any(d => d.Id == "FDDB103");

                return (!hasFddb103).ToProperty()
                    .Label($"attrName='{attrName}', derivedPattern='{derivedPattern}', " +
                           $"explicitPattern='{explicitPattern}', hasFddb103={hasFddb103}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.4, 6.6**
    /// FDDB103 is NOT emitted when the entity uses DiscriminatorValue (ExactMatch strategy)
    /// even if the value happens to match a derived pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "6")]
    public Property FDDB103NotEmitted_WhenUsingDiscriminatorValue()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from pattern in GenNonNullDerivedPattern()
                          select (attrName, pattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, pattern) = testCase;

                // Arrange: entity with DiscriminatorValue (ExactMatch), not DiscriminatorPattern
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        ExactValue = pattern,
                        Pattern = null,
                        Strategy = DiscriminatorStrategy.ExactMatch,
                        IsAutoDerived = false
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = attrName,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = pattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeDetectRedundantExplicitDiscriminator(analyzer, entity);

                // Assert: FDDB103 should NOT be emitted for ExactMatch discriminators
                var diagnostics = GetDiagnostics(analyzer);
                var hasFddb103 = diagnostics.Any(d => d.Id == "FDDB103");

                return (!hasFddb103).ToProperty()
                    .Label($"attrName='{attrName}', pattern='{pattern}', " +
                           $"hasFddb103={hasFddb103} (ExactMatch should not trigger FDDB103)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.4, 6.6**
    /// FDDB103 is NOT emitted when the discriminator is auto-derived (not explicit).
    /// Only explicit discriminators that match derived patterns are considered redundant.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "6")]
    public Property FDDB103NotEmitted_WhenDiscriminatorIsAutoDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from pattern in GenNonNullDerivedPattern()
                          select (attrName, pattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, pattern) = testCase;

                // Arrange: entity with auto-derived discriminator (not explicit)
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = pattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern),
                        IsAutoDerived = true // Auto-derived, not explicit
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = attrName,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = pattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeDetectRedundantExplicitDiscriminator(analyzer, entity);

                // Assert: FDDB103 should NOT be emitted for auto-derived discriminators
                var diagnostics = GetDiagnostics(analyzer);
                var hasFddb103 = diagnostics.Any(d => d.Id == "FDDB103");

                return (!hasFddb103).ToProperty()
                    .Label($"attrName='{attrName}', pattern='{pattern}', " +
                           $"hasFddb103={hasFddb103} (auto-derived should not trigger FDDB103)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.4, 6.6**
    /// FDDB103 is NOT emitted when the discriminator property doesn't match any key property's
    /// attribute name — only key-property-targeting discriminators can be considered redundant.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "6")]
    public Property FDDB103NotEmitted_WhenDiscriminatorPropertyDoesNotMatchKeyAttribute()
    {
        var testCaseGen = from keyAttrName in GenAttributeName()
                          from discPropName in Gen.Elements(
                              "entityType", "type", "discriminator", "kind",
                              "category", "itemClass", "sortType")
                          from pattern in GenNonNullDerivedPattern()
                          where keyAttrName != discPropName
                          select (keyAttrName, discPropName, pattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (keyAttrName, discPropName, pattern) = testCase;

                // Arrange: explicit discriminator targets a different attribute than the key property
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = discPropName, // Points to non-key attribute
                        Pattern = pattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern),
                        IsAutoDerived = false
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = keyAttrName,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = pattern // Same pattern, but different attribute
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeDetectRedundantExplicitDiscriminator(analyzer, entity);

                // Assert: FDDB103 should NOT be emitted because DiscriminatorProperty
                // doesn't match the key property's attribute name
                var diagnostics = GetDiagnostics(analyzer);
                var hasFddb103 = diagnostics.Any(d => d.Id == "FDDB103");

                return (!hasFddb103).ToProperty()
                    .Label($"keyAttrName='{keyAttrName}', discPropName='{discPropName}', " +
                           $"pattern='{pattern}', hasFddb103={hasFddb103}");
            });
    }
}
