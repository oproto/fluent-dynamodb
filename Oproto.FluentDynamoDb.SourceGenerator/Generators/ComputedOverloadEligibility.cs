using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Encapsulates the decision logic for whether an entity qualifies for
/// typed parameter convenience overloads or KeyInputMode parameter injection.
/// </summary>
internal static class ComputedOverloadEligibility
{
    /// <summary>
    /// Determines whether an entity qualifies for typed parameter convenience overloads.
    /// Returns true when at least one key has IsComputed == true and ComputedKey.SourceProperties.Length >= 2.
    /// </summary>
    /// <param name="entity">The entity model to evaluate.</param>
    /// <returns>True if the entity qualifies for a typed parameter convenience overload.</returns>
    internal static bool QualifiesForTypedOverload(EntityModel entity)
    {
        var pk = entity.PartitionKeyProperty;
        var sk = entity.SortKeyProperty;

        bool pkComputed = pk?.IsComputed == true
            && pk.ComputedKey!.SourceProperties.Length >= 2;
        bool skComputed = sk?.IsComputed == true
            && sk.ComputedKey!.SourceProperties.Length >= 2;

        return pkComputed || skComputed;
    }

    /// <summary>
    /// Determines whether the generated typed overload would be ambiguous with existing overloads.
    /// Compares the typed overload parameter types/count against the standard overload parameters.
    /// If all computed source properties resolve to the same types as the existing overload
    /// and the count matches, it's considered ambiguous.
    /// </summary>
    /// <param name="entity">The entity model to evaluate.</param>
    /// <returns>True if the typed overload would be ambiguous with the standard overload.</returns>
    internal static bool WouldBeAmbiguous(EntityModel entity)
    {
        var typedParams = OverloadParameterResolver.GetTypedOverloadParameters(entity);
        var standardParams = OverloadParameterResolver.GetStandardOverloadParameters(entity);

        if (typedParams == null) return true; // unresolvable = treat as ambiguous

        return typedParams.Count == standardParams.Count
            && typedParams.Zip(standardParams, (t, s) => t.Type == s.Type).All(x => x);
    }

    /// <summary>
    /// Determines whether an entity qualifies for KeyInputMode parameter injection.
    /// Returns true when at least one string key has a prefix AND no non-ambiguous typed overload exists.
    /// </summary>
    /// <param name="entity">The entity model to evaluate.</param>
    /// <returns>True if the entity qualifies for KeyInputMode parameter injection.</returns>
    internal static bool QualifiesForKeyInputMode(EntityModel entity)
    {
        if (QualifiesForTypedOverload(entity) && !WouldBeAmbiguous(entity))
            return false; // typed overload handles disambiguation

        var pk = entity.PartitionKeyProperty;
        var sk = entity.SortKeyProperty;

        bool pkEligible = pk != null
            && pk.PropertyType == "string"
            && !string.IsNullOrEmpty(pk.KeyFormat?.Prefix);
        bool skEligible = sk != null
            && sk.PropertyType == "string"
            && !string.IsNullOrEmpty(sk.KeyFormat?.Prefix);

        return pkEligible || skEligible;
    }
}
