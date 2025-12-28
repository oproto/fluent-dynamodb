namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for chained list operations in update expressions.
/// These methods operate on List&lt;T&gt; (the return type of the first operation)
/// to enable method chaining like: x.Tags.SetAt(0, "a").SetAt(1, "b")
/// </summary>
/// <remarks>
/// <para><strong>Important:</strong></para>
/// <para>
/// These extension methods exist to support chaining of list operations.
/// The first operation in a chain uses <see cref="UpdateExpressionPropertyExtensions"/>
/// (which operates on UpdateExpressionProperty&lt;List&lt;T&gt;&gt;), but subsequent
/// chained operations use these methods (which operate on List&lt;T&gt;, the return type).
/// </para>
/// 
/// <para><strong>Example:</strong></para>
/// <code>
/// // First SetAt uses UpdateExpressionPropertyExtensions (on UpdateExpressionProperty&lt;List&lt;string&gt;&gt;)
/// // Second SetAt uses ListOperationExtensions (on List&lt;string&gt;, the return type)
/// .Set(x => new ItemUpdateModel { Tags = x.Tags.SetAt(0, "a").SetAt(1, "b") })
/// </code>
/// </remarks>
public static class ListOperationExtensions
{
    /// <summary>
    /// Sets the value at a specific index in a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="index">The zero-based index of the element to set.</param>
    /// <param name="value">The value to set at the specified index.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// This method enables chaining multiple SetAt operations:
    /// <c>x.Tags.SetAt(0, "a").SetAt(1, "b")</c>
    /// </para>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> SetAt<T>(this List<T> list, int index, T value)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Removes the element at a specific index from a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Note: RemoveAt cannot be chained with SetAt due to DynamoDB's overlapping path restriction.
    /// This method exists for completeness but chaining RemoveAt with other operations will throw.
    /// </para>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> RemoveAt<T>(this List<T> list, int index)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Appends an element to the end of a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="item">The item to append to the end of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Note: Append cannot be chained with SetAt/RemoveAt due to DynamoDB's overlapping path restriction.
    /// This method exists for completeness but chaining Append with index operations will throw.
    /// </para>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> Append<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Prepends an element to the beginning of a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="item">The item to prepend to the beginning of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Note: Prepend cannot be chained with SetAt/RemoveAt due to DynamoDB's overlapping path restriction.
    /// This method exists for completeness but chaining Prepend with index operations will throw.
    /// </para>
    /// </remarks>
    [ExpressionOnly]
    public static List<T> Prepend<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Appends multiple elements to the end of a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="items">The items to append to the end of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    [ExpressionOnly]
    public static List<T> AppendRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Prepends multiple elements to the beginning of a list (for chained operations).
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list (result of a previous list operation).</param>
    /// <param name="items">The items to prepend to the beginning of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    [ExpressionOnly]
    public static List<T> PrependRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");
}
