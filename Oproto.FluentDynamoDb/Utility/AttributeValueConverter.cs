using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Utility class for converting between .NET types and DynamoDB AttributeValue types.
/// Provides conversion methods for advanced DynamoDB types including Maps, Sets, Lists, and TTL fields.
/// All methods handle null and empty collections according to DynamoDB requirements.
/// </summary>
public static class AttributeValueConverter
{
    #region Map Conversions

    /// <summary>
    /// Converts a Dictionary&lt;string, string&gt; to a DynamoDB Map (M) AttributeValue.
    /// </summary>
    /// <param name="dict">The dictionary to convert.</param>
    /// <returns>An AttributeValue with M type, or null if the dictionary is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty maps, so null or empty dictionaries return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToMap(Dictionary<string, string>? dict)
    {
        if (dict == null || dict.Count == 0)
            return null;

        var map = new Dictionary<string, AttributeValue>(dict.Count);
        foreach (var kvp in dict)
        {
            map[kvp.Key] = new AttributeValue { S = kvp.Value };
        }
        return new AttributeValue { M = map };
    }

    /// <summary>
    /// Converts a Dictionary&lt;string, AttributeValue&gt; to a DynamoDB Map (M) AttributeValue.
    /// </summary>
    /// <param name="dict">The dictionary to convert.</param>
    /// <returns>An AttributeValue with M type, or null if the dictionary is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty maps, so null or empty dictionaries return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToMap(Dictionary<string, AttributeValue>? dict)
    {
        if (dict == null || dict.Count == 0)
            return null;

        return new AttributeValue { M = dict };
    }

