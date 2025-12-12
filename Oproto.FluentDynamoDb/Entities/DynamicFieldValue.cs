using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Represents a dynamic field value for use in lambda expressions.
/// This type provides comparison operators that enable natural expression syntax
/// like <c>x.DynamicFields["score"] > 100</c>.
/// </summary>
/// <remarks>
/// <para>
/// This type is designed for use in expression trees only. The comparison operators
/// are analyzed by the expression translator and converted to DynamoDB expression syntax.
/// They should never be called directly at runtime.
/// </para>
/// <para>
/// Supported comparisons:
/// </para>
/// <list type="bullet">
/// <item><description>Equality: <c>== "value"</c>, <c>== 42</c>, <c>== true</c></description></item>
/// <item><description>Inequality: <c>!= "value"</c></description></item>
/// <item><description>Numeric comparisons: <c>&gt; 100</c>, <c>&lt; 50</c>, <c>&gt;= 10</c>, <c>&lt;= 20</c></description></item>
/// </list>
/// </remarks>
public readonly struct DynamicFieldValue
{
    /// <summary>
    /// The field name this value represents.
    /// </summary>
    internal string FieldName { get; }

    /// <summary>
    /// Creates a new DynamicFieldValue for the specified field.
    /// </summary>
    internal DynamicFieldValue(string fieldName)
    {
        FieldName = fieldName;
    }

    #region String Comparisons

    /// <summary>Compares the dynamic field to a string value.</summary>
    public static bool operator ==(DynamicFieldValue field, string? value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a string value.</summary>
    public static bool operator !=(DynamicFieldValue field, string? value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    /// <summary>Compares the dynamic field to a string value.</summary>
    public static bool operator ==(string? value, DynamicFieldValue field)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a string value.</summary>
    public static bool operator !=(string? value, DynamicFieldValue field)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    #endregion

    #region Integer Comparisons

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator ==(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator !=(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator >(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">", field.FieldName));

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator <(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<", field.FieldName));

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator >=(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">=", field.FieldName));

    /// <summary>Compares the dynamic field to an integer value.</summary>
    public static bool operator <=(DynamicFieldValue field, int value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<=", field.FieldName));

    #endregion

    #region Long Comparisons

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator ==(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator !=(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator >(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">", field.FieldName));

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator <(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<", field.FieldName));

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator >=(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">=", field.FieldName));

    /// <summary>Compares the dynamic field to a long value.</summary>
    public static bool operator <=(DynamicFieldValue field, long value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<=", field.FieldName));

    #endregion

    #region Double Comparisons

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator ==(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator !=(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator >(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">", field.FieldName));

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator <(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<", field.FieldName));

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator >=(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">=", field.FieldName));

    /// <summary>Compares the dynamic field to a double value.</summary>
    public static bool operator <=(DynamicFieldValue field, double value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<=", field.FieldName));

    #endregion

    #region Decimal Comparisons

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator ==(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator !=(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator >(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">", field.FieldName));

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator <(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<", field.FieldName));

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator >=(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage(">=", field.FieldName));

    /// <summary>Compares the dynamic field to a decimal value.</summary>
    public static bool operator <=(DynamicFieldValue field, decimal value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("<=", field.FieldName));

    #endregion

    #region Boolean Comparisons

    /// <summary>Compares the dynamic field to a boolean value.</summary>
    public static bool operator ==(DynamicFieldValue field, bool value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to a boolean value.</summary>
    public static bool operator !=(DynamicFieldValue field, bool value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    #endregion

    #region AttributeValue Comparisons (for backward compatibility)

    /// <summary>Compares the dynamic field to an AttributeValue.</summary>
    public static bool operator ==(DynamicFieldValue field, AttributeValue? value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("==", field.FieldName));

    /// <summary>Compares the dynamic field to an AttributeValue.</summary>
    public static bool operator !=(DynamicFieldValue field, AttributeValue? value)
        => throw new InvalidOperationException(ExpressionOnlyMessage("!=", field.FieldName));

    #endregion

    #region Object Overrides

    /// <inheritdoc />
    public override bool Equals(object? obj) => false;

    /// <inheritdoc />
    public override int GetHashCode() => FieldName?.GetHashCode() ?? 0;

    /// <inheritdoc />
    public override string ToString() => $"DynamicField[{FieldName}]";

    #endregion

    private static string ExpressionOnlyMessage(string op, string fieldName)
        => $"DynamicFieldValue operator '{op}' cannot be called directly at runtime. " +
           $"It is only valid within expression trees for filter or condition expressions. " +
           $"Example: table.Query().WithFilter(x => x.DynamicFields[\"{fieldName}\"] {op} value)";
}
