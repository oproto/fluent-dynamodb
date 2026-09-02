using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.Encryption.Kms.UnitTests;

/// <summary>
/// Property-based tests for DefaultKmsKeyResolver.
/// These tests verify correctness properties that should hold across all valid inputs.
/// </summary>
[Trait("Feature", "async-kms-key-resolver")]
[Trait("Category", "Property")]
public class DefaultKmsKeyResolverPropertyTests
{
    /// <summary>
    /// **Feature: async-kms-key-resolver, Property 1: Resolution priority ordering**
    /// **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.8**
    ///
    /// For any DefaultKmsKeyResolver constructed with a defaultKeyId, an optional contextKeyMap,
    /// and an optional aliasKeyMap, and for any contextId and keyAlias inputs, the resolved key SHALL equal:
    /// - The aliasKeyMap[keyAlias] value if keyAlias is non-null and present in aliasKeyMap (case-sensitive)
    /// - Otherwise, the contextKeyMap[contextId] value if contextId is non-null and present in contextKeyMap (case-sensitive)
    /// - Otherwise, the defaultKeyId
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolutionPriority_AliasOverContextOverDefault()
    {
        return Prop.ForAll(
            GenerateResolutionTestInput(),
            input =>
            {
                var (defaultKeyId, contextKeyMap, aliasKeyMap, contextId, keyAlias) = input;

                // Arrange
                var resolver = new DefaultKmsKeyResolver(
                    defaultKeyId,
                    contextKeyMap,
                    aliasKeyMap);

                // Act
                var result = resolver.ResolveKeyIdAsync(contextId, keyAlias).GetAwaiter().GetResult();

                // Assert: Determine expected result based on priority ordering
                string expected;

                if (keyAlias != null && aliasKeyMap != null && aliasKeyMap.ContainsKey(keyAlias))
                {
                    // Priority 1: Alias map hit
                    expected = aliasKeyMap[keyAlias];
                }
                else if (contextId != null && contextKeyMap != null && contextKeyMap.ContainsKey(contextId))
                {
                    // Priority 2: Context map hit
                    expected = contextKeyMap[contextId];
                }
                else
                {
                    // Priority 3: Default
                    expected = defaultKeyId;
                }

                return (result == expected).ToProperty()
                    .Label($"Expected: '{expected}', Got: '{result}', " +
                           $"keyAlias: '{keyAlias ?? "(null)"}', contextId: '{contextId ?? "(null)"}', " +
                           $"aliasMap keys: [{(aliasKeyMap != null ? string.Join(",", aliasKeyMap.Keys) : "null")}], " +
                           $"contextMap keys: [{(contextKeyMap != null ? string.Join(",", contextKeyMap.Keys) : "null")}]");
            });
    }

    /// <summary>
    /// **Feature: async-kms-key-resolver, Property 1: Resolution priority ordering (alias wins over context)**
    /// **Validates: Requirements 2.2, 2.3, 2.8**
    ///
    /// When both keyAlias exists in aliasKeyMap AND contextId exists in contextKeyMap,
    /// the alias mapping SHALL take priority.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolutionPriority_AliasWinsOverContext_WhenBothMatch()
    {
        return Prop.ForAll(
            GenerateAliasPriorityTestInput(),
            input =>
            {
                var (defaultKeyId, contextId, keyAlias, contextValue, aliasValue) = input;

                // Ensure alias and context values are distinct for a meaningful test
                var distinctAliasValue = aliasValue + "_alias";
                var distinctContextValue = contextValue + "_context";

                var contextKeyMap = new Dictionary<string, string> { [contextId] = distinctContextValue };
                var aliasKeyMap = new Dictionary<string, string> { [keyAlias] = distinctAliasValue };

                var resolver = new DefaultKmsKeyResolver(defaultKeyId, contextKeyMap, aliasKeyMap);

                // Act
                var result = resolver.ResolveKeyIdAsync(contextId, keyAlias).GetAwaiter().GetResult();

                // Assert: Alias should always win
                return (result == distinctAliasValue).ToProperty()
                    .Label($"Expected alias value '{distinctAliasValue}', Got '{result}'");
            });
    }

