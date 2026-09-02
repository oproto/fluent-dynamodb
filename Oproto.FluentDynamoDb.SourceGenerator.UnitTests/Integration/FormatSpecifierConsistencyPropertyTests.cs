using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Utilities;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Property-based tests for cross-operation consistency of format specifiers.
/// Validates that all three operation paths (Keys builder, Put mapper, Update recomputation)
/// produce identical string values for the same format string and typed inputs.
///
/// Since all three paths ultimately call string.Format(CultureInfo.InvariantCulture, format, typedArgs),
/// this test verifies the formula is consistent across paths by simulating each path's logic.
///
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
/// </summary>
[Trait("Feature", "computed-field-format-specifiers")]
[Trait("Property", "9")]
[Trait("Category", "Integration")]
public class FormatSpecifierConsistencyPropertyTests
{
    #region Generators

    /// <summary>
    /// Represents a typed value with a compatible format specifier.
    /// </summary>
    private record TypedValueWithSpecifier(object Value, string FormatSpecifier, string TypeName);

    /// <summary>
    /// Generates an integer value with a compatible integer format specifier.
    /// Validates: Requirement 5.2 (int with D4, zero-padding)
    /// </summary>
    private static Gen<TypedValueWithSpecifier> IntWithSpecifierGen =>
        from value in Gen.Choose(0, 99999)
        from specifier in Gen.Elements("D4", "D6", "D2", "G")
        select new TypedValueWithSpecifier(value, specifier, "int");

    /// <summary>
    /// Generates a decimal value with a compatible numeric format specifier.
    /// </summary>
    private static Gen<TypedValueWithSpecifier> DecimalWithSpecifierGen =>
        from intPart in Gen.Choose(0, 9999)
        from fracPart in Gen.Choose(0, 99)
        from specifier in Gen.Elements("N2", "F2", "G", "F4")
        select new TypedValueWithSpecifier((decimal)intPart + (decimal)fracPart / 100m, specifier, "decimal");

    /// <summary>
    /// Generates a string value (uses simple placeholder without format specifier).
    /// </summary>
    private static Gen<TypedValueWithSpecifier> StringValueGen =>
        from value in Gen.Elements("Alpha", "Beta", "Category", "electronics", "TaskName", "id123", "Active")
        select new TypedValueWithSpecifier(value, string.Empty, "string");

    /// <summary>
    /// Generates a typed value from any supported type.
    /// </summary>
    private static Gen<TypedValueWithSpecifier> TypedValueGen =>
        Gen.OneOf(IntWithSpecifierGen, DecimalWithSpecifierGen, StringValueGen);

    /// <summary>
    /// Generates a list of 1-3 typed values ensuring at least one has a format specifier.
    /// </summary>
    private static Gen<List<TypedValueWithSpecifier>> TypedValueListWithSpecifiersGen =>
        from forcedSpecifier in Gen.OneOf(IntWithSpecifierGen, DecimalWithSpecifierGen)
        from additionalCount in Gen.Choose(0, 2)
        from additionalValues in Gen.ListOf(additionalCount, TypedValueGen)
        select new List<TypedValueWithSpecifier> { forcedSpecifier }.Concat(additionalValues).ToList();

    /// <summary>
    /// Separator characters used between placeholders.
    /// </summary>
    private static Gen<char> SeparatorGen => Gen.Elements('#', '-', '_');

    /// <summary>
    /// Builds a format string and typed argument array from a list of typed values.
    /// Ensures at least one placeholder has a format specifier.
    /// </summary>
    private static Gen<(string Format, object[] Args)> FormatWithArgsGen =>
        from values in TypedValueListWithSpecifiersGen
        from separator in SeparatorGen
        let formatParts = values.Select((v, i) =>
            string.IsNullOrEmpty(v.FormatSpecifier)
                ? $"{{{i}}}"
                : $"{{{i}:{v.FormatSpecifier}}}")
        let formatString = string.Join(separator.ToString(), formatParts)
        let args = values.Select(v => v.Value).ToArray()
        select (formatString, args);

    #endregion

    #region Path Simulation Methods

    /// <summary>
    /// Simulates the Keys builder path:
    /// For indices with format specifiers, passes typed value cast to object.
    /// For indices without specifiers, passes ToString() result (pre-stringification).
    /// Uses CultureInfo.InvariantCulture when any specifier is present.
    /// </summary>
    private static string SimulateKeysBuilderPath(string format, object[] typedValues)
    {
        var specifierIndices = FormatSpecifierHelper.GetIndicesWithFormatSpecifiers(format);

        // Build args: indices with specifiers get typed value as (object),
        // indices without specifiers get pre-stringified value
        var args = new object[typedValues.Length];
        for (int i = 0; i < typedValues.Length; i++)
        {
            if (specifierIndices.Contains(i))
            {
                // Typed value cast to object — let string.Format apply IFormattable
                args[i] = typedValues[i];
            }
            else
            {
                // Pre-stringification (existing GetValueExpression behavior)
                args[i] = typedValues[i]?.ToString() ?? string.Empty;
            }
        }

        if (specifierIndices.Count > 0)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        else
        {
            return string.Format(format, args);
        }
    }

