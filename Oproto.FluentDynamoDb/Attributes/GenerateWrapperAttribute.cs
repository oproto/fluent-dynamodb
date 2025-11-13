using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks an extension method for automatic wrapper generation in entity-specific builders.
/// The source generator will create wrapper methods that maintain the correct return type
/// for fluent chaining while delegating to the extension method implementation.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is used by the source generator to automatically create wrapper methods
/// in entity-specific update builders (e.g., OrderUpdateBuilder). Wrappers ensure that
/// fluent method chaining maintains the entity-specific builder type, allowing access to
/// simplified methods like Set() that don't require explicit generic type parameters.
/// </para>
/// <para>
/// <strong>Simple Wrappers (RequiresSpecialization = false):</strong>
/// For methods that only need return type covariance, the generator creates a simple
/// wrapper that delegates to the extension method and returns the entity-specific builder.
/// </para>
/// <para>
/// <strong>Specialized Wrappers (RequiresSpecialization = true):</strong>
/// For methods with generic type parameters that should be fixed to the entity type,
/// the generator applies specialization rules based on the method signature pattern.
/// Common patterns include fixing TEntity in Where() expressions or fixing TEntity and
/// TUpdateExpressions in Set() expressions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple wrapper - only return type changes
/// [GenerateWrapper]
/// public static T Where&lt;T&gt;(this IWithConditionExpression&lt;T&gt; builder, string conditionExpression)
/// {
///     return builder.SetConditionExpression(conditionExpression);
/// }
/// 
/// // Specialized wrapper - TEntity parameter is fixed
/// [GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter")]
/// public static T Where&lt;T, TEntity&gt;(
///     this IWithConditionExpression&lt;T&gt; builder,
///     Expression&lt;Func&lt;TEntity, bool&gt;&gt; expression,
///     EntityMetadata? metadata = null)
/// {
///     // Implementation...
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GenerateWrapperAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether this method requires generic type specialization.
    /// </summary>
    /// <value>
    /// <c>true</c> if the generator should apply specialization rules based on the method signature;
    /// <c>false</c> if the generator should create a simple wrapper that only changes the return type.
    /// </value>
    /// <remarks>
    /// <para>
    /// When <c>false</c> (default), the generator creates a simple wrapper that:
    /// <list type="bullet">
    /// <item>Maintains the same method signature</item>
    /// <item>Changes the return type to the entity-specific builder</item>
    /// <item>Delegates to the extension method</item>
    /// <item>Returns <c>this</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// When <c>true</c>, the generator applies pattern matching to determine how to specialize
    /// generic type parameters. For example:
    /// <list type="bullet">
    /// <item>Methods with <c>Expression&lt;Func&lt;TEntity, bool&gt;&gt;</c> fix TEntity to the builder's entity type</item>
    /// <item>Methods with <c>Expression&lt;Func&lt;TUpdateExpressions, TUpdateModel&gt;&gt;</c> fix TEntity and TUpdateExpressions</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool RequiresSpecialization { get; set; }

    /// <summary>
    /// Gets or sets optional notes describing the specialization requirements for this method.
    /// </summary>
    /// <value>
    /// A description of how the method should be specialized, or <c>null</c> if no notes are provided.
    /// </value>
    /// <remarks>
    /// This property is primarily for documentation purposes and to help maintainers understand
    /// the specialization logic. The source generator uses pattern matching on the method signature
    /// to determine the actual specialization rules, but these notes can clarify the intent.
    /// </remarks>
    /// <example>
    /// <code>
    /// [GenerateWrapper(
    ///     RequiresSpecialization = true,
    ///     SpecializationNotes = "Fixes TEntity and TUpdateExpressions generic parameters")]
    /// public static TBuilder Set&lt;TEntity, TUpdateExpressions, TUpdateModel&gt;(...)
    /// </code>
    /// </example>
    public string? SpecializationNotes { get; set; }
}
