using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for FDDB101 Conflict Detection (ValidateExplicitVsDerivedDiscriminator).
/// Feature: unify-prefix-computed-discriminator, Property 5: FDDB101 Conflict Detection
/// </summary>
public class FDDB101ConflictPropertyTests
{
    private static readonly MethodInfo ValidateExplicitVsDerivedDiscriminatorMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ValidateExplicitVsDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo DiagnosticsField =
        typeof(EntityAnalyzer).GetField(
            "_diagnostics",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// The Diagnostic type from Microsoft.CodeAnalysis (loaded via reflection
    /// since the type is not directly referenced in this test project).
    /// </summary>
    private static readonly Type DiagnosticType =
        DiagnosticsField.FieldType.GetGenericArguments()[0];

    /// <summary>
    /// The Id property on Diagnostic (returns the diagnostic code like "FDDB101").
    /// </summary>
    private static readonly PropertyInfo DiagnosticIdProperty =
        DiagnosticType.GetProperty("Id")!;

    /// <summary>
    /// Creates an EntityAnalyzer instance without calling the constructor,
    /// then initializes the _diagnostics field so ReportDiagnostic works.
    /// </summary>
    private static object CreateAnalyzerWithDiagnostics()
    {
        var analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
        // Create a List<Diagnostic> using reflection since we don't directly reference the type
        var listType = typeof(List<>).MakeGenericType(DiagnosticType);
        var list = Activator.CreateInstance(listType)!;
        DiagnosticsField.SetValue(analyzer, list);
        return analyzer;
    }

    /// <summary>
    /// Gets diagnostic IDs from the analyzer instance's diagnostics list.
    /// </summary>
    private static List<string> GetDiagnosticIds(object analyzer)
    {
        var list = (IList)DiagnosticsField.GetValue(analyzer)!;
        var ids = new List<string>();
        foreach (var diagnostic in list)
        {
            var id = (string)DiagnosticIdProperty.GetValue(diagnostic)!;
            ids.Add(id);
        }
        return ids;
    }

    /// <summary>
    /// Invokes the private instance ValidateExplicitVsDerivedDiscriminator method via reflection.
    /// </summary>
    private static void InvokeValidateExplicitVsDerivedDiscriminator(object analyzer, EntityModel entity)
    {
        ValidateExplicitVsDerivedDiscriminatorMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates DynamoDB attribute names for key properties.
    /// </summary>
    private static Gen<string> GenAttributeName()
    {
        return Gen.Elements(
            "sk", "sortKey", "SK", "pk", "partitionKey", "PK",
            "gsi1sk", "range", "rangeKey", "entitySort",
            "hash", "hashKey", "entityPk");
    }

    /// <summary>
    /// Generates non-null derived discriminator patterns (patterns that start with a literal prefix).
    /// </summary>
    private static Gen<string> GenDerivedPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*");
    }

    /// <summary>
    /// Generates explicit patterns that are guaranteed to differ from the derived pattern.
    /// </summary>
    private static Gen<string> GenDifferentExplicitPattern(string derivedPattern)
    {
        var candidates = new[]
        {
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*", "WRONG#*", "BAD#*"
        };
        var filtered = candidates.Where(p => p != derivedPattern).ToArray();
        return Gen.Elements(filtered);
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.4, 4.5**
    /// For any entity with explicit DiscriminatorPattern matching a key property's attribute name
    /// where the derived pattern is non-null and differs from the explicit pattern,
    /// FDDB101 SHALL be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "5")]
    public Property FDDB101_Emitted_WhenExplicitPatternDiffersFromDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from derivedPattern in GenDerivedPattern()
                          from explicitPattern in GenDifferentExplicitPattern("")
                          where explicitPattern != derivedPattern // Ensure they actually differ
                          select (attrName, derivedPattern, explicitPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, derivedPattern, explicitPattern) = testCase;

                // Arrange: entity with explicit discriminator pointing at a key property
                // whose derived pattern differs from the explicit pattern
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = explicitPattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(explicitPattern),
                        IsAutoDerived = false // Explicit discriminator
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
                InvokeValidateExplicitVsDerivedDiscriminator(analyzer, entity);

                // Assert: FDDB101 should be emitted
                var diagnosticIds = GetDiagnosticIds(analyzer);
                var hasFDDB101 = diagnosticIds.Contains("FDDB101");

                return hasFDDB101.ToProperty()
                    .Label($"attrName='{attrName}', explicit='{explicitPattern}', " +
                           $"derived='{derivedPattern}', hasFDDB101={hasFDDB101}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.4, 4.5**
    /// For any entity with explicit DiscriminatorPattern matching a key property's attribute name
    /// where the derived pattern is non-null and equals the explicit pattern,
    /// FDDB101 SHALL NOT be emitted (patterns match, no conflict).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "5")]
    public Property FDDB101_NotEmitted_WhenExplicitPatternMatchesDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from pattern in GenDerivedPattern()
                          select (attrName, pattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, pattern) = testCase;

                // Arrange: entity with explicit discriminator that matches derived exactly
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = pattern, // Same as derived
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern),
                        IsAutoDerived = false
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
                InvokeValidateExplicitVsDerivedDiscriminator(analyzer, entity);

                // Assert: FDDB101 should NOT be emitted
                var diagnosticIds = GetDiagnosticIds(analyzer);
                var hasFDDB101 = diagnosticIds.Contains("FDDB101");

                return (!hasFDDB101).ToProperty()
                    .Label($"attrName='{attrName}', pattern='{pattern}', " +
                           $"hasFDDB101={hasFDDB101} (should be false)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.4, 4.5**
    /// For any entity with explicit DiscriminatorPattern matching a key property's attribute name
    /// where the derived pattern is null (trivial key format "{0}"),
    /// FDDB101 SHALL NOT be emitted (explicit supplements rather than contradicts).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "5")]
    public Property FDDB101_NotEmitted_WhenDerivedPatternIsNull()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from explicitPattern in GenDerivedPattern()
                          select (attrName, explicitPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, explicitPattern) = testCase;

                // Arrange: entity with explicit discriminator but key has null derived pattern
                // (trivial format "{0}" → no discrimination capability)
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
                            DerivedDiscriminatorPattern = null // Trivial key, no derived pattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeValidateExplicitVsDerivedDiscriminator(analyzer, entity);

                // Assert: FDDB101 should NOT be emitted
                var diagnosticIds = GetDiagnosticIds(analyzer);
                var hasFDDB101 = diagnosticIds.Contains("FDDB101");

                return (!hasFDDB101).ToProperty()
                    .Label($"attrName='{attrName}', explicit='{explicitPattern}', " +
                           $"derived=null, hasFDDB101={hasFDDB101} (should be false)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.1, 4.4, 4.5**
    /// For any entity with auto-derived discriminator (not explicit),
    /// FDDB101 SHALL NOT be emitted regardless of patterns.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "5")]
    public Property FDDB101_NotEmitted_WhenDiscriminatorIsAutoDerived()
    {
        var testCaseGen = from attrName in GenAttributeName()
                          from derivedPattern in GenDerivedPattern()
                          from explicitPattern in GenDerivedPattern()
                          where explicitPattern != derivedPattern
                          select (attrName, derivedPattern, explicitPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (attrName, derivedPattern, explicitPattern) = testCase;

                // Arrange: entity with auto-derived discriminator (should skip validation)
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = attrName,
                        Pattern = explicitPattern,
                        Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(explicitPattern),
                        IsAutoDerived = true // Auto-derived, not explicit
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
                InvokeValidateExplicitVsDerivedDiscriminator(analyzer, entity);

                // Assert: FDDB101 should NOT be emitted for auto-derived discriminators
                var diagnosticIds = GetDiagnosticIds(analyzer);
                var hasFDDB101 = diagnosticIds.Contains("FDDB101");

                return (!hasFDDB101).ToProperty()
                    .Label($"attrName='{attrName}', explicit='{explicitPattern}', " +
                           $"derived='{derivedPattern}', isAutoDerived=true, " +
                           $"hasFDDB101={hasFDDB101} (should be false)");
            });
    }
}
