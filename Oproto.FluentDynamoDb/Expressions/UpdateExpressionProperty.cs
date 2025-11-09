namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Wrapper type for properties in update expressions that enables type-safe extension methods.
/// </summary>
/// <typeparam name="T">The underlying property type.</typeparam>
/// <remarks>
/// <para>
/// UpdateExpressionProperty&lt;T&gt; is a marker type used exclusively in expression trees to enable
/// type-safe update operations through extension methods. It should never be instantiated directly
/// by user code.
/// </para>
/// 
/// <para><strong>Purpose:</strong></para>
/// <para>
/// This wrapper type enables extension methods to be scoped by generic type constraints, allowing
/// operations like Add(), Remove(), and Delete() to only be available on appropriate property types.
/// For example, Add() is only available on numeric types and sets, while Delete() is only available
/// on set types.
/// </para>
/// 
/// <para><strong>Usage:</strong></para>
/// <para>
/// This type is used in source-generated {Entity}UpdateExpressions classes. Developers interact
/// with it through lambda expressions passed to the Set() method on UpdateItemRequestBuilder.
/// The expression translator analyzes the expression tree and translates operations into DynamoDB
/// update expression syntax.
/// </para>
/// 
/// <para><strong>Important:</strong></para>
/// <para>
/// Do not attempt to instantiate this class directly. It exists only as a compile-time marker
/// for expression translation. All extension methods on this type throw exceptions if called
/// at runtime - they are only meant to be analyzed in expression trees.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Used in source-generated UpdateExpressions class
/// public partial class UserUpdateExpressions
/// {
///     public UpdateExpressionProperty&lt;string&gt; Name { get; } = new();
///     public UpdateExpressionProperty&lt;int&gt; LoginCount { get; } = new();
///     public UpdateExpressionProperty&lt;HashSet&lt;string&gt;&gt; Tags { get; } = new();
/// }
/// 
/// // Used in update expressions
/// table.Users.Update(userId)
///     .Set(x => new UserUpdateModel 
///     {
///         Name = "John",                      // Simple assignment
///         LoginCount = x.LoginCount.Add(1),   // Atomic increment
///         Tags = x.Tags.Delete("old-tag")     // Remove from set
///     })
///     .UpdateAsync();
/// </code>
/// </example>
public sealed class UpdateExpressionProperty<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExpressionProperty{T}"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor is public to allow source-generated UpdateExpressions classes to create instances.
    /// However, user code should never instantiate this type directly - it exists only as a marker
    /// for expression translation.
    /// </remarks>
    public UpdateExpressionProperty()
    {
    }

    /// <summary>
    /// Addition operator for arithmetic operations in update expressions.
    /// Translates to DynamoDB SET syntax: SET #attr = #attr + :val
    /// </summary>
    /// <param name="left">The property to update.</param>
    /// <param name="right">The value to add.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this operator is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// This operator enables arithmetic addition in update expressions. It is only meant to be used
    /// in expression trees passed to the Set() method and will throw an exception if called at runtime.
    /// </para>
    /// <para>
    /// The operator is only available when T is a numeric type (int, long, decimal, double, etc.).
    /// For atomic increment operations, consider using the Add() extension method instead, which
    /// generates an ADD action rather than a SET action.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Increment score by 10
    /// .Set(x => new UserUpdateModel { Score = x.Score + 10 })
    /// 
    /// // Add bonus to balance
    /// var bonus = 50.0m;
    /// .Set(x => new UserUpdateModel { Balance = x.Balance + bonus })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T operator +(UpdateExpressionProperty<T> left, T right)
        => throw new InvalidOperationException(
            "This operator is only for use in update expressions and should not be called directly. " +
            "Use it within a lambda expression passed to the Set() method.");

    /// <summary>
    /// Subtraction operator for arithmetic operations in update expressions.
    /// Translates to DynamoDB SET syntax: SET #attr = #attr - :val
    /// </summary>
    /// <param name="left">The property to update.</param>
    /// <param name="right">The value to subtract.</param>
    /// <returns>Never returns - throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this operator is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// This operator enables arithmetic subtraction in update expressions. It is only meant to be used
    /// in expression trees passed to the Set() method and will throw an exception if called at runtime.
    /// </para>
    /// <para>
    /// The operator is only available when T is a numeric type (int, long, decimal, double, etc.).
    /// For atomic decrement operations, consider using the Add() extension method with a negative value,
    /// which generates an ADD action rather than a SET action.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Decrease score by 5
    /// .Set(x => new UserUpdateModel { Score = x.Score - 5 })
    /// 
    /// // Subtract fee from balance
    /// var fee = 2.50m;
    /// .Set(x => new UserUpdateModel { Balance = x.Balance - fee })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T operator -(UpdateExpressionProperty<T> left, T right)
        => throw new InvalidOperationException(
            "This operator is only for use in update expressions and should not be called directly. " +
            "Use it within a lambda expression passed to the Set() method.");
}