    /// <summary>
    /// **Feature: async-kms-key-resolver, Property 1: Resolution priority ordering (case sensitivity)**
    /// **Validates: Requirements 2.8**
    ///
    /// Lookups are case-sensitive: a key that differs only in case from an entry
    /// in the map SHALL NOT match.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolutionPriority_LookupsAreCaseSensitive()
    {
        return Prop.ForAll(
            GenerateDefaultKeyId(),
            GenerateMixedCaseKey(),
            GenerateNonEmptyNonWhitespaceString(),
            (defaultKeyId, originalKey, mapValue) =>
            {
                // Create maps with the original-case key
                var aliasKeyMap = new Dictionary<string, string> { [originalKey] = mapValue + "_alias" };
                var contextKeyMap = new Dictionary<string, string> { [originalKey] = mapValue + "_context" };

                var resolver = new DefaultKmsKeyResolver(defaultKeyId, contextKeyMap, aliasKeyMap);

                // Create a case-toggled version of the key
                var toggledKey = ToggleCase(originalKey);

                // Only test when the toggled key is actually different (not all-numeric etc.)
                if (toggledKey == originalKey)
                    return true.ToProperty().Label("Key has no case variation, skipping");

                // Act: lookup with toggled case for both alias and context
                var result = resolver.ResolveKeyIdAsync(toggledKey, toggledKey).GetAwaiter().GetResult();

                // Assert: Should NOT match (case-sensitive), so should fall through to default
                return (result == defaultKeyId).ToProperty()
                    .Label($"Expected default '{defaultKeyId}' for case-mismatched key '{toggledKey}' (original: '{originalKey}'), Got '{result}'");
            });
    }

    /// <summary>
    /// **Feature: async-kms-key-resolver, Property 1: Resolution priority ordering (null maps)**
    /// **Validates: Requirements 2.5**
    ///
    /// When both maps are null, the resolver SHALL always return the defaultKeyId
    /// regardless of contextId and keyAlias values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolutionPriority_NullMaps_AlwaysReturnsDefault()
    {
        return Prop.ForAll(
            GenerateDefaultKeyId(),
            GenerateNullableString(),
            GenerateNullableString(),
            (defaultKeyId, contextId, keyAlias) =>
            {
                // Arrange: Both maps are null
                var resolver = new DefaultKmsKeyResolver(defaultKeyId, contextKeyMap: null, aliasKeyMap: null);

                // Act
                var result = resolver.ResolveKeyIdAsync(contextId, keyAlias).GetAwaiter().GetResult();

                // Assert: Always returns default
                return (result == defaultKeyId).ToProperty()
                    .Label($"Expected default '{defaultKeyId}', Got '{result}'");
            });
    }



    #region Generators

    /// <summary>
    /// Generates a complete resolution test input as a tuple.
    /// </summary>
    private static Arbitrary<(string DefaultKeyId, Dictionary<string, string>? ContextKeyMap, Dictionary<string, string>? AliasKeyMap, string? ContextId, string? KeyAlias)> GenerateResolutionTestInput()
    {
        return Arb.From(
            from defaultKeyId in GenerateDefaultKeyIdGen()
            from contextKeyMap in GenerateOptionalDictionaryGen()
            from aliasKeyMap in GenerateOptionalDictionaryGen()
            from contextId in GenerateNullableStringGen()
            from keyAlias in GenerateNullableStringGen()
            select (defaultKeyId, contextKeyMap, aliasKeyMap, contextId, keyAlias));
    }

    /// <summary>
    /// Generates test input for alias-priority testing where both maps will have matching entries.
    /// </summary>
    private static Arbitrary<(string DefaultKeyId, string ContextId, string KeyAlias, string ContextValue, string AliasValue)> GenerateAliasPriorityTestInput()
    {
        return Arb.From(
            from defaultKeyId in GenerateDefaultKeyIdGen()
            from contextId in GenerateMapKey()
            from keyAlias in GenerateMapKey()
            from contextValue in GenerateMapValue()
            from aliasValue in GenerateMapValue()
            select (defaultKeyId, contextId, keyAlias, contextValue, aliasValue));
    }