    /// <summary>
    /// Reconstructs a Dictionary&lt;string, string&gt; from a DynamoDB Map (M) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A dictionary with string values, or null if the AttributeValue is null or has no map.</returns>
    public static Dictionary<string, string>? FromMap(AttributeValue? av)
    {
        if (av?.M == null || av.M.Count == 0)
            return null;

        return av.M.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.S);
    }

    /// <summary>
    /// Reconstructs a Dictionary&lt;string, AttributeValue&gt; from a DynamoDB Map (M) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A dictionary with AttributeValue values, or null if the AttributeValue is null or has no map.</returns>
    public static Dictionary<string, AttributeValue>? FromMapRaw(AttributeValue? av)
    {
        if (av?.M == null || av.M.Count == 0)
            return null;

        return av.M;
    }

    #endregion

    #region Set Conversions

    /// <summary>
    /// Converts a HashSet&lt;string&gt; to a DynamoDB String Set (SS) AttributeValue.
    /// </summary>
    /// <param name="set">The hash set to convert.</param>
    /// <returns>An AttributeValue with SS type, or null if the set is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty sets, so null or empty sets return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToStringSet(HashSet<string>? set)
    {
        if (set == null || set.Count == 0)
            return null;

        return new AttributeValue { SS = set.ToList() };
    }

    /// <summary>
    /// Converts a HashSet&lt;int&gt; to a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="set">The hash set to convert.</param>
    /// <returns>An AttributeValue with NS type, or null if the set is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty sets, so null or empty sets return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToNumberSet(HashSet<int>? set)
    {
        if (set == null || set.Count == 0)
            return null;

        return new AttributeValue { NS = set.Select(n => n.ToString()).ToList() };
    }

    /// <summary>
    /// Converts a HashSet&lt;long&gt; to a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="set">The hash set to convert.</param>
    /// <returns>An AttributeValue with NS type, or null if the set is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty sets, so null or empty sets return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToNumberSet(HashSet<long>? set)
    {
        if (set == null || set.Count == 0)
            return null;

        return new AttributeValue { NS = set.Select(n => n.ToString()).ToList() };
    }

    /// <summary>
    /// Converts a HashSet&lt;decimal&gt; to a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="set">The hash set to convert.</param>
    /// <returns>An AttributeValue with NS type, or null if the set is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty sets, so null or empty sets return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToNumberSet(HashSet<decimal>? set)
    {
        if (set == null || set.Count == 0)
            return null;

        return new AttributeValue { NS = set.Select(n => n.ToString()).ToList() };
    }

    /// <summary>
    /// Converts a HashSet&lt;byte[]&gt; to a DynamoDB Binary Set (BS) AttributeValue.
    /// </summary>
    /// <param name="set">The hash set to convert.</param>
    /// <returns>An AttributeValue with BS type, or null if the set is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty sets, so null or empty sets return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToBinarySet(HashSet<byte[]>? set)
    {
        if (set == null || set.Count == 0)
            return null;

        return new AttributeValue { BS = set.Select(b => new MemoryStream(b)).ToList() };
    }

    /// <summary>
    /// Reconstructs a HashSet&lt;string&gt; from a DynamoDB String Set (SS) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A hash set of strings, or null if the AttributeValue is null or has no string set.</returns>
    public static HashSet<string>? FromStringSet(AttributeValue? av)
    {
        if (av?.SS == null || av.SS.Count == 0)
            return null;

        return new HashSet<string>(av.SS);
    }

    /// <summary>
    /// Reconstructs a HashSet&lt;int&gt; from a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A hash set of integers, or null if the AttributeValue is null or has no number set.</returns>
    public static HashSet<int>? FromNumberSetInt(AttributeValue? av)
    {
        if (av?.NS == null || av.NS.Count == 0)
            return null;

        return new HashSet<int>(av.NS.Select(int.Parse));
    }

    /// <summary>
    /// Reconstructs a HashSet&lt;long&gt; from a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A hash set of longs, or null if the AttributeValue is null or has no number set.</returns>
    public static HashSet<long>? FromNumberSetLong(AttributeValue? av)
    {
        if (av?.NS == null || av.NS.Count == 0)
            return null;

        return new HashSet<long>(av.NS.Select(long.Parse));
    }

    /// <summary>
    /// Reconstructs a HashSet&lt;decimal&gt; from a DynamoDB Number Set (NS) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A hash set of decimals, or null if the AttributeValue is null or has no number set.</returns>
    public static HashSet<decimal>? FromNumberSetDecimal(AttributeValue? av)
    {
        if (av?.NS == null || av.NS.Count == 0)
            return null;

        return new HashSet<decimal>(av.NS.Select(decimal.Parse));
    }

    /// <summary>
    /// Reconstructs a HashSet&lt;byte[]&gt; from a DynamoDB Binary Set (BS) AttributeValue.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A hash set of byte arrays, or null if the AttributeValue is null or has no binary set.</returns>
    public static HashSet<byte[]>? FromBinarySet(AttributeValue? av)
    {
        if (av?.BS == null || av.BS.Count == 0)
            return null;

        return new HashSet<byte[]>(av.BS.Select(ms =>
        {
            using var memoryStream = new MemoryStream();
            ms.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }));
    }

    #endregion

    #region List Conversions

    /// <summary>
    /// Converts a List&lt;T&gt; to a DynamoDB List (L) AttributeValue using a custom element converter.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to convert.</param>
    /// <param name="converter">A function to convert each element to an AttributeValue.</param>
    /// <returns>An AttributeValue with L type, or null if the list is null or empty.</returns>
    /// <remarks>
    /// DynamoDB does not support empty lists, so null or empty lists return null.
    /// The caller should omit the attribute from the item when null is returned.
    /// </remarks>
    public static AttributeValue? ToList<T>(List<T>? list, Func<T, AttributeValue> converter)
    {
        if (list == null || list.Count == 0)
            return null;

        var attributeValues = new List<AttributeValue>(list.Count);
        foreach (var item in list)
        {
            attributeValues.Add(converter(item));
        }
        return new AttributeValue { L = attributeValues };
    }

    /// <summary>
    /// Reconstructs a List&lt;T&gt; from a DynamoDB List (L) AttributeValue using a custom element converter.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <param name="converter">A function to convert each AttributeValue to an element.</param>
    /// <returns>A list of elements, or null if the AttributeValue is null or has no list.</returns>
    public static List<T>? FromList<T>(AttributeValue? av, Func<AttributeValue, T> converter)
    {
        if (av?.L == null || av.L.Count == 0)
            return null;

        var list = new List<T>(av.L.Count);
        foreach (var item in av.L)
        {
            list.Add(converter(item));
        }
        return list;
    }

    #endregion

    #region TTL Conversions

    /// <summary>
    /// Converts a DateTime to a DynamoDB Number (N) AttributeValue representing Unix epoch seconds.
    /// This is the required format for DynamoDB Time-To-Live (TTL) fields.
    /// </summary>
    /// <param name="dateTime">The DateTime to convert.</param>
    /// <returns>An AttributeValue with N type containing Unix epoch seconds, or null if the DateTime is null.</returns>
    /// <remarks>
    /// The DateTime is converted to UTC before calculating the Unix epoch seconds.
    /// DynamoDB TTL requires the value to be in seconds since January 1, 1970 00:00:00 UTC.
    /// </remarks>
    public static AttributeValue? ToTtl(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
            return null;

        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seconds = (long)(dateTime.Value.ToUniversalTime() - epoch).TotalSeconds;
        return new AttributeValue { N = seconds.ToString() };
    }

    /// <summary>
    /// Converts a DateTimeOffset to a DynamoDB Number (N) AttributeValue representing Unix epoch seconds.
    /// This is the required format for DynamoDB Time-To-Live (TTL) fields.
    /// </summary>
    /// <param name="dateTimeOffset">The DateTimeOffset to convert.</param>
    /// <returns>An AttributeValue with N type containing Unix epoch seconds, or null if the DateTimeOffset is null.</returns>
    /// <remarks>
    /// DynamoDB TTL requires the value to be in seconds since January 1, 1970 00:00:00 UTC.
    /// </remarks>
    public static AttributeValue? ToTtl(DateTimeOffset? dateTimeOffset)
    {
        if (!dateTimeOffset.HasValue)
            return null;

        var seconds = dateTimeOffset.Value.ToUnixTimeSeconds();
        return new AttributeValue { N = seconds.ToString() };
    }

    /// <summary>
    /// Reconstructs a DateTime from a DynamoDB Number (N) AttributeValue containing Unix epoch seconds.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A DateTime in UTC, or null if the AttributeValue is null or has no number.</returns>
    /// <remarks>
    /// The returned DateTime will have DateTimeKind.Utc.
    /// </remarks>
    public static DateTime? FromTtl(AttributeValue? av)
    {
        if (av?.N == null)
            return null;

        var seconds = long.Parse(av.N);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(seconds);
    }

    /// <summary>
    /// Reconstructs a DateTimeOffset from a DynamoDB Number (N) AttributeValue containing Unix epoch seconds.
    /// </summary>
    /// <param name="av">The AttributeValue to convert.</param>
    /// <returns>A DateTimeOffset in UTC, or null if the AttributeValue is null or has no number.</returns>
    public static DateTimeOffset? FromTtlOffset(AttributeValue? av)
    {
        if (av?.N == null)
            return null;

        var seconds = long.Parse(av.N);
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    #endregion
}
