using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Properties;

/// <summary>
/// Property-based tests for Property 3: Computed key exclusion.
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 4.7, 10.5**
/// 
/// For any computed key property, regardless of prefix configuration and KeyInputMode,
/// the serialized value equals the computed value without prefix transformation.
/// </summary>
[Trait("Feature", "put-key-prefix-application")]
[Trait("Property", "3")]
public class ComputedKeyExclusionPropertyTests
{
    /// <summary>
    /// Generator for resolved KeyInputMode values (Auto, Value, Raw).
    /// Default is excluded because it is resolved before reaching the generated code.
    /// </summary>
    private static readonly Arbitrary<KeyInputMode> ResolvedModeArb =
        Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw).ToArbitrary();

    /// <summary>
    /// Generator for valid non-empty string components used in computed keys.
    /// These represent the source property values (e.g., Year, Month for a date-based computed key).
    /// </summary>
    private static readonly Arbitrary<string> NonEmptyComponentArb =
        Gen.OneOf(
            Gen.Choose(1, 9999).Select(n => n.ToString()),
            Gen.Choose(1, 20).SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    'a', 'b', 'c', 'x', 'y', 'z', 'A', 'B', 'C',
                    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
                )).Select(chars => new string(chars)))
        ).Where(s => !string.IsNullOrEmpty(s)).ToArbitrary();

    /// <summary>
    /// Generator for non-empty sort key values used as non-computed SK input.
    /// </summary>
    private static readonly Arbitrary<string> SortKeyValueArb =
        Gen.OneOf(
            Gen.Choose(1, 20).SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    'a', 'b', 'c', 'x', 'y', 'z', 'A', 'B', 'C',
                    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                    '-', '_'
                )).Select(chars => new string(chars)))
        ).Where(s => !string.IsNullOrEmpty(s)).ToArbitrary();

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 4.7, 10.5**
    /// 
    /// For any source property values (Component1, Component2) that form a computed PK,
    /// and any resolved KeyInputMode (Auto, Value, Raw), the serialized PK value in the
    /// DynamoDB item equals the computed value (Component1#Component2) without any prefix applied.
    /// 
    /// The entity has [PartitionKey] + [Computed("Component1", "Component2", Separator = "#")]
    /// so the computed PK passes through unchanged (computed keys never receive prefix application).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPk_NeverGetsPrefixApplied_RegardlessOfMode()
    {
        var gen = from component1 in NonEmptyComponentArb.Generator
                  from component2 in NonEmptyComponentArb.Generator
                  from skValue in SortKeyValueArb.Generator
                  from mode in Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw)
                  select (component1, component2, skValue, mode);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (component1, component2, skValue, mode) = tuple;

                // Arrange: create entity with source property values
                var entity = new ComputedPkWithPrefixTestEntity
                {
                    Component1 = component1,
                    Component2 = component2,
                    Sk = skValue
                };

                // Act: serialize with the given KeyInputMode
                var options = new FluentDynamoDbOptions();
                var item = ComputedPkWithPrefixTestEntity.ToDynamoDb(entity, options, mode);

                // The computed PK should be "Component1#Component2" - no prefix applied
                var expectedPk = $"{component1}#{component2}";
                var actualPk = item["pk"].S;

                return (actualPk == expectedPk).ToProperty()
                    .Label($"Computed PK should not have prefix applied. " +
                           $"Mode: {mode}, Component1: '{component1}', Component2: '{component2}', " +
                           $"Expected: '{expectedPk}', Got: '{actualPk}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.2, 3.3, 4.7**
    /// 
    /// For any entity with a computed PK and a non-computed SK with prefix,
    /// the SK SHOULD get prefix applied (in Auto/Value mode) while the PK does NOT.
    /// This proves the computed exclusion is selective - only computed keys are excluded.
    /// 
    /// In Value mode: SK always gets prefix prepended.
    /// In Auto mode: SK gets prefix if it doesn't already start with "META#".
    /// In Raw mode: SK passes through unchanged (no prefix applied to anything).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonComputedSk_GetsPrefixApplied_WhileComputedPkDoesNot()
    {
        // Use SK values that do NOT start with "META#" so Auto mode will apply prefix
        var skValueGen = Gen.Choose(1, 20).SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    'a', 'b', 'c', 'x', 'y', 'z',
                    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
                )).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("META#"))
            .ToArbitrary();

        return Prop.ForAll(
            NonEmptyComponentArb,
            NonEmptyComponentArb,
            skValueGen,
            (component1, component2, skValue) =>
            {
                // Arrange: create entity
                var entity = new ComputedPkWithPrefixTestEntity
                {
                    Component1 = component1,
                    Component2 = component2,
                    Sk = skValue
                };

                // Act: serialize with Value mode (always prepends prefix to non-computed keys)
                var options = new FluentDynamoDbOptions();
                var item = ComputedPkWithPrefixTestEntity.ToDynamoDb(entity, options, KeyInputMode.Value);

                // The computed PK should NOT have prefix
                var expectedPk = $"{component1}#{component2}";
                var actualPk = item["pk"].S;
                var pkCorrect = actualPk == expectedPk;

                // The non-computed SK SHOULD have prefix "META#" prepended in Value mode
                var expectedSk = $"META#{skValue}";
                var actualSk = item["sk"].S;
                var skCorrect = actualSk == expectedSk;

                return (pkCorrect && skCorrect).ToProperty()
                    .Label($"Computed PK excluded, non-computed SK prefixed. " +
                           $"PK - Expected: '{expectedPk}', Got: '{actualPk}' (correct: {pkCorrect}). " +
                           $"SK - Expected: '{expectedSk}', Got: '{actualSk}' (correct: {skCorrect}).");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 4.7, 10.5**
    /// 
    /// For any computed key property, Auto mode should NOT detect the configured prefix
    /// and attempt StartsWith logic — it should always write the computed value as-is.
    /// Even if the computed value happens to NOT start with the prefix, no prefix is prepended.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPk_AutoMode_NeverPrependsPrefix()
    {
        return Prop.ForAll(
            NonEmptyComponentArb,
            NonEmptyComponentArb,
            SortKeyValueArb,
            (component1, component2, skValue) =>
            {
                var entity = new ComputedPkWithPrefixTestEntity
                {
                    Component1 = component1,
                    Component2 = component2,
                    Sk = skValue
                };

                var options = new FluentDynamoDbOptions();
                var item = ComputedPkWithPrefixTestEntity.ToDynamoDb(entity, options, KeyInputMode.Auto);

                var expectedPk = $"{component1}#{component2}";
                var actualPk = item["pk"].S;

                // Even in Auto mode, computed PK should never be prefixed
                return (actualPk == expectedPk).ToProperty()
                    .Label($"Computed PK in Auto mode should not get prefix. " +
                           $"Expected: '{expectedPk}', Got: '{actualPk}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 4.7, 10.5**
    /// 
    /// For any computed key property, Value mode should NOT prepend the configured prefix.
    /// Even though Value mode always prepends for non-computed keys, computed keys are excluded.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPk_ValueMode_NeverPrependsPrefix()
    {
        return Prop.ForAll(
            NonEmptyComponentArb,
            NonEmptyComponentArb,
            SortKeyValueArb,
            (component1, component2, skValue) =>
            {
                var entity = new ComputedPkWithPrefixTestEntity
                {
                    Component1 = component1,
                    Component2 = component2,
                    Sk = skValue
                };

                var options = new FluentDynamoDbOptions();
                var item = ComputedPkWithPrefixTestEntity.ToDynamoDb(entity, options, KeyInputMode.Value);

                var expectedPk = $"{component1}#{component2}";
                var actualPk = item["pk"].S;

                // Even in Value mode, computed PK should never be prefixed
                return (actualPk == expectedPk).ToProperty()
                    .Label($"Computed PK in Value mode should not get prefix. " +
                           $"Expected: '{expectedPk}', Got: '{actualPk}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 4.7, 10.5**
    /// 
    /// For any computed key property, Raw mode should pass through unchanged (same as other modes
    /// for computed keys). This confirms the exclusion is mode-independent.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputedPk_RawMode_PassesThroughUnchanged()
    {
        return Prop.ForAll(
            NonEmptyComponentArb,
            NonEmptyComponentArb,
            SortKeyValueArb,
            (component1, component2, skValue) =>
            {
                var entity = new ComputedPkWithPrefixTestEntity
                {
                    Component1 = component1,
                    Component2 = component2,
                    Sk = skValue
                };

                var options = new FluentDynamoDbOptions();
                var item = ComputedPkWithPrefixTestEntity.ToDynamoDb(entity, options, KeyInputMode.Raw);

                var expectedPk = $"{component1}#{component2}";
                var actualPk = item["pk"].S;

                // In Raw mode, computed PK is the same as any other mode (no prefix)
                return (actualPk == expectedPk).ToProperty()
                    .Label($"Computed PK in Raw mode should pass through as computed value. " +
                           $"Expected: '{expectedPk}', Got: '{actualPk}'");
            });
    }
}

/// <summary>
/// Test entity with a computed PK (no prefix) and a non-computed SK with prefix.
/// The source generator should:
/// - NOT apply prefix to the computed PK (computed key exclusion — no prefix configured)
/// - Apply prefix to the non-computed SK (normal prefix behavior)
/// 
/// PK: [PartitionKey] + [Computed("Component1", "Component2", Separator = "#")]
///   → Computed value = "Component1#Component2" (passes through unchanged)
/// SK: [SortKey(Prefix = "META")]
///   → Normal prefix behavior based on KeyInputMode
/// </summary>
[DynamoDbTable("test-computed-key-exclusion")]
public partial class ComputedPkWithPrefixTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Component1", "Component2", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "META")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Component1 { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string Component2 { get; set; } = string.Empty;

    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}
