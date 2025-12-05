using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Thrown when an update expression attempts an invalid operation.
/// This occurs when trying to update key properties or perform other disallowed operations.
/// </summary>
/// <remarks>
/// <para><strong>Common Causes:</strong></para>
/// <list type="bullet">
/// <item><description>Attempting to update a partition key property</description></item>
/// <item><description>Attempting to update a sort key property</description></item>
/// <item><description>Attempting to remove a key property</description></item>
/// </list>
/// 
/// <para><strong>Resolution:</strong></para>
/// <list type="number">
/// <item><description>Do not include key properties in update expressions</description></item>
/// <item><description>Key properties are immutable and cannot be changed after item creation</description></item>
/// <item><description>To change a key, delete the old item and create a new one with the new key</description></item>
/// </list>
/// 
/// <para><strong>Property Information:</strong></para>
/// <para>
/// The exception provides <see cref="PropertyName"/> to help identify which property caused the error.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Entity with key properties
/// public class User
/// {
///     [PartitionKey]
///     public string UserId { get; set; }
///     
///     [SortKey]
///     public string Email { get; set; }
///     
///     public string Name { get; set; }
/// }
/// 
/// try
/// {
///     // ✗ Invalid: Attempting to update partition key
///     table.Users.Update(userId)
///         .Set(x => new UserUpdateModel 
///         {
///             UserId = "new-id",  // Cannot update key!
///             Name = "John"
///         })
///         .UpdateAsync();
/// }
/// catch (InvalidUpdateOperationException ex)
/// {
///     Console.WriteLine($"Property: {ex.PropertyName}"); // "UserId"
///     Console.WriteLine(ex.Message);
///     // "Cannot update key property 'UserId'..."
/// }
/// 
/// // ✓ Valid: Only update non-key properties
/// table.Users.Update(userId)
///     .Set(x => new UserUpdateModel 
///     {
///         Name = "John"
///     })
///     .UpdateAsync();
/// 
/// try
/// {
///     // ✗ Invalid: Attempting to remove sort key
///     table.Users.Update(userId)
///         .Set(x => new UserUpdateModel 
///         {
///             Email = x.Email.Remove()  // Cannot remove key!
///         })
///         .UpdateAsync();
/// }
/// catch (InvalidUpdateOperationException ex)
/// {
///     Console.WriteLine($"Property: {ex.PropertyName}"); // "Email"
/// }
/// </code>
/// </example>
public class InvalidUpdateOperationException : ExpressionTranslationException
{
    /// <summary>
    /// Gets the name of the property that caused the invalid operation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidUpdateOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="expression">The original expression.</param>
    public InvalidUpdateOperationException(string message, string propertyName, Expression? expression = null)
        : base(message, expression)
    {
        PropertyName = propertyName;
    }
}
