namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for list operations in update expressions.
/// These methods are markers for expression translation and should not be called directly.
/// They will be recognized by the UpdateExpressionTranslator and converted to DynamoDB list operations.
/// </summary>
/// <remarks>
/// <para><strong>Important:</strong></para>
/// <para>
/// These extension methods are designed exclusively for use within lambda expressions passed to
/// Update().Set() and similar update methods. They are not meant to be called directly
/// in regular C# code and will throw <see cref="InvalidOperationException"/> if invoked.
/// </para>
/// 
/// <para><strong>How It Works:</strong></para>
/// <para>
/// When you use these methods in a lambda expression, the UpdateExpressionTranslator 
/// recognizes them and translates them to the corresponding DynamoDB list_append operations.
/// The methods themselves are never actually executed - they serve as markers in the expression tree.
/// </para>
/// 
/// <para><strong>DynamoDB List Operations:</strong></para>
/// <list type="bullet">
/// <item><description>Append: SET #attr = list_append(#attr, :val) - adds to end</description></item>
/// <item><description>Prepend: SET #attr = list_append(:val, #attr) - adds to beginning</description></item>
/// </list>
/// 
/// <para><strong>Example Usage:</strong></para>
/// <code>
/// // Append a single item to the end of a list
/// await table.Items.Update(itemId)
///     .Set(x => x.Tags.Append("new-tag"))
///     .UpdateAsync();
/// 
/// // Prepend a single item to the beginning of a list
/// await table.Items.Update(itemId)
///     .Set(x => x.Tags.Prepend("priority-tag"))
///     .UpdateAsync();
/// 
/// // Append multiple items to the end of a list
/// await table.Items.Update(itemId)
///     .Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2" }))
///     .UpdateAsync();
/// 
/// // Works with nested lists
/// await table.Orders.Update(orderId)
///     .Set(x => x.Metadata.Keywords.Append("sale"))
///     .UpdateAsync();
/// </code>
/// </remarks>
public static class ListOperationExtensions
{
    /// <summary>
    /// Appends an element to the end of a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to append to (typically an entity property).</param>
    /// <param name="item">The item to append to the end of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: SET #attr = list_append(#attr, :val)
    /// Where :val is a list containing the single item.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Add a new tag to a list of tags</description></item>
    /// <item><description>Append a log entry to an audit trail</description></item>
    /// <item><description>Add a new item to a shopping cart</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>The item is wrapped in a list for the list_append operation</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Append a single tag
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.Append("new-tag"))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags = list_append(#tags, :v0)
    /// // Where :v0 = { L: [{ S: "new-tag" }] }
    /// 
    /// // Append to nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.Append("sale"))
    ///     .UpdateAsync();
    /// // Translates to: SET #metadata.#keywords = list_append(#metadata.#keywords, :v0)
    /// 
    /// // Append complex object
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.LineItems.Append(new LineItem { ProductId = "123", Quantity = 1 }))
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> Append<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Prepends an element to the beginning of a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to prepend to (typically an entity property).</param>
    /// <param name="item">The item to prepend to the beginning of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: SET #attr = list_append(:val, #attr)
    /// Where :val is a list containing the single item.
    /// Note the reversed order compared to Append - the value comes first.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Add a priority item to the front of a queue</description></item>
    /// <item><description>Insert a new entry at the beginning of a history list</description></item>
    /// <item><description>Add a pinned item to the top of a list</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>The item is wrapped in a list for the list_append operation</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Prepend a priority tag
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.Prepend("priority-tag"))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags = list_append(:v0, #tags)
    /// // Where :v0 = { L: [{ S: "priority-tag" }] }
    /// 
    /// // Prepend to nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.Prepend("urgent"))
    ///     .UpdateAsync();
    /// // Translates to: SET #metadata.#keywords = list_append(:v0, #metadata.#keywords)
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> Prepend<T>(this List<T> list, T item)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Appends multiple elements to the end of a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to append to (typically an entity property).</param>
    /// <param name="items">The items to append to the end of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: SET #attr = list_append(#attr, :val)
    /// Where :val is a list containing all the items.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Add multiple tags at once</description></item>
    /// <item><description>Batch append log entries</description></item>
    /// <item><description>Add multiple items to a collection in a single operation</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>More efficient than multiple Append calls for adding several items</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Append multiple tags
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2", "tag3" }))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags = list_append(#tags, :v0)
    /// // Where :v0 = { L: [{ S: "tag1" }, { S: "tag2" }, { S: "tag3" }] }
    /// 
    /// // Append from a variable
    /// var newTags = new List&lt;string&gt; { "featured", "sale" };
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.AppendRange(newTags))
    ///     .UpdateAsync();
    /// 
    /// // Append to nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.AppendRange(new[] { "sale", "discount" }))
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> AppendRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Prepends multiple elements to the beginning of a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to prepend to (typically an entity property).</param>
    /// <param name="items">The items to prepend to the beginning of the list.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>
    /// Translates to: SET #attr = list_append(:val, #attr)
    /// Where :val is a list containing all the items.
    /// Note the reversed order compared to AppendRange - the value comes first.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Add multiple priority items to the front of a queue</description></item>
    /// <item><description>Insert multiple entries at the beginning of a history list</description></item>
    /// <item><description>Add multiple pinned items to the top of a list</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>More efficient than multiple Prepend calls for adding several items</description></item>
    /// <item><description>Items are added in the order provided (first item in the enumerable will be first in the list)</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Prepend multiple priority tags
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.PrependRange(new[] { "urgent", "priority" }))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags = list_append(:v0, #tags)
    /// // Where :v0 = { L: [{ S: "urgent" }, { S: "priority" }] }
    /// 
    /// // Prepend from a variable
    /// var priorityTags = new List&lt;string&gt; { "featured", "pinned" };
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.PrependRange(priorityTags))
    ///     .UpdateAsync();
    /// 
    /// // Prepend to nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.PrependRange(new[] { "urgent", "expedite" }))
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> PrependRange<T>(this List<T> list, IEnumerable<T> items)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Sets the value at a specific index in a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to update (typically an entity property).</param>
    /// <param name="index">The zero-based index of the element to set.</param>
    /// <param name="value">The value to set at the specified index.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>Translates to: SET #attr[index] = :val</para>
    /// 
    /// <para><strong>Dynamic Index Support:</strong></para>
    /// <para>
    /// The index can be a constant, variable, property access, or method call,
    /// as long as it does not reference the entity parameter. The index expression
    /// is evaluated at translation time to produce the integer value used in the
    /// DynamoDB expression.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Update a specific element in a list by position</description></item>
    /// <item><description>Replace an item at a known index</description></item>
    /// <item><description>Modify list elements without replacing the entire list</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Index must be non-negative (validated at translation time)</description></item>
    /// <item><description>If the index doesn't exist, DynamoDB will create it (sparse list behavior)</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// <item><description>Multiple SetAt calls with different indices can be chained</description></item>
    /// </list>
    /// 
    /// <para><strong>Chaining Support:</strong></para>
    /// <para>
    /// Multiple SetAt calls can be chained to update different indices in a single operation:
    /// <c>x.Tags.SetAt(0, "a").SetAt(1, "b")</c> generates <c>SET #tags[0] = :v0, #tags[1] = :v1</c>
    /// </para>
    /// <para>
    /// However, SetAt cannot be chained with Append, Prepend, or RemoveAt on the same list
    /// due to DynamoDB's overlapping document path restriction.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Constant index
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.SetAt(0, "updated"))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags[0] = :v0
    /// 
    /// // Variable index
    /// int idx = GetIndex();
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.SetAt(idx, "updated"))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags[N] = :v0 (where N is the evaluated value of idx)
    /// 
    /// // Method call index
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.SetAt(GetTargetIndex(), "updated"))
    ///     .UpdateAsync();
    /// 
    /// // Property access index
    /// var config = GetConfig();
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.SetAt(config.TargetIndex, "updated"))
    ///     .UpdateAsync();
    /// 
    /// // Nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.SetAt(0, "updated"))
    ///     .UpdateAsync();
    /// // Translates to: SET #metadata.#keywords[0] = :v0
    /// 
    /// // Chained SetAt (multiple indices)
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.SetAt(0, "first").SetAt(1, "second"))
    ///     .UpdateAsync();
    /// // Translates to: SET #tags[0] = :v0, #tags[1] = :v1
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> SetAt<T>(this List<T> list, int index, T value)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Removes the element at a specific index from a list.
    /// This method is only for use in update expressions and will be translated to DynamoDB syntax.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to update (typically an entity property).</param>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <returns>Always throws an exception if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para><strong>DynamoDB Translation:</strong></para>
    /// <para>Translates to: REMOVE #attr[index]</para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>Removes the element at the specified index</description></item>
    /// <item><description>Elements after the removed index shift down</description></item>
    /// <item><description>If the index doesn't exist, the operation succeeds without error</description></item>
    /// </list>
    /// 
    /// <para><strong>Dynamic Index Support:</strong></para>
    /// <para>
    /// The index can be a constant, variable, property access, or method call,
    /// as long as it does not reference the entity parameter. The index expression
    /// is evaluated at translation time to produce the integer value used in the
    /// DynamoDB expression.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Remove a specific element from a list by position</description></item>
    /// <item><description>Delete an item at a known index</description></item>
    /// <item><description>Remove list elements without replacing the entire list</description></item>
    /// </list>
    /// 
    /// <para><strong>Important Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>Index must be non-negative (validated at translation time)</description></item>
    /// <item><description>Works with nested lists via document paths</description></item>
    /// <item><description>Cannot be used in filter or condition expressions</description></item>
    /// <item><description>Cannot be chained with SetAt, Append, or Prepend on the same list due to DynamoDB's overlapping document path restriction</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Constant index
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.RemoveAt(2))
    ///     .UpdateAsync();
    /// // Translates to: REMOVE #tags[2]
    /// 
    /// // Variable index
    /// int idx = GetIndexToRemove();
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.RemoveAt(idx))
    ///     .UpdateAsync();
    /// // Translates to: REMOVE #tags[N] (where N is the evaluated value of idx)
    /// 
    /// // Method call index
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.RemoveAt(GetTargetIndex()))
    ///     .UpdateAsync();
    /// 
    /// // Property access index
    /// var config = GetConfig();
    /// await table.Items.Update(itemId)
    ///     .Set(x => x.Tags.RemoveAt(config.TargetIndex))
    ///     .UpdateAsync();
    /// 
    /// // Nested list
    /// await table.Orders.Update(orderId)
    ///     .Set(x => x.Metadata.Keywords.RemoveAt(1))
    ///     .UpdateAsync();
    /// // Translates to: REMOVE #metadata.#keywords[1]
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> RemoveAt<T>(this List<T> list, int index)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions and should not be called directly.");
}
