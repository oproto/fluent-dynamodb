using System.Collections;
using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// A collection of dynamic fields captured from DynamoDB items that are not mapped to entity properties.
/// Provides typed accessors for common types while maintaining the underlying AttributeValue storage.
/// </summary>
/// <remarks>
/// <para>
/// This collection is populated automatically by the source generator when an entity has the
/// <see cref="Attributes.EnableDynamicFieldsAttribute"/> applied. It contains all DynamoDB attributes
/// that are not explicitly mapped to entity properties.
/// </para>
/// <para>
/// The collection provides typed accessors for common types (string, int, bool, etc.) as well as
/// raw <see cref="AttributeValue"/> access for complex types.
/// </para>
/// <para>
/// This class is not thread-safe. Do not share instances across threads without external synchronization.
/// </para>
/// </remarks>
public sealed class DynamicFieldCollection : IEnumerable<KeyValuePair<string, AttributeValue>>
{
    private readonly Dictionary<string, AttributeValue> _fields;
    private readonly HashSet<string> _addedOrModified = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removed = new(StringComparer.Ordinal);
    private bool _trackChanges;

    /// <summary>
    /// Initializes a new empty <see cref="DynamicFieldCollection"/>.
    /// </summary>
    public DynamicFieldCollection()
    {
        _fields = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Initializes a new <see cref="DynamicFieldCollection"/> with the specified fields.
    /// </summary>
    /// <param name="fields">The initial fields to populate the collection with.</param>
    public DynamicFieldCollection(Dictionary<string, AttributeValue> fields)
    {
        _fields = fields ?? new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the number of dynamic fields in the collection.
    /// </summary>
    public int Count => _fields.Count;

    /// <summary>
    /// Gets the names of all dynamic fields in the collection.
    /// </summary>
    public IEnumerable<string> FieldNames => _fields.Keys;

    /// <summary>
    /// Gets the set of field names that have been marked for removal.
    /// Used by the expression translator to generate REMOVE clauses.
    /// </summary>
    public IReadOnlySet<string> RemovedFields => _removed;

    /// <summary>
    /// Gets whether this collection has any tracked changes (additions, modifications, or removals).
    /// </summary>
    public bool HasChanges => _addedOrModified.Count > 0 || _removed.Count > 0;


    #region Expression Support (Indexer and Existence Checks)

    /// <summary>
    /// Gets a <see cref="DynamicFieldValue"/> for the specified field. Used in lambda expressions for filter and condition expressions.
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to access.</param>
    /// <returns>A <see cref="DynamicFieldValue"/> that can be compared to typed values in expressions.</returns>
    /// <exception cref="InvalidOperationException">
    /// Always thrown when called directly at runtime. This indexer is designed for use in expression trees only.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This indexer enables type-safe access to dynamic fields in lambda expressions for filter, condition, and update expressions.
    /// It is designed to be analyzed by the expression translator and converted into DynamoDB expression syntax.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> This indexer should never be called directly at runtime.
    /// Use the typed getter methods (GetString, GetInt, etc.) for runtime access.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Filter by string value
    /// table.Query().WithFilter(x => x.DynamicFields["customField"] == "value");
    /// 
    /// // Filter by numeric value with comparison
    /// table.Query().WithFilter(x => x.DynamicFields["score"] > 100);
    /// 
    /// // Filter by boolean value
    /// table.Query().WithFilter(x => x.DynamicFields["isActive"] == true);
    /// </code>
    /// </example>
    public DynamicFieldValue this[string fieldName]
    {
        get => throw new InvalidOperationException(
            $"DynamicFieldCollection indexer cannot be called directly at runtime. " +
            $"It is only valid within expression trees for filter, condition, or update expressions. " +
            $"For runtime access, use GetString(\"{fieldName}\"), GetInt(\"{fieldName}\"), or other typed getter methods.");
    }

    /// <summary>
    /// Checks if a dynamic field exists. Used in lambda expressions for filter and condition expressions.
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to check.</param>
    /// <returns>True if the field exists (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">
    /// Always thrown when called directly at runtime. This method is designed for use in expression trees only.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is translated to the DynamoDB <c>attribute_exists()</c> function.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> This method should never be called directly at runtime.
    /// Use <see cref="ContainsKey"/> for runtime existence checks.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if field exists in filter expression
    /// table.Query().WithFilter(x => x.DynamicFields.Exists("optionalField"));
    /// // Generates: attribute_exists(#dynField0)
    /// </code>
    /// </example>
    public bool Exists(string fieldName)
    {
        throw new InvalidOperationException(
            $"DynamicFieldCollection.Exists cannot be called directly at runtime. " +
            $"It is only valid within expression trees for filter or condition expressions. " +
            $"For runtime existence checks, use ContainsKey(\"{fieldName}\").");
    }

    /// <summary>
    /// Checks if a dynamic field does not exist. Used in lambda expressions for filter and condition expressions.
    /// </summary>
    /// <param name="fieldName">The name of the dynamic field to check.</param>
    /// <returns>True if the field does not exist (for expression analysis only).</returns>
    /// <exception cref="InvalidOperationException">
    /// Always thrown when called directly at runtime. This method is designed for use in expression trees only.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is translated to the DynamoDB <c>attribute_not_exists()</c> function.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> This method should never be called directly at runtime.
    /// Use <c>!ContainsKey(fieldName)</c> for runtime non-existence checks.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Check if field does not exist in filter expression
    /// table.Query().WithFilter(x => x.DynamicFields.NotExists("deletedAt"));
    /// // Generates: attribute_not_exists(#dynField0)
    /// </code>
    /// </example>
    public bool NotExists(string fieldName)
    {
        throw new InvalidOperationException(
            $"DynamicFieldCollection.NotExists cannot be called directly at runtime. " +
            $"It is only valid within expression trees for filter or condition expressions. " +
            $"For runtime non-existence checks, use !ContainsKey(\"{fieldName}\").");
    }

    #endregion


    #region Type Detection

    /// <summary>
    /// Gets the DynamoDB type of the specified dynamic field.
    /// </summary>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <returns>
    /// The <see cref="DynamicFieldType"/> indicating the field's type,
    /// or <see cref="DynamicFieldType.NotFound"/> if the field does not exist.
    /// </returns>
    /// <remarks>
    /// For string values, this method attempts to parse the value as a date/time.
    /// If successful, it returns <see cref="DynamicFieldType.DateTime"/>; otherwise,
    /// it returns <see cref="DynamicFieldType.String"/>.
    /// </remarks>
    public DynamicFieldType GetFieldType(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return DynamicFieldType.NotFound;

        if (value.S != null)
        {
            // Try to detect if string is a DateTime/DateTimeOffset
            if (DateTimeOffset.TryParse(value.S, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _))
            {
                return DynamicFieldType.DateTime;
            }
            return DynamicFieldType.String;
        }

        if (value.N != null) return DynamicFieldType.Number;
        if (value.B != null) return DynamicFieldType.Binary;
        if (value.IsBOOLSet == true) return DynamicFieldType.Boolean;
        if (value.NULL == true) return DynamicFieldType.Null;
        if (value.IsLSet == true) return DynamicFieldType.List;
        if (value.IsMSet == true) return DynamicFieldType.Map;
        if (value.SS?.Count > 0) return DynamicFieldType.StringSet;
        if (value.NS?.Count > 0) return DynamicFieldType.NumberSet;
        if (value.BS?.Count > 0) return DynamicFieldType.BinarySet;

        return DynamicFieldType.NotFound;
    }

    #endregion

    #region Collection Operations

    /// <summary>
    /// Determines whether the collection contains a field with the specified name.
    /// </summary>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <returns><c>true</c> if the field exists; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string fieldName) => _fields.ContainsKey(fieldName);

    /// <summary>
    /// Removes the field with the specified name from the collection.
    /// </summary>
    /// <param name="fieldName">The name of the field to remove.</param>
    /// <returns><c>true</c> if the field was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(string fieldName)
    {
        var removed = _fields.Remove(fieldName);
        if (_trackChanges && removed)
        {
            _addedOrModified.Remove(fieldName);
            _removed.Add(fieldName);
        }
        return removed;
    }

    /// <summary>
    /// Removes all fields from the collection.
    /// </summary>
    public void Clear()
    {
        if (_trackChanges)
        {
            foreach (var key in _fields.Keys)
            {
                _removed.Add(key);
            }
            _addedOrModified.Clear();
        }
        _fields.Clear();
    }

    /// <summary>
    /// Gets all fields as a dictionary. Used internally by the mapper.
    /// </summary>
    internal Dictionary<string, AttributeValue> ToDictionary() => _fields;

    #endregion


    #region Typed Getters

    /// <summary>
    /// Gets a string value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The string value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a string type.</exception>
    public string? GetString(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.S != null) return value.S;

        throw new DynamicFieldTypeException(fieldName, typeof(string), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets an integer value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The integer value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a number type.</exception>
    public int? GetInt(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.N != null && int.TryParse(value.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(int), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a long value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The long value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a number type.</exception>
    public long? GetLong(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.N != null && long.TryParse(value.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(long), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a double value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The double value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a number type.</exception>
    public double? GetDouble(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.N != null && double.TryParse(value.N, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(double), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a decimal value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The decimal value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a number type.</exception>
    public decimal? GetDecimal(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.N != null && decimal.TryParse(value.N, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(decimal), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a boolean value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The boolean value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a boolean type.</exception>
    public bool? GetBool(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.IsBOOLSet == true) return value.BOOL;

        throw new DynamicFieldTypeException(fieldName, typeof(bool), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a DateTime value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The DateTime value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but cannot be parsed as DateTime.</exception>
    public DateTime? GetDateTime(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.S != null && DateTime.TryParse(value.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(DateTime), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a DateTimeOffset value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The DateTimeOffset value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but cannot be parsed as DateTimeOffset.</exception>
    public DateTimeOffset? GetDateTimeOffset(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.S != null && DateTimeOffset.TryParse(value.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;

        throw new DynamicFieldTypeException(fieldName, typeof(DateTimeOffset), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a byte array value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The byte array value, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a binary type.</exception>
    public byte[]? GetBytes(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.B != null) return value.B.ToArray();

        throw new DynamicFieldTypeException(fieldName, typeof(byte[]), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a list of strings from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The list of strings, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a list type.</exception>
    public List<string>? GetStringList(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.IsLSet == true)
            return value.L.Where(v => v.S != null).Select(v => v.S).ToList()!;

        throw new DynamicFieldTypeException(fieldName, typeof(List<string>), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a list of integers from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The list of integers, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a list type.</exception>
    public List<int>? GetIntList(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.IsLSet == true)
            return value.L
                .Where(v => v.N != null)
                .Select(v => int.Parse(v.N, CultureInfo.InvariantCulture))
                .ToList();

        throw new DynamicFieldTypeException(fieldName, typeof(List<int>), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a set of strings from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The set of strings, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a string set type.</exception>
    public HashSet<string>? GetStringSet(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.SS?.Count > 0) return new HashSet<string>(value.SS);

        throw new DynamicFieldTypeException(fieldName, typeof(HashSet<string>), GetDynamoDbTypeName(value));
    }

    /// <summary>
    /// Gets a set of integers from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The set of integers, or <c>null</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but is not a number set type.</exception>
    public HashSet<int>? GetNumberSet(string fieldName)
    {
        if (!_fields.TryGetValue(fieldName, out var value))
            return null;

        if (value.NULL == true) return null;
        if (value.NS?.Count > 0)
            return new HashSet<int>(value.NS.Select(n => int.Parse(n, CultureInfo.InvariantCulture)));

        throw new DynamicFieldTypeException(fieldName, typeof(HashSet<int>), GetDynamoDbTypeName(value));
    }

    #endregion


    #region TryGet Methods

    /// <summary>
    /// Tries to get a string value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the string value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a string type; otherwise, <c>false</c>.</returns>
    public bool TryGetString(string fieldName, out string? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.S != null) { value = av.S; return true; }
        return false;
    }

    /// <summary>
    /// Tries to get an integer value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the integer value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a number type; otherwise, <c>false</c>.</returns>
    public bool TryGetInt(string fieldName, out int? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.N != null && int.TryParse(av.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a long value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the long value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a number type; otherwise, <c>false</c>.</returns>
    public bool TryGetLong(string fieldName, out long? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.N != null && long.TryParse(av.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a double value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the double value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a number type; otherwise, <c>false</c>.</returns>
    public bool TryGetDouble(string fieldName, out double? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.N != null && double.TryParse(av.N, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a decimal value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the decimal value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a number type; otherwise, <c>false</c>.</returns>
    public bool TryGetDecimal(string fieldName, out decimal? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.N != null && decimal.TryParse(av.N, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a boolean value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the boolean value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a boolean type; otherwise, <c>false</c>.</returns>
    public bool TryGetBool(string fieldName, out bool? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.IsBOOLSet == true) { value = av.BOOL; return true; }
        return false;
    }

    /// <summary>
    /// Tries to get a DateTime value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the DateTime value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and can be parsed as DateTime; otherwise, <c>false</c>.</returns>
    public bool TryGetDateTime(string fieldName, out DateTime? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.S != null && DateTime.TryParse(av.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a DateTimeOffset value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the DateTimeOffset value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and can be parsed as DateTimeOffset; otherwise, <c>false</c>.</returns>
    public bool TryGetDateTimeOffset(string fieldName, out DateTimeOffset? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.S != null && DateTimeOffset.TryParse(av.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            value = result;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a byte array value from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the byte array value if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a binary type; otherwise, <c>false</c>.</returns>
    public bool TryGetBytes(string fieldName, out byte[]? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.B != null) { value = av.B.ToArray(); return true; }
        return false;
    }

    /// <summary>
    /// Tries to get a list of strings from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the list of strings if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a list type; otherwise, <c>false</c>.</returns>
    public bool TryGetStringList(string fieldName, out List<string>? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.IsLSet == true)
        {
            value = av.L.Where(v => v.S != null).Select(v => v.S).ToList()!;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a list of integers from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the list of integers if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a list type; otherwise, <c>false</c>.</returns>
    public bool TryGetIntList(string fieldName, out List<int>? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.IsLSet == true)
        {
            value = av.L.Where(v => v.N != null).Select(v => int.Parse(v.N, CultureInfo.InvariantCulture)).ToList();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get a set of strings from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the set of strings if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a string set type; otherwise, <c>false</c>.</returns>
    public bool TryGetStringSet(string fieldName, out HashSet<string>? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.SS?.Count > 0) { value = new HashSet<string>(av.SS); return true; }
        return false;
    }

    /// <summary>
    /// Tries to get a set of integers from the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the set of integers if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the field exists and is a number set type; otherwise, <c>false</c>.</returns>
    public bool TryGetNumberSet(string fieldName, out HashSet<int>? value)
    {
        value = null;
        if (!_fields.TryGetValue(fieldName, out var av))
            return false;

        if (av.NULL == true) { value = null; return true; }
        if (av.NS?.Count > 0)
        {
            value = new HashSet<int>(av.NS.Select(n => int.Parse(n, CultureInfo.InvariantCulture)));
            return true;
        }
        return false;
    }

    #endregion


    #region Generic Get/TryGet

    /// <summary>
    /// Gets a value of the specified type from the specified field.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The value, or <c>default</c> if the field does not exist.</returns>
    /// <exception cref="DynamicFieldTypeException">Thrown when the field exists but cannot be converted to the requested type.</exception>
    public T? Get<T>(string fieldName)
    {
        var type = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string)) return (T?)(object?)GetString(fieldName);
        if (underlyingType == typeof(int)) return (T?)(object?)GetInt(fieldName);
        if (underlyingType == typeof(long)) return (T?)(object?)GetLong(fieldName);
        if (underlyingType == typeof(double)) return (T?)(object?)GetDouble(fieldName);
        if (underlyingType == typeof(decimal)) return (T?)(object?)GetDecimal(fieldName);
        if (underlyingType == typeof(bool)) return (T?)(object?)GetBool(fieldName);
        if (underlyingType == typeof(DateTime)) return (T?)(object?)GetDateTime(fieldName);
        if (underlyingType == typeof(DateTimeOffset)) return (T?)(object?)GetDateTimeOffset(fieldName);
        if (underlyingType == typeof(byte[])) return (T?)(object?)GetBytes(fieldName);

        throw new NotSupportedException($"Type {type.Name} is not supported by Get<T>. Use GetRaw for complex types.");
    }

    /// <summary>
    /// Tries to get a value of the specified type from the specified field.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve.</typeparam>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">When this method returns, contains the value if found; otherwise, <c>default</c>.</param>
    /// <returns><c>true</c> if the field exists and can be converted to the requested type; otherwise, <c>false</c>.</returns>
    public bool TryGet<T>(string fieldName, out T? value)
    {
        value = default;
        var type = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            if (TryGetString(fieldName, out var s)) { value = (T?)(object?)s; return true; }
            return false;
        }
        if (underlyingType == typeof(int))
        {
            if (TryGetInt(fieldName, out var i)) { value = (T?)(object?)i; return true; }
            return false;
        }
        if (underlyingType == typeof(long))
        {
            if (TryGetLong(fieldName, out var l)) { value = (T?)(object?)l; return true; }
            return false;
        }
        if (underlyingType == typeof(double))
        {
            if (TryGetDouble(fieldName, out var d)) { value = (T?)(object?)d; return true; }
            return false;
        }
        if (underlyingType == typeof(decimal))
        {
            if (TryGetDecimal(fieldName, out var dec)) { value = (T?)(object?)dec; return true; }
            return false;
        }
        if (underlyingType == typeof(bool))
        {
            if (TryGetBool(fieldName, out var b)) { value = (T?)(object?)b; return true; }
            return false;
        }
        if (underlyingType == typeof(DateTime))
        {
            if (TryGetDateTime(fieldName, out var dt)) { value = (T?)(object?)dt; return true; }
            return false;
        }
        if (underlyingType == typeof(DateTimeOffset))
        {
            if (TryGetDateTimeOffset(fieldName, out var dto)) { value = (T?)(object?)dto; return true; }
            return false;
        }
        if (underlyingType == typeof(byte[]))
        {
            if (TryGetBytes(fieldName, out var bytes)) { value = (T?)(object?)bytes; return true; }
            return false;
        }

        return false;
    }

    #endregion


    #region Typed Setters

    /// <summary>
    /// Sets a string value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetString(string fieldName, string? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { S = value };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets an integer value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetInt(string fieldName, int? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { N = value.Value.ToString(CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a long value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetLong(string fieldName, long? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { N = value.Value.ToString(CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a double value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetDouble(string fieldName, double? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { N = value.Value.ToString("G17", CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a decimal value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetDecimal(string fieldName, decimal? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { N = value.Value.ToString(CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a boolean value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetBool(string fieldName, bool? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { BOOL = value.Value };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a DateTime value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    /// <remarks>The value is stored as an ISO 8601 formatted string.</remarks>
    public void SetDateTime(string fieldName, DateTime? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { S = value.Value.ToString("O", CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a DateTimeOffset value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    /// <remarks>The value is stored as an ISO 8601 formatted string.</remarks>
    public void SetDateTimeOffset(string fieldName, DateTimeOffset? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { S = value.Value.ToString("O", CultureInfo.InvariantCulture) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a byte array value for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetBytes(string fieldName, byte[]? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { B = new MemoryStream(value) };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a list of strings for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetStringList(string fieldName, List<string>? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue
        {
            L = value.Select(s => new AttributeValue { S = s }).ToList()
        };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a list of integers for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetIntList(string fieldName, List<int>? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue
        {
            L = value.Select(i => new AttributeValue { N = i.ToString(CultureInfo.InvariantCulture) }).ToList()
        };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a set of strings for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetStringSet(string fieldName, HashSet<string>? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { SS = value.ToList() };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a set of integers for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    public void SetNumberSet(string fieldName, HashSet<int>? value)
    {
        if (value == null) { Remove(fieldName); return; }
        _fields[fieldName] = new AttributeValue { NS = value.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList() };
        TrackModification(fieldName);
    }

    /// <summary>
    /// Sets a value for the specified field using generic type inference.
    /// </summary>
    /// <typeparam name="T">The type of value to set.</typeparam>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set, or <c>null</c> to remove the field.</param>
    /// <exception cref="NotSupportedException">Thrown when the type is not supported.</exception>
    public void Set<T>(string fieldName, T? value)
    {
        if (value == null) { Remove(fieldName); return; }

        var type = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Note: Each typed setter already calls TrackModification, so we don't need to call it here
        if (underlyingType == typeof(string)) { SetString(fieldName, (string)(object)value); return; }
        if (underlyingType == typeof(int)) { SetInt(fieldName, (int)(object)value); return; }
        if (underlyingType == typeof(long)) { SetLong(fieldName, (long)(object)value); return; }
        if (underlyingType == typeof(double)) { SetDouble(fieldName, (double)(object)value); return; }
        if (underlyingType == typeof(decimal)) { SetDecimal(fieldName, (decimal)(object)value); return; }
        if (underlyingType == typeof(bool)) { SetBool(fieldName, (bool)(object)value); return; }
        if (underlyingType == typeof(DateTime)) { SetDateTime(fieldName, (DateTime)(object)value); return; }
        if (underlyingType == typeof(DateTimeOffset)) { SetDateTimeOffset(fieldName, (DateTimeOffset)(object)value); return; }
        if (underlyingType == typeof(byte[])) { SetBytes(fieldName, (byte[])(object)value); return; }

        throw new NotSupportedException($"Type {type.Name} is not supported by Set<T>. Use SetRaw for complex types.");
    }

    #endregion


    #region Raw Access

    /// <summary>
    /// Gets the raw AttributeValue for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The AttributeValue, or <c>null</c> if the field does not exist.</returns>
    public AttributeValue? GetRaw(string fieldName)
    {
        return _fields.TryGetValue(fieldName, out var value) ? value : null;
    }

    /// <summary>
    /// Sets the raw AttributeValue for the specified field.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The AttributeValue to set, or <c>null</c> to remove the field.</param>
    public void SetRaw(string fieldName, AttributeValue? value)
    {
        if (value == null)
        {
            Remove(fieldName);
            return;
        }
        _fields[fieldName] = value;
        TrackModification(fieldName);
    }

    #endregion

    #region IEnumerable Implementation

    /// <summary>
    /// Returns an enumerator that iterates through the dynamic fields.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    public IEnumerator<KeyValuePair<string, AttributeValue>> GetEnumerator() => _fields.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the dynamic fields.
    /// </summary>
    /// <returns>An enumerator for the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Change Tracking

    /// <summary>
    /// Returns a new collection containing only the fields that have been added or modified,
    /// with tracking of removed fields. By default, resets change tracking on the source collection.
    /// </summary>
    /// <param name="resetTracking">If true (default), resets change tracking on the source collection.
    /// Set to false for retry scenarios where you need to preserve tracking.</param>
    /// <returns>A new <see cref="DynamicFieldCollection"/> containing only changed fields.</returns>
    public DynamicFieldCollection ChangesOnly(bool resetTracking = true)
    {
        var changes = new DynamicFieldCollection();

        // Copy added/modified fields
        foreach (var key in _addedOrModified.Where(k => _fields.ContainsKey(k)))
        {
            changes._fields[key] = _fields[key];
        }

        // Copy removed fields list (for REMOVE clause generation)
        foreach (var key in _removed)
        {
            changes._removed.Add(key);
        }

        // Reset tracking on source collection (default behavior)
        if (resetTracking)
        {
            ResetChangeTracking();
        }

        return changes;
    }

    /// <summary>
    /// Manually resets change tracking, clearing all tracked additions, modifications, and removals.
    /// </summary>
    public void ResetChangeTracking()
    {
        _addedOrModified.Clear();
        _removed.Clear();
    }

    /// <summary>
    /// Starts tracking changes to the collection. Called by FromDynamoDb after populating the collection.
    /// </summary>
    /// <remarks>
    /// This method is called automatically by the source-generated <c>FromDynamoDb</c> method after
    /// populating the collection with unmapped attributes. After this method is called, all subsequent
    /// modifications (Set, Remove, Clear) will be tracked and can be retrieved via <see cref="ChangesOnly"/>.
    /// </remarks>
    public void StartTrackingChanges()
    {
        _trackChanges = true;
        _addedOrModified.Clear();
        _removed.Clear();
    }

    /// <summary>
    /// Tracks a field modification when change tracking is enabled.
    /// </summary>
    /// <param name="fieldName">The name of the field that was modified.</param>
    private void TrackModification(string fieldName)
    {
        if (_trackChanges)
        {
            _removed.Remove(fieldName);
            _addedOrModified.Add(fieldName);
        }
    }

    #endregion

    #region Helper Methods

    private static string GetDynamoDbTypeName(AttributeValue value)
    {
        if (value.S != null) return "String (S)";
        if (value.N != null) return "Number (N)";
        if (value.B != null) return "Binary (B)";
        if (value.IsBOOLSet == true) return "Boolean (BOOL)";
        if (value.NULL == true) return "Null (NULL)";
        if (value.IsLSet == true) return "List (L)";
        if (value.IsMSet == true) return "Map (M)";
        if (value.SS?.Count > 0) return "String Set (SS)";
        if (value.NS?.Count > 0) return "Number Set (NS)";
        if (value.BS?.Count > 0) return "Binary Set (BS)";
        return "Unknown";
    }

    #endregion
}
