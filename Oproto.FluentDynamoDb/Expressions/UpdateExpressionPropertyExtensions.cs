namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Extension methods for update expression operations on <see cref="UpdateExpressionProperty{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// These methods are markers for expression translation and should not be called directly.
/// They are designed to be used within lambda expressions passed to the Set() method on
/// UpdateItemRequestBuilder, where they are analyzed and translated into DynamoDB update
/// expression syntax.
/// </para>
/// 
/// <para><strong>Important:</strong></para>
/// <para>
/// All methods in this class throw <see cref="InvalidOperationException"/> if called directly.
/// They exist only to provide IntelliSense support and compile-time type safety for update
/// expressions.
/// </para>
/// 
/// <para><strong>Type Safety:</strong></para>
/// <para>
/// Extension methods are constrained by generic type parameters to ensure they are only
/// available on appropriate property types. For example, Add() is only available on numeric
/// types and sets, while Delete() is only available on set types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Atomic increment
/// .Set(x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) })
/// 
/// // Add to set
/// .Set(x => new UserUpdateModel { Tags = x.Tags.Add("premium", "verified") })
/// 
/// // Remove attribute
/// .Set(x => new UserUpdateModel { TempData = x.TempData.Remove() })
/// 
/// // Delete from set
/// .Set(x => new UserUpdateModel { Tags = x.Tags.Delete("old-tag") })
/// 
/// // if_not_exists function
/// .Set(x => new UserUpdateModel { ViewCount = x.ViewCount.IfNotExists(0) })
/// 
/// // List append
/// .Set(x => new UserUpdateModel { History = x.History.ListAppend("new-event") })
/// 
/// // List prepend
/// .Set(x => new UserUpdateModel { History = x.History.ListPrepend("new-event") })
/// </code>
/// </example>
public static class UpdateExpressionPropertyExtensions
{
    #region Add Operations

