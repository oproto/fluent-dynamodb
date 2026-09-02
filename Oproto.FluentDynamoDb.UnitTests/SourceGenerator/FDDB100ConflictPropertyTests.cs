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
/// Property-based tests for FDDB100 Conflict Detection (ValidatePrefixFormatConsistency).
/// Feature: unify-prefix-computed-discriminator, Property 4: FDDB100 Conflict Detection
/// </summary>
public class FDDB100ConflictPropertyTests
{
    private static readonly MethodInfo ValidatePrefixFormatConsistencyMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ValidatePrefixFormatConsistency",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo DiagnosticsField =
        typeof(EntityAnalyzer).GetField(
            "_diagnostics",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Creates an EntityAnalyzer instance without calling the constructor,
    /// then initializes the _diagnostics field so ReportDiagnostic works.
    /// </summary>
    private static object CreateAnalyzerWithDiagnostics()
    {
        var analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
        // Initialize the _diagnostics list since we bypassed the constructor
        DiagnosticsField.SetValue(analyzer, new List<Diagnostic>());
        return analyzer;
    }

    /// <summary>
    /// Gets the diagnostics list from the analyzer instance.
    /// </summary>
    private static List<Diagnostic> GetDiagnostics(object analyzer)
    {
        return (List<Diagnostic>)DiagnosticsField.GetValue(analyzer)!;
    }

    /// <summary>
    /// Invokes the private instance ValidatePrefixFormatConsistency method via reflection.
    /// </summary>
    private static void InvokeValidatePrefixFormatConsistency(object analyzer, EntityModel entity)
    {
        ValidatePrefixFormatConsistencyMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates non-empty prefix strings for key properties.
    /// </summary>
    private static Gen<string> GenPrefix()
    {
        return Gen.Elements(
            "ORDER", "USER", "CUSTOMER", "TENANT",
            "INVOICE", "PRODUCT", "EVENT", "SESSION",
            "ACCT", "META", "LINE", "DETAIL");
    }

    /// <summary>
    /// Generates separator strings (including the default "#").
    /// </summary>
    private static Gen<string> GenSeparator()
    {
        return Gen.Elements("#", "_", ":", "-", "|", "~");
    }

    /// <summary>
    /// Generates a format string that starts with the expected prefix+separator (no conflict).
    /// </summary>
    private static Gen<string> GenMatchingFormat(string prefix, string separator)
    {
        var expectedStart = $"{prefix}{separator}";
        return Gen.Elements(
            $"{expectedStart}{{0}}",
            $"{expectedStart}{{0}}#{{1}}",
            $"{expectedStart}item#{{0}}",
            $"{expectedStart}{{0}}#{{1}}#{{2}}");
    }

    /// <summary>
    /// Generates a format string that does NOT start with the expected prefix+separator (conflict).
    /// </summary>
    private static Gen<string> GenConflictingFormat(string prefix, string separator)
    {
        var expectedStart = $"{prefix}{separator}";
        // Generate formats that definitely don't start with expectedStart
        var candidates = new[]
        {
            "WRONG#{0}", "OTHER_{0}", "{0}#suffix", "BAD#{0}#{1}",
            "TENANT#{0}", "CUSTOM:{0}", "X{0}", "abc#{0}",
            $"DIFF{separator}{{0}}", $"NOT{prefix}{{0}}"
        }.Where(f => !f.StartsWith(expectedStart, StringComparison.Ordinal)).ToArray();

        return Gen.Elements(candidates);
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.4, 3.5, 3.6, 3.7**
    /// For any key with non-empty Prefix and explicit Format that does NOT start with
    /// "{Prefix}{Separator}", FDDB100 SHALL be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "4")]
    public Property FDDB100_Emitted_WhenFormatDoesNotStartWithPrefixSeparator()
    {
        var testCaseGen = from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from format in GenConflictingFormat(prefix, separator)
                          from isPartitionKey in Gen.Elements(true, false)
                          select (prefix, separator, format, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (prefix, separator, format, isPartitionKey) = testCase;

                // Arrange: key property with non-empty Prefix and ComputedKey with custom Format
                // where Format does NOT start with "{Prefix}{Separator}"
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = isPartitionKey ? "pk" : "sk",
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            KeyFormat = new KeyFormatModel
                            {
                                Prefix = prefix,
                                Separator = separator
                            },
                            ComputedKey = new ComputedKeyModel
                            {
                                SourceProperties = new[] { "Prop1", "Prop2" },
                                Format = format
                            }
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeValidatePrefixFormatConsistency(analyzer, entity);

                // Assert: FDDB100 should be emitted
                var diagnostics = GetDiagnostics(analyzer);
                var hasFDDB100 = diagnostics.Any(d => d.Id == "FDDB100");

                return hasFDDB100.ToProperty()
                    .Label($"prefix='{prefix}', separator='{separator}', " +
                           $"format='{format}', isPartitionKey={isPartitionKey}, " +
                           $"hasFDDB100={hasFDDB100}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.4, 3.5, 3.6, 3.7**
    /// For any key with non-empty Prefix and explicit Format that DOES start with
    /// "{Prefix}{Separator}", FDDB100 SHALL NOT be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "4")]
    public Property FDDB100_NotEmitted_WhenFormatStartsWithPrefixSeparator()
    {
        var testCaseGen = from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from format in GenMatchingFormat(prefix, separator)
                          from isPartitionKey in Gen.Elements(true, false)
                          select (prefix, separator, format, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (prefix, separator, format, isPartitionKey) = testCase;

                // Arrange: key property with non-empty Prefix and ComputedKey with custom Format
                // where Format DOES start with "{Prefix}{Separator}"
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = isPartitionKey ? "pk" : "sk",
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            KeyFormat = new KeyFormatModel
                            {
                                Prefix = prefix,
                                Separator = separator
                            },
                            ComputedKey = new ComputedKeyModel
                            {
                                SourceProperties = new[] { "Prop1", "Prop2" },
                                Format = format
                            }
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeValidatePrefixFormatConsistency(analyzer, entity);

                // Assert: FDDB100 should NOT be emitted
                var diagnostics = GetDiagnostics(analyzer);
                var hasFDDB100 = diagnostics.Any(d => d.Id == "FDDB100");

                return (!hasFDDB100).ToProperty()
                    .Label($"prefix='{prefix}', separator='{separator}', " +
                           $"format='{format}', isPartitionKey={isPartitionKey}, " +
                           $"hasFDDB100={hasFDDB100} (should be false)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.4, 3.5, 3.6, 3.7**
    /// For any key with null or empty Prefix, FDDB100 SHALL NOT be emitted
    /// regardless of the ComputedAttribute Format value.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "4")]
    public Property FDDB100_NotEmitted_WhenPrefixIsNullOrEmpty()
    {
        var testCaseGen = from nullPrefix in Gen.Elements(true, false) // true = null, false = empty
                          from format in Gen.Elements(
                              "WRONG#{0}", "OTHER_{0}", "{0}#suffix",
                              "TENANT#{0}", "CUSTOM:{0}")
                          from isPartitionKey in Gen.Elements(true, false)
                          select (nullPrefix, format, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (nullPrefix, format, isPartitionKey) = testCase;

                // Arrange: key property with null or empty Prefix
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = isPartitionKey ? "pk" : "sk",
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            KeyFormat = new KeyFormatModel
                            {
                                Prefix = nullPrefix ? null : "",
                                Separator = "#"
                            },
                            ComputedKey = new ComputedKeyModel
                            {
                                SourceProperties = new[] { "Prop1" },
                                Format = format
                            }
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeValidatePrefixFormatConsistency(analyzer, entity);

                // Assert: FDDB100 should NOT be emitted
                var diagnostics = GetDiagnostics(analyzer);
                var hasFDDB100 = diagnostics.Any(d => d.Id == "FDDB100");

                return (!hasFDDB100).ToProperty()
                    .Label($"prefix={(nullPrefix ? "null" : "''")}, " +
                           $"format='{format}', isPartitionKey={isPartitionKey}, " +
                           $"hasFDDB100={hasFDDB100} (should be false)");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.4, 3.5, 3.6, 3.7**
    /// For any key with non-empty Prefix but no custom format (ComputedKey is null or
    /// HasCustomFormat is false), FDDB100 SHALL NOT be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "4")]
    public Property FDDB100_NotEmitted_WhenNoCustomFormat()
    {
        var testCaseGen = from prefix in GenPrefix()
                          from separator in GenSeparator()
                          from hasComputedKey in Gen.Elements(true, false) // true = ComputedKey with null Format, false = no ComputedKey
                          from isPartitionKey in Gen.Elements(true, false)
                          select (prefix, separator, hasComputedKey, isPartitionKey);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (prefix, separator, hasComputedKey, isPartitionKey) = testCase;

                // Arrange: key property with Prefix but no explicit custom format
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "KeyProp",
                            AttributeName = isPartitionKey ? "pk" : "sk",
                            IsPartitionKey = isPartitionKey,
                            IsSortKey = !isPartitionKey,
                            KeyFormat = new KeyFormatModel
                            {
                                Prefix = prefix,
                                Separator = separator
                            },
                            ComputedKey = hasComputedKey
                                ? new ComputedKeyModel
                                {
                                    SourceProperties = new[] { "Prop1" },
                                    Format = null // No custom format
                                }
                                : null // No ComputedKey at all
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzerWithDiagnostics();
                InvokeValidatePrefixFormatConsistency(analyzer, entity);

                // Assert: FDDB100 should NOT be emitted
                var diagnostics = GetDiagnostics(analyzer);
                var hasFDDB100 = diagnostics.Any(d => d.Id == "FDDB100");

                return (!hasFDDB100).ToProperty()
                    .Label($"prefix='{prefix}', separator='{separator}', " +
                           $"hasComputedKey={hasComputedKey}, isPartitionKey={isPartitionKey}, " +
                           $"hasFDDB100={hasFDDB100} (should be false)");
            });
    }
}