    /// <summary>
    /// Generates non-empty, non-whitespace strings suitable for defaultKeyId.
    /// </summary>
    private static Arbitrary<string> GenerateDefaultKeyId()
    {
        return Arb.From(GenerateDefaultKeyIdGen());
    }

    private static Gen<string> GenerateDefaultKeyIdGen()
    {
        return Gen.Elements(
            "arn:aws:kms:us-east-1:123456789012:key/default-key",
            "arn:aws:kms:us-west-2:987654321098:key/fallback",
            "alias/my-default-key",
            "arn:aws:kms:eu-west-1:111222333444:key/prod-key",
            "default-key-id-12345");
    }

    /// <summary>
    /// Generates optional dictionaries (null or with 0-5 random entries).
    /// </summary>
    private static Gen<Dictionary<string, string>?> GenerateOptionalDictionaryGen()
    {
        var nullDict = Gen.Constant<Dictionary<string, string>?>(null);
        var emptyDict = Gen.Constant<Dictionary<string, string>?>(new Dictionary<string, string>());
        var nonEmptyDict =
            from count in Gen.Choose(1, 5)
            from keys in Gen.ArrayOf(count, GenerateMapKey())
            from values in Gen.ArrayOf(count, GenerateMapValue())
            let pairs = keys.Zip(values)
                .GroupBy(kv => kv.First)
                .Select(g => g.First())
                .ToDictionary(kv => kv.First, kv => kv.Second)
            select (Dictionary<string, string>?)pairs;

        return Gen.OneOf(nullDict, emptyDict, nonEmptyDict);
    }

    /// <summary>
    /// Generates nullable strings for contextId and keyAlias inputs.
    /// </summary>
    private static Arbitrary<string?> GenerateNullableString()
    {
        return Arb.From(GenerateNullableStringGen());
    }

    private static Gen<string?> GenerateNullableStringGen()
    {
        return Gen.OneOf(
            Gen.Constant<string?>(null),
            GenerateMapKey().Select(s => (string?)s));
    }

    /// <summary>
    /// Generates non-empty, non-whitespace strings.
    /// </summary>
    private static Arbitrary<string> GenerateNonEmptyNonWhitespaceString()
    {
        return Arb.From(GenerateMapKey());
    }

    /// <summary>
    /// Generates keys that contain mixed-case letters for case-sensitivity testing.
    /// </summary>
    private static Arbitrary<string> GenerateMixedCaseKey()
    {
        return Arb.From(
            Gen.Elements(
                "TenantA", "tenantA", "TENANT_A",
                "PiiKey", "piiKey", "PII_KEY",
                "MyAlias", "myAlias", "MY_ALIAS",
                "ContextOne", "contextOne", "CONTEXT_ONE"));
    }

    /// <summary>
    /// Generates map keys (non-null, non-empty strings representing contextIds or aliases).
    /// </summary>
    private static Gen<string> GenerateMapKey()
    {
        return Gen.Elements(
            "tenant-a", "tenant-b", "tenant-c",
            "pii", "financial", "health",
            "context-1", "context-2", "context-3",
            "alias-x", "alias-y", "alias-z");
    }

    /// <summary>
    /// Generates map values (non-null, non-empty strings representing KMS key ARNs).
    /// </summary>
    private static Gen<string> GenerateMapValue()
    {
        return Gen.Elements(
            "arn:aws:kms:us-east-1:123456789012:key/key-1",
            "arn:aws:kms:us-east-1:123456789012:key/key-2",
            "arn:aws:kms:us-east-1:123456789012:key/key-3",
            "arn:aws:kms:us-west-2:987654321098:key/key-4",
            "arn:aws:kms:eu-west-1:111222333444:key/key-5",
            "alias/pii-key",
            "alias/financial-key",
            "alias/health-key");
    }

    /// <summary>
    /// Toggles the case of each character in a string.
    /// </summary>
    private static string ToggleCase(string input)
    {
        return new string(input.Select(c =>
            char.IsUpper(c) ? char.ToLower(c) :
            char.IsLower(c) ? char.ToUpper(c) : c).ToArray());
    }

    #endregion
}