    /// <summary>
    /// Performs an atomic ADD operation for int properties.
    /// </summary>
    /// <param name="property">The property to increment or decrement.</param>
    /// <param name="value">The value to add. Use negative values for decrement.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB ADD action: <c>ADD #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The ADD action atomically increments or decrements a numeric attribute. This is useful
    /// for counters and other numeric values that need to be updated concurrently without
    /// race conditions.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is initialized to the specified value</description></item>
    /// <item><description>If the attribute exists, the value is added to the current value</description></item>
    /// <item><description>Use negative values to decrement</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Increment login count by 1
    /// .Set(x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) })
    /// 
    /// // Decrement score by 10
    /// .Set(x => new UserUpdateModel { Score = x.Score.Add(-10) })
    /// 
    /// // Increment with variable
    /// int increment = 5;
    /// .Set(x => new UserUpdateModel { Points = x.Points.Add(increment) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static int Add(this UpdateExpressionProperty<int> property, int value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Performs an atomic ADD operation for long properties.
    /// </summary>
    /// <param name="property">The property to increment or decrement.</param>
    /// <param name="value">The value to add. Use negative values for decrement.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB ADD action: <c>ADD #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The ADD action atomically increments or decrements a numeric attribute. This is useful
    /// for counters and other numeric values that need to be updated concurrently without
    /// race conditions.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Increment view count
    /// .Set(x => new ArticleUpdateModel { ViewCount = x.ViewCount.Add(1L) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static long Add(this UpdateExpressionProperty<long> property, long value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Performs an atomic ADD operation for decimal properties.
    /// </summary>
    /// <param name="property">The property to increment or decrement.</param>
    /// <param name="value">The value to add. Use negative values for decrement.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB ADD action: <c>ADD #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The ADD action atomically increments or decrements a numeric attribute. This is useful
    /// for financial calculations and other decimal values that need to be updated concurrently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add to account balance
    /// .Set(x => new AccountUpdateModel { Balance = x.Balance.Add(100.50m) })
    /// 
    /// // Subtract from balance
    /// .Set(x => new AccountUpdateModel { Balance = x.Balance.Add(-50.25m) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static decimal Add(this UpdateExpressionProperty<decimal> property, decimal value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Performs an atomic ADD operation for double properties.
    /// </summary>
    /// <param name="property">The property to increment or decrement.</param>
    /// <param name="value">The value to add. Use negative values for decrement.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB ADD action: <c>ADD #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The ADD action atomically increments or decrements a numeric attribute. This is useful
    /// for measurements and other floating-point values that need to be updated concurrently.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add to temperature reading
    /// .Set(x => new SensorUpdateModel { Temperature = x.Temperature.Add(2.5) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static double Add(this UpdateExpressionProperty<double> property, double value)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Adds elements to a set using DynamoDB's ADD action.
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="property">The set property.</param>
    /// <param name="elements">Elements to add to the set.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB ADD action: <c>ADD #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The ADD action for sets performs a set union operation, adding the specified elements
    /// to the existing set. Elements that already exist in the set are ignored.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is created with the specified elements</description></item>
    /// <item><description>If the attribute exists, the elements are added to the existing set</description></item>
    /// <item><description>Duplicate elements are automatically handled by DynamoDB</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Add single tag
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Add("premium") })
    /// 
    /// // Add multiple tags
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Add("premium", "verified", "active") })
    /// 
    /// // Add tags from variable
    /// var newTags = new[] { "premium", "verified" };
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Add(newTags) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static HashSet<T> Add<T>(this UpdateExpressionProperty<HashSet<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    #endregion

    #region Remove Operations

    /// <summary>
    /// Removes an attribute using DynamoDB's REMOVE action.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to remove.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB REMOVE action: <c>REMOVE #attr</c>
    /// </para>
    /// 
    /// <para>
    /// The REMOVE action deletes an attribute from an item. This is different from setting
    /// a value to null - REMOVE completely removes the attribute from the item.
    /// </para>
    /// 
    /// <para><strong>Important:</strong></para>
    /// <list type="bullet">
    /// <item><description>Cannot remove partition key or sort key attributes</description></item>
    /// <item><description>If the attribute doesn't exist, the operation succeeds without error</description></item>
    /// <item><description>Can be used to remove optional attributes</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove temporary data
    /// .Set(x => new UserUpdateModel { TempData = x.TempData.Remove() })
    /// 
    /// // Remove multiple attributes
    /// .Set(x => new UserUpdateModel 
    /// {
    ///     TempData = x.TempData.Remove(),
    ///     CachedValue = x.CachedValue.Remove()
    /// })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T Remove<T>(this UpdateExpressionProperty<T> property)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    #endregion

    #region Delete Operations

    /// <summary>
    /// Removes elements from a set using DynamoDB's DELETE action.
    /// </summary>
    /// <typeparam name="T">The element type of the set.</typeparam>
    /// <param name="property">The set property.</param>
    /// <param name="elements">Elements to remove from the set.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB DELETE action: <c>DELETE #attr :val</c>
    /// </para>
    /// 
    /// <para>
    /// The DELETE action removes specific elements from a set. This is different from REMOVE,
    /// which deletes the entire attribute. DELETE performs a set difference operation.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>Only works with set types (SS, NS, BS)</description></item>
    /// <item><description>Removes only the specified elements from the set</description></item>
    /// <item><description>If all elements are removed, the attribute remains as an empty set</description></item>
    /// <item><description>If the attribute doesn't exist, the operation succeeds without error</description></item>
    /// <item><description>Elements that don't exist in the set are ignored</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Remove single tag
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Delete("old-tag") })
    /// 
    /// // Remove multiple tags
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Delete("old-tag", "deprecated", "unused") })
    /// 
    /// // Remove tags from variable
    /// var tagsToRemove = new[] { "old-tag", "deprecated" };
    /// .Set(x => new UserUpdateModel { Tags = x.Tags.Delete(tagsToRemove) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static HashSet<T> Delete<T>(this UpdateExpressionProperty<HashSet<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    #endregion

    #region DynamoDB Functions

    /// <summary>
    /// Uses DynamoDB's if_not_exists function to set a value only if the attribute doesn't exist.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="property">The property to check.</param>
    /// <param name="defaultValue">The value to set if the attribute doesn't exist.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB SET with if_not_exists function: <c>SET #attr = if_not_exists(#attr, :val)</c>
    /// </para>
    /// 
    /// <para>
    /// The if_not_exists function checks if an attribute exists in the item. If it doesn't exist,
    /// it sets the attribute to the specified default value. If it does exist, the current value
    /// is preserved.
    /// </para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Initializing counters to 0 if they don't exist</description></item>
    /// <item><description>Setting default values for optional attributes</description></item>
    /// <item><description>Ensuring an attribute has a value without overwriting existing data</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize view count to 0 if it doesn't exist
    /// .Set(x => new ArticleUpdateModel { ViewCount = x.ViewCount.IfNotExists(0) })
    /// 
    /// // Set default status if not present
    /// .Set(x => new UserUpdateModel { Status = x.Status.IfNotExists("active") })
    /// 
    /// // Initialize list if not present
    /// .Set(x => new UserUpdateModel { History = x.History.IfNotExists(new List&lt;string&gt;()) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static T IfNotExists<T>(this UpdateExpressionProperty<T> property, T defaultValue)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Appends elements to the end of a list using DynamoDB's list_append function.
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="property">The list property.</param>
    /// <param name="elements">Elements to append to the list.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB SET with list_append function: <c>SET #attr = list_append(#attr, :val)</c>
    /// </para>
    /// 
    /// <para>
    /// The list_append function concatenates two lists, adding the new elements to the end of
    /// the existing list. This is useful for maintaining ordered collections like event histories
    /// or activity logs.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is created with the specified elements</description></item>
    /// <item><description>If the attribute exists, the elements are appended to the end</description></item>
    /// <item><description>Maintains the order of elements</description></item>
    /// <item><description>Allows duplicate elements</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Append single event
    /// .Set(x => new UserUpdateModel { History = x.History.ListAppend("login") })
    /// 
    /// // Append multiple events
    /// .Set(x => new UserUpdateModel { History = x.History.ListAppend("login", "view-profile", "logout") })
    /// 
    /// // Append events from variable
    /// var events = new[] { "login", "view-profile" };
    /// .Set(x => new UserUpdateModel { History = x.History.ListAppend(events) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> ListAppend<T>(this UpdateExpressionProperty<List<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    /// <summary>
    /// Prepends elements to the beginning of a list using DynamoDB's list_append function.
    /// </summary>
    /// <typeparam name="T">The element type of the list.</typeparam>
    /// <param name="property">The list property.</param>
    /// <param name="elements">Elements to prepend to the list.</param>
    /// <returns>Never returns - this method throws if called directly.</returns>
    /// <exception cref="InvalidOperationException">Always thrown - this method is only for use in expressions.</exception>
    /// <remarks>
    /// <para>
    /// Translates to DynamoDB SET with list_append function: <c>SET #attr = list_append(:val, #attr)</c>
    /// </para>
    /// 
    /// <para>
    /// The list_append function concatenates two lists. By reversing the order of arguments,
    /// this method adds the new elements to the beginning of the existing list. This is useful
    /// for maintaining most-recent-first ordered collections.
    /// </para>
    /// 
    /// <para><strong>DynamoDB Behavior:</strong></para>
    /// <list type="bullet">
    /// <item><description>If the attribute doesn't exist, it is created with the specified elements</description></item>
    /// <item><description>If the attribute exists, the elements are prepended to the beginning</description></item>
    /// <item><description>Maintains the order of elements</description></item>
    /// <item><description>Allows duplicate elements</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Prepend single event (most recent first)
    /// .Set(x => new UserUpdateModel { RecentActivity = x.RecentActivity.ListPrepend("login") })
    /// 
    /// // Prepend multiple events
    /// .Set(x => new UserUpdateModel { RecentActivity = x.RecentActivity.ListPrepend("logout", "view-profile", "login") })
    /// 
    /// // Prepend events from variable
    /// var recentEvents = new[] { "logout", "view-profile" };
    /// .Set(x => new UserUpdateModel { RecentActivity = x.RecentActivity.ListPrepend(recentEvents) })
    /// </code>
    /// </example>
    [ExpressionOnly]
    public static List<T> ListPrepend<T>(this UpdateExpressionProperty<List<T>> property, params T[] elements)
        => throw new InvalidOperationException("This method is only for use in update expressions and should not be called directly.");

    #endregion
}
