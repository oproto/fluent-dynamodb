using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Enables FluentResults API generation for the decorated entity or table.
/// When applied, the source generator will generate Result-returning convenience methods
/// on the entity accessor (e.g., GetAsyncResult, PutAsyncResult, DeleteAsyncResult, QueryAsyncResult).
/// </summary>
/// <remarks>
/// <para>
/// This attribute provides an opt-in mechanism for using the FluentResults pattern
/// instead of traditional exception-based error handling. When applied to an entity class,
/// the source generator creates methods that return <c>Result&lt;T&gt;</c> types from the
/// FluentResults library.
/// </para>
/// <para>
/// <strong>Generated Methods:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><c>GetAsyncResult</c> - Returns <c>Result&lt;T?&gt;</c> instead of throwing exceptions</description></item>
/// <item><description><c>PutAsyncResult</c> - Returns <c>Result</c> instead of throwing exceptions</description></item>
/// <item><description><c>DeleteAsyncResult</c> - Returns <c>Result</c> instead of throwing exceptions</description></item>
/// <item><description><c>QueryAsyncResult</c> - Returns <c>Result&lt;List&lt;T&gt;&gt;</c> instead of throwing exceptions</description></item>
/// </list>
/// <para>
/// <strong>Usage:</strong>
/// </para>
/// <code>
/// // Apply to entity class
/// [DynamoDbTable("Users")]
/// [UseFluentResults]
/// public partial class User
/// {
///     [PartitionKey]
///     [DynamoDbAttribute("pk")]
///     public string UserId { get; set; } = string.Empty;
/// }
/// 
/// // Generated accessor methods:
/// var result = await table.Users.GetAsyncResult(userId);
/// if (result.IsSuccess)
/// {
///     var user = result.Value;
/// }
/// else
/// {
///     foreach (var error in result.Errors)
///     {
///         Console.WriteLine(error.Message);
///     }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class UseFluentResultsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether to suppress generation of traditional async methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c> (default), the source generator will only generate Result-returning methods
    /// (e.g., <c>GetAsyncResult</c>, <c>PutAsyncResult</c>) and will suppress the traditional
    /// async methods (e.g., <c>GetAsync</c>, <c>PutAsync</c>).
    /// </para>
    /// <para>
    /// When <c>false</c>, both traditional async methods and Result-returning methods will be generated,
    /// allowing gradual migration to the FluentResults pattern.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Only generate Result-returning methods (default)
    /// [UseFluentResults]
    /// public partial class User { }
    /// 
    /// // Generate both traditional and Result-returning methods
    /// [UseFluentResults(HideGeneratedAsyncMethods = false)]
    /// public partial class Order { }
    /// </code>
    /// </example>
    public bool HideGeneratedAsyncMethods { get; set; } = true;
}
