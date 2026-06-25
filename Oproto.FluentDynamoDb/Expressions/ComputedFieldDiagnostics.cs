namespace Oproto.FluentDynamoDb.Expressions;

/// <summary>
/// Diagnostic constants and helpers for computed field validation in update expressions.
/// These diagnostics are thrown as <see cref="InvalidOperationException"/> during expression translation
/// when computed field assignment rules are violated.
/// </summary>
/// <remarks>
/// <para><strong>Diagnostic Codes:</strong></para>
/// <list type="bullet">
/// <item><description><strong>FDDB071</strong>: Source property references the entity parameter (must use constant/local values)</description></item>
/// <item><description><strong>FDDB072</strong>: Partial source property assignment (all sources must be specified together)</description></item>
/// <item><description><strong>FDDB073</strong>: Mixed direct and source-based assignment (choose one approach)</description></item>
/// </list>
/// </remarks>
internal static class ComputedFieldDiagnostics
{
    /// <summary>
    /// FDDB071: Message template for when a source property assignment references the entity parameter.
    /// Placeholder {0} is the source property name.
    /// </summary>
    internal const string EntityParameterReferenceMessage =
        "Source properties of computed fields must be assigned constant or local values. " +
        "'{0}' references the entity parameter, but computed fields are evaluated client-side.";

    /// <summary>
    /// FDDB072: Message template for when only some source properties of a computed field are assigned.
    /// Placeholder {0} is the computed field name. Placeholder {1} is the comma-separated list of missing source properties.
    /// </summary>
    internal const string PartialSourceAssignmentMessage =
        "All source properties of computed field '{0}' must be specified when updating via sources. " +
        "Missing: {1}";

    /// <summary>
    /// FDDB073: Message template for when both a computed field and its source properties are assigned in the same expression.
    /// Placeholder {0} is the computed field name.
    /// </summary>
    internal const string MixedAssignmentMessage =
        "Cannot set both computed field '{0}' and its source properties " +
        "in the same update expression. Use one approach or the other.";

    /// <summary>
    /// Throws FDDB071: Entity parameter reference in computed field source property assignment.
    /// </summary>
    /// <param name="propertyName">The source property name that references the entity parameter.</param>
    /// <exception cref="InvalidOperationException">Always thrown with the FDDB071 message.</exception>
    internal static void ThrowEntityParameterReference(string propertyName)
    {
        throw new InvalidOperationException(
            string.Format(EntityParameterReferenceMessage, propertyName));
    }

    /// <summary>
    /// Throws FDDB072: Partial source property assignment for a computed field.
    /// </summary>
    /// <param name="computedFieldName">The name of the computed field.</param>
    /// <param name="missingProperties">The names of the missing source properties.</param>
    /// <exception cref="InvalidOperationException">Always thrown with the FDDB072 message.</exception>
    internal static void ThrowPartialSourceAssignment(string computedFieldName, IEnumerable<string> missingProperties)
    {
        throw new InvalidOperationException(
            string.Format(PartialSourceAssignmentMessage, computedFieldName, string.Join(", ", missingProperties)));
    }

    /// <summary>
    /// Throws FDDB073: Mixed direct and source-based assignment for a computed field.
    /// </summary>
    /// <param name="computedFieldName">The name of the computed field.</param>
    /// <exception cref="InvalidOperationException">Always thrown with the FDDB073 message.</exception>
    internal static void ThrowMixedAssignment(string computedFieldName)
    {
        throw new InvalidOperationException(
            string.Format(MixedAssignmentMessage, computedFieldName));
    }
}
