namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Represents the DynamoDB data type of a dynamic field.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="DynamicFieldCollection.GetFieldType"/> to determine the type of a dynamic field
/// before accessing it with the appropriate typed getter.
/// </para>
/// <para>
/// Note that <see cref="DateTime"/> indicates a string value that is parseable as a date/time.
/// The underlying DynamoDB storage is still a string (S type).
/// </para>
/// </remarks>
public enum DynamicFieldType
{
    /// <summary>
    /// Field does not exist in the collection.
    /// </summary>
    /// <remarks>
    /// Returned when calling <see cref="DynamicFieldCollection.GetFieldType"/> with a field name
    /// that is not present in the collection.
    /// </remarks>
    NotFound,

    /// <summary>
    /// String (S) that is not a recognized date format.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetString"/> or <see cref="DynamicFieldCollection.TryGetString"/>
    /// to access the value.
    /// </remarks>
    String,

    /// <summary>
    /// String (S) that parses as DateTime or DateTimeOffset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The underlying storage is still a string; this indicates the value is parseable as a date/time
    /// in ISO 8601 format (e.g., "2024-01-15T10:30:00Z" or "2024-01-15T10:30:00+05:00").
    /// </para>
    /// <para>
    /// Use <see cref="DynamicFieldCollection.GetDateTime"/>, <see cref="DynamicFieldCollection.GetDateTimeOffset"/>,
    /// or their TryGet variants to access the value.
    /// </para>
    /// </remarks>
    DateTime,

    /// <summary>
    /// Number (N) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetInt"/>, <see cref="DynamicFieldCollection.GetLong"/>,
    /// <see cref="DynamicFieldCollection.GetDouble"/>, <see cref="DynamicFieldCollection.GetDecimal"/>,
    /// or their TryGet variants to access the value.
    /// </remarks>
    Number,

    /// <summary>
    /// Binary (B) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetBytes"/> or <see cref="DynamicFieldCollection.TryGetBytes"/>
    /// to access the value.
    /// </remarks>
    Binary,

    /// <summary>
    /// Boolean (BOOL) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetBool"/> or <see cref="DynamicFieldCollection.TryGetBool"/>
    /// to access the value.
    /// </remarks>
    Boolean,

    /// <summary>
    /// Null (NULL) type - field exists but has null value.
    /// </summary>
    /// <remarks>
    /// The field exists in DynamoDB with an explicit NULL type. Typed getters will return null/default.
    /// </remarks>
    Null,

    /// <summary>
    /// List (L) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetStringList"/>, <see cref="DynamicFieldCollection.GetIntList"/>,
    /// or their TryGet variants to access homogeneous lists.
    /// For heterogeneous lists, use <see cref="DynamicFieldCollection.GetRaw"/> for direct AttributeValue access.
    /// </remarks>
    List,

    /// <summary>
    /// Map (M) type - nested object.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetRaw"/> for direct AttributeValue access to work with nested maps.
    /// </remarks>
    Map,

    /// <summary>
    /// String Set (SS) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetStringSet"/> or <see cref="DynamicFieldCollection.TryGetStringSet"/>
    /// to access the value.
    /// </remarks>
    StringSet,

    /// <summary>
    /// Number Set (NS) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetNumberSet"/> or <see cref="DynamicFieldCollection.TryGetNumberSet"/>
    /// to access the value as integers.
    /// </remarks>
    NumberSet,

    /// <summary>
    /// Binary Set (BS) type.
    /// </summary>
    /// <remarks>
    /// Use <see cref="DynamicFieldCollection.GetRaw"/> for direct AttributeValue access to work with binary sets.
    /// </remarks>
    BinarySet
}