    /// <summary>
    /// Simulates the Put mapper path:
    /// Passes typed property values directly to string.Format.
    /// Uses CultureInfo.InvariantCulture when format specifiers are detected.
    /// </summary>
    private static string SimulatePutMapperPath(string format, object[] typedValues)
    {
        if (FormatSpecifierHelper.HasAnyFormatSpecifier(format))
        {
            return string.Format(CultureInfo.InvariantCulture, format, typedValues);
        }
        else
        {
            return string.Format(format, typedValues);
        }
    }

    /// <summary>
    /// Simulates the Update recomputation path:
    /// Detects format specifiers → passes typed values → uses InvariantCulture.
    /// When no specifiers, pre-stringifies values before string.Format.
    /// </summary>
    private static string SimulateUpdateRecomputationPath(string format, object[] typedValues)
    {
        if (FormatSpecifierHelper.HasAnyFormatSpecifier(format))
        {
            // Format specifiers present: pass typed values with InvariantCulture
            var args = typedValues.Select(v => v ?? (object)string.Empty).ToArray();
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
        else
        {
            // No specifiers: pre-stringify
            var args = typedValues.Select(v => (object)(v?.ToString() ?? string.Empty)).ToArray();
            return string.Format(format, args);
        }
    }

    #endregion

    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    ///
    /// Property 9: Cross-Operation Consistency
    ///
    /// For any computed field with format specifiers and any set of typed source values,
    /// the output of the Keys builder path, the Put/ToDynamoDb path, and the Update
    /// recomputation path SHALL produce identical string values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllThreePaths_ProduceIdenticalOutput_ForSameFormatAndTypedInputs()
    {
        return Prop.ForAll(
            FormatWithArgsGen.ToArbitrary(),
            input =>
            {
                var (format, args) = input;

                // Simulate all three operation paths
                var keysResult = SimulateKeysBuilderPath(format, args);
                var putResult = SimulatePutMapperPath(format, args);
                var updateResult = SimulateUpdateRecomputationPath(format, args);

                // All three must be identical
                var keysEqualsPut = keysResult == putResult;
                var putEqualsUpdate = putResult == updateResult;
                var allEqual = keysEqualsPut && putEqualsUpdate;

                return allEqual.ToProperty()
                    .Label($"Format='{format}', " +
                           $"Keys='{keysResult}', Put='{putResult}', Update='{updateResult}'. " +
                           $"Keys==Put: {keysEqualsPut}, Put==Update: {putEqualsUpdate}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    ///
    /// Property 9 (supplementary): All three paths produce the same result as calling
    /// string.Format(CultureInfo.InvariantCulture, format, typedArgs) directly,
    /// which is the canonical expected value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllThreePaths_MatchCanonicalStringFormatInvariantCulture()
    {
        return Prop.ForAll(
            FormatWithArgsGen.ToArbitrary(),
            input =>
            {
                var (format, args) = input;

                // The canonical expected result
                var expected = string.Format(CultureInfo.InvariantCulture, format, args);

                // Simulate all three paths
                var keysResult = SimulateKeysBuilderPath(format, args);
                var putResult = SimulatePutMapperPath(format, args);
                var updateResult = SimulateUpdateRecomputationPath(format, args);

                var keysMatch = keysResult == expected;
                var putMatch = putResult == expected;
                var updateMatch = updateResult == expected;
                var allMatch = keysMatch && putMatch && updateMatch;

                return allMatch.ToProperty()
                    .Label($"Format='{format}', Expected='{expected}', " +
                           $"Keys='{keysResult}' (match={keysMatch}), " +
                           $"Put='{putResult}' (match={putMatch}), " +
                           $"Update='{updateResult}' (match={updateMatch})");
            });
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.4**
    ///
    /// Property 9 (InvariantCulture determinism): All three paths produce the same output
    /// regardless of repeated invocations, confirming deterministic behavior with InvariantCulture.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllThreePaths_AreDeterministic_AcrossMultipleInvocations()
    {
        return Prop.ForAll(
            FormatWithArgsGen.ToArbitrary(),
            input =>
            {
                var (format, args) = input;

                // Run each path twice
                var keys1 = SimulateKeysBuilderPath(format, args);
                var keys2 = SimulateKeysBuilderPath(format, args);
                var put1 = SimulatePutMapperPath(format, args);
                var put2 = SimulatePutMapperPath(format, args);
                var update1 = SimulateUpdateRecomputationPath(format, args);
                var update2 = SimulateUpdateRecomputationPath(format, args);

                var keysDeterministic = keys1 == keys2;
                var putDeterministic = put1 == put2;
                var updateDeterministic = update1 == update2;
                var allDeterministic = keysDeterministic && putDeterministic && updateDeterministic;

                return allDeterministic.ToProperty()
                    .Label($"Format='{format}', " +
                           $"Keys deterministic={keysDeterministic}, " +
                           $"Put deterministic={putDeterministic}, " +
                           $"Update deterministic={updateDeterministic}");
            });
    }
}
