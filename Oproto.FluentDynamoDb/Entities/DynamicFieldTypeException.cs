namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Exception thrown when attempting to access a dynamic field with an incompatible type.
/// </summary>
/// <remarks>
/// This exception is thrown by <see cref="DynamicFieldCollection"/> typed getter methods
/// when the requested type does not match the actual DynamoDB type of the field.
/// </remarks>
public sealed class DynamicFieldTypeException : InvalidOperationException
{
    /// <summary>
    /// Gets the name of the field that caused the exception.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// Gets the type that was requested.
    /// </summary>
    public Type RequestedType { get; }

    /// <summary>
    /// Gets the actual DynamoDB type of the field.
    /// </summary>
    public string ActualDynamoDbType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFieldTypeException"/> class.
    /// </summary>
    /// <param name="fieldName">The name of the field that caused the exception.</param>
    /// <param name="requestedType">The type that was requested.</param>
    /// <param name="actualDynamoDbType">The actual DynamoDB type of the field.</param>
    public DynamicFieldTypeException(string fieldName, Type requestedType, string actualDynamoDbType)
        : base($"Dynamic field '{fieldName}' cannot be converted to {requestedType.Name}. " +
               $"The field contains a DynamoDB {actualDynamoDbType} value.")
    {
        FieldName = fieldName;
        RequestedType = requestedType;
        ActualDynamoDbType = actualDynamoDbType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFieldTypeException"/> class with a custom message.
    /// </summary>
    /// <param name="fieldName">The name of the field that caused the exception.</param>
    /// <param name="requestedType">The type that was requested.</param>
    /// <param name="actualDynamoDbType">The actual DynamoDB type of the field.</param>
    /// <param name="message">The custom error message.</param>
    public DynamicFieldTypeException(string fieldName, Type requestedType, string actualDynamoDbType, string message)
        : base(message)
    {
        FieldName = fieldName;
        RequestedType = requestedType;
        ActualDynamoDbType = actualDynamoDbType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicFieldTypeException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="fieldName">The name of the field that caused the exception.</param>
    /// <param name="requestedType">The type that was requested.</param>
    /// <param name="actualDynamoDbType">The actual DynamoDB type of the field.</param>
    /// <param name="message">The custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DynamicFieldTypeException(string fieldName, Type requestedType, string actualDynamoDbType, string message, Exception innerException)
        : base(message, innerException)
    {
        FieldName = fieldName;
        RequestedType = requestedType;
        ActualDynamoDbType = actualDynamoDbType;
    }
}
