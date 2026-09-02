using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Properties;

/// <summary>
/// Property-based tests for Property 2: No-prefix pass-through.
/// 
/// **Validates: Requirements 1.6, 2.6, 10.4**
/// 
/// For any key value and any resolved KeyInputMode, when prefix is null or empty,
/// the serialized value equals the original input unchanged.
/// </summary>
[Trait("Feature", "put-key-prefix-application")]
[Trait("Property", "2")]
public class NoPrefixPassThroughPropertyTests
{
    /// <summary>
    /// Generator for resolved KeyInputMode values (Auto, Value, Raw).
    /// Default is excluded because it is resolved before reaching KeyPrefixHelper.
    /// </summary>
    private static readonly Arbitrary<KeyInputMode> ResolvedModeArb =
        Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw).ToArbitrary();

    /// <summary>
    /// Generator for null or empty prefix values (null, empty string, whitespace).
    /// These represent key properties with no prefix configured.
    /// </summary>
    private static readonly Arbitrary<string?> NullOrEmptyPrefixArb =
        Gen.Elements<string?>(null, "", " ", "  ", "\t").ToArbitrary();

    /// <summary>
    /// Generator for non-null key values of various lengths including empty string.
    /// </summary>
    private static readonly Arbitrary<string> KeyValueArb =
        Gen.OneOf(
            Gen.Constant(""),
            Gen.Choose(1, 5).SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    'a', 'b', 'c', '1', '2', '#', '_', '-', 'Z', ' '
                )).Select(chars => new string(chars))),
            Gen.Choose(6, 50).SelectMany(len =>
                Gen.ArrayOf(len, Arb.Default.Char().Generator)
                    .Select(chars => new string(chars)))
        ).ToArbitrary();

    /// <summary>
    /// **Validates: Requirements 1.6, 2.6, 10.4**
    /// 
    /// For any non-null key value and any resolved KeyInputMode (Auto, Value, Raw),
    /// when prefix is null, ApplyKeyPrefix returns the value unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullPrefix_ReturnsValueUnchanged()
    {
        return Prop.ForAll(
            KeyValueArb,
            ResolvedModeArb,
            Arb.Default.NonNull<string>(),
            (value, mode, separator) =>
            {
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, null, separator.Get, mode);
                return (result == value).ToProperty()
                    .Label($"Null prefix should return value unchanged. Mode: {mode}, Value: '{value}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6, 2.6, 10.4**
    /// 
    /// For any non-null key value and any resolved KeyInputMode (Auto, Value, Raw),
    /// when prefix is an empty string, ApplyKeyPrefix returns the value unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPrefix_ReturnsValueUnchanged()
    {
        return Prop.ForAll(
            KeyValueArb,
            ResolvedModeArb,
            Arb.Default.NonNull<string>(),
            (value, mode, separator) =>
            {
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, "", separator.Get, mode);
                return (result == value).ToProperty()
                    .Label($"Empty prefix should return value unchanged. Mode: {mode}, Value: '{value}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6, 2.6, 10.4**
    /// 
    /// For any non-null key value and any resolved KeyInputMode (Auto, Value, Raw),
    /// when prefix is whitespace-only, ApplyKeyPrefix returns the value unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespacePrefix_ReturnsValueUnchanged()
    {
        var gen = from value in KeyValueArb.Generator
                  from mode in Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw)
                  from prefix in Gen.Elements(" ", "  ", "\t", " \t ")
                  from separator in Arb.Default.NonNull<string>().Generator
                  select (value, mode, prefix, separator.Get);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (value, mode, prefix, separator) = tuple;
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, mode);
                return (result == value).ToProperty()
                    .Label($"Whitespace prefix should return value unchanged. Mode: {mode}, Value: '{value}', Prefix: '{prefix}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6, 2.6, 10.4**
    /// 
    /// Combined property: For any non-null key value, any null/empty/whitespace prefix,
    /// any separator, and any resolved KeyInputMode, ApplyKeyPrefix returns the value unchanged.
    /// This is the comprehensive test covering all null/empty prefix variants together.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property AnyNullOrEmptyPrefix_AnyMode_ReturnsValueUnchanged()
    {
        var gen = from value in KeyValueArb.Generator
                  from mode in Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw)
                  from prefix in Gen.Elements<string?>(null, "", " ", "  ", "\t")
                  from separator in Arb.Default.NonNull<string>().Generator
                  select (value, mode, prefix, separator.Get);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (value, mode, prefix, separator) = tuple;
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, mode);
                return (result == value).ToProperty()
                    .Label($"Null/empty prefix should return value unchanged. Mode: {mode}, Value: '{value}', Prefix: '{prefix}', Got: '{result}'");
            });
    }
}
