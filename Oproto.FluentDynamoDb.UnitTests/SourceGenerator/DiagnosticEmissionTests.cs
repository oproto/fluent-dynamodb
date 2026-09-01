using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for diagnostic emissions (FDDB100–FDDB103).
/// Validates Requirements 3.1, 3.5, 3.6, 3.7, 4.1, 4.5, 5.1, 5.6, 6.1, 6.6.
/// </summary>
public class DiagnosticEmissionTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _validatePrefixFormatConsistency;
    private readonly MethodInfo _validateExplicitVsDerivedDiscriminator;
    private readonly MethodInfo _detectRedundantExplicitDiscriminator;
    private readonly FieldInfo _diagnosticsField;

    public DiagnosticEmissionTests()
    {
        _analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));

        // Initialize the _diagnostics field since GetUninitializedObject skips field initializers
        _diagnosticsField = typeof(EntityAnalyzer).GetField(
            "_diagnostics", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _diagnosticsField.SetValue(_analyzer, new List<Diagnostic>());

        _validatePrefixFormatConsistency = typeof(EntityAnalyzer).GetMethod(
            "ValidatePrefixFormatConsistency",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        _validateExplicitVsDerivedDiscriminator = typeof(EntityAnalyzer).GetMethod(
            "ValidateExplicitVsDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        _detectRedundantExplicitDiscriminator = typeof(EntityAnalyzer).GetMethod(
            "DetectRedundantExplicitDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private List<Diagnostic> GetDiagnostics()
    {
        return (List<Diagnostic>)_diagnosticsField.GetValue(_analyzer)!;
    }

    private void ClearDiagnostics()
    {
        GetDiagnostics().Clear();
    }

    private void InvokeValidatePrefixFormatConsistency(EntityModel entity)
    {
        _validatePrefixFormatConsistency.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeValidateExplicitVsDerivedDiscriminator(EntityModel entity)
    {
        _validateExplicitVsDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });
    }

    private void InvokeDetectRedundantExplicitDiscriminator(EntityModel entity)
    {
        _detectRedundantExplicitDiscriminator.Invoke(_analyzer, new object[] { entity });
    }

    // ===== FDDB100 Tests =====

    [Fact]
    public void FDDB100_Emitted_WhenPrefixDoesNotMatchFormatStart()
    {
        // Arrange: Prefix="ORDER" but Format="TENANT#{0}" — the format doesn't start with "ORDER#"
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" },
                    ComputedKey = new ComputedKeyModel
                    {
                        Format = "TENANT#{0}",
                        SourceProperties = new[] { "TenantId" }
                    }
                }
            }
        };

        // Act
        InvokeValidatePrefixFormatConsistency(entity);

        // Assert
        var diagnostics = GetDiagnostics();
        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("FDDB100");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void FDDB100_NotEmitted_WhenPrefixMatchesFormatStart()
    {
        // Arrange: Prefix="ORDER" and Format="ORDER#{0}" — format starts with "ORDER#"
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" },
                    ComputedKey = new ComputedKeyModel
                    {
                        Format = "ORDER#{0}#{1}",
                        SourceProperties = new[] { "CustomerId", "OrderId" }
                    }
                }
            }
        };

        // Act
        InvokeValidatePrefixFormatConsistency(entity);

        // Assert
        GetDiagnostics().Should().BeEmpty();
    }

    [Fact]
    public void FDDB100_NotEmitted_WhenNoPrefixOrNoCustomFormat()
    {
        // Arrange: No prefix on key — should never emit FDDB100
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Properties = new[]
            {
                // Property with no prefix
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = null, Separator = "#" },
                    ComputedKey = new ComputedKeyModel
                    {
                        Format = "ANYTHING#{0}",
                        SourceProperties = new[] { "Id" }
                    }
                },
                // Property with prefix but no custom format (Format is null)
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel { Prefix = "LINE", Separator = "#" },
                    ComputedKey = new ComputedKeyModel
                    {
                        Format = null,
                        Separator = "#",
                        SourceProperties = new[] { "LineId" }
                    }
                }
            }
        };

        // Act
        InvokeValidatePrefixFormatConsistency(entity);

        // Assert
        GetDiagnostics().Should().BeEmpty();
    }

    // ===== FDDB101 Tests =====

    [Fact]
    public void FDDB101_Emitted_WhenExplicitPatternDiffersFromDerived()
    {
        // Arrange: Explicit pattern "USER#*" but derived pattern on the same attribute is "ORDER#*"
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "USER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                IsAutoDerived = false
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        // Act
        InvokeValidateExplicitVsDerivedDiscriminator(entity);

        // Assert
        var diagnostics = GetDiagnostics();
        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("FDDB101");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void FDDB101_NotEmitted_WhenDerivedIsNull_TrivialKey()
    {
        // Arrange: Key has trivial format "{0}" so DerivedDiscriminatorPattern is null.
        // The explicit pattern supplements rather than contradicts.
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                IsAutoDerived = false
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = null // trivial key — derived is "*"
                }
            }
        };

        // Act
        InvokeValidateExplicitVsDerivedDiscriminator(entity);

        // Assert
        GetDiagnostics().Should().BeEmpty();
    }

    // ===== FDDB102 Tests =====

    [Fact]
    public void FDDB102_NotEmitted_ForNonTautologicalAutoDerivedOverlap_NotForExplicitOverlap()
    {
        // Arrange: Two entities with overlapping auto-derived patterns
        var entityA = new EntityModel
        {
            ClassName = "Order",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        var entityB = new EntityModel
        {
            ClassName = "OrderLine",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*#LINE#*",
                Strategy = DiscriminatorStrategy.Complex,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*#LINE#*"
                }
            }
        };

        // Act — use PatternOverlapAnalyzer directly (FDDB102 is emitted there)
        var tableEntities = new List<EntityModel> { entityA, entityB };
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — After Bug 3 fix: FDDB102 should NOT be emitted for auto-derived overlap
        // when the exclusion is non-tautological (ORDER#* vs ORDER#*#LINE#* is resolved
        // by IndexOf check). DISC005 should be emitted instead.
        diagnostics.Should().NotContain(d => d.Id == "FDDB102");
        diagnostics.Should().Contain(d => d.Id == "DISC005");

        // Now test that explicit overlap does NOT emit FDDB102
        var entityC = new EntityModel
        {
            ClassName = "ExplicitOrder",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                IsAutoDerived = false // explicit
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        var entityD = new EntityModel
        {
            ClassName = "ExplicitOrderLine",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*#LINE#*",
                Strategy = DiscriminatorStrategy.Complex,
                IsAutoDerived = false // explicit
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*#LINE#*"
                }
            }
        };

        var explicitEntities = new List<EntityModel> { entityC, entityD };
        var explicitDiagnostics = PatternOverlapAnalyzer.Analyze(explicitEntities);

        // Assert — FDDB102 should NOT be emitted for explicit overlap
        explicitDiagnostics.Should().NotContain(d => d.Id == "FDDB102");
    }

    // ===== FDDB103 Tests =====

    [Fact]
    public void FDDB103_Emitted_WhenExplicitMatchesDerivedExactly()
    {
        // Arrange: Explicit pattern "ORDER#*" matches derived pattern "ORDER#*" exactly
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                IsAutoDerived = false
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        // Act
        InvokeDetectRedundantExplicitDiscriminator(entity);

        // Assert
        var diagnostics = GetDiagnostics();
        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("FDDB103");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void FDDB103_NotEmitted_ForDiscriminatorValue_ExactMatch()
    {
        // Arrange: DiscriminatorValue (ExactMatch strategy) — never triggers FDDB103
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "entityType",
                ExactValue = "ORDER",
                Strategy = DiscriminatorStrategy.ExactMatch,
                IsAutoDerived = false
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "entityType",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER" // Even if it matches exactly
                }
            }
        };

        // Act
        InvokeDetectRedundantExplicitDiscriminator(entity);

        // Assert — ExactMatch strategy is never flagged for redundancy
        GetDiagnostics().Should().BeEmpty();
    }
}
