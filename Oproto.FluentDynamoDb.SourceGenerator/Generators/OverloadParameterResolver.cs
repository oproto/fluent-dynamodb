using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Resolves source property names to their types for typed parameter convenience overload generation.
/// </summary>
internal static class OverloadParameterResolver
{
    /// <summary>
    /// Represents a resolved parameter for a typed convenience overload.
    /// </summary>
    internal sealed class ParameterInfo
    {
        public string Name { get; }
        public string Type { get; }
        public bool IsNullable { get; }

        public ParameterInfo(string name, string type, bool isNullable)
        {
            Name = name;
            Type = type;
            IsNullable = isNullable;
        }
    }

    /// <summary>
    /// Resolves the parameter list for a typed convenience overload from a computed key property.
    /// Returns null if any source property cannot be resolved to a property in EntityModel.Properties.
    /// </summary>
    /// <param name="entity">The entity model containing all properties.</param>
    /// <param name="keyProperty">The computed key property whose source properties are being resolved.</param>
    /// <returns>A list of resolved parameters, or null if any source property cannot be found.</returns>
    internal static List<ParameterInfo>? ResolveParameters(EntityModel entity, PropertyModel keyProperty)
    {
        var parameters = new List<ParameterInfo>();
        foreach (var sourcePropName in keyProperty.ComputedKey!.SourceProperties)
        {
            var prop = entity.Properties.FirstOrDefault(
                p => p.PropertyName == sourcePropName);
            if (prop == null)
                return null; // unresolvable — diagnostic will be emitted by caller

            parameters.Add(new ParameterInfo(
                ToCamelCase(prop.PropertyName),
                prop.PropertyType,
                prop.IsNullable));
        }
        return parameters;
    }

    /// <summary>
    /// Returns the combined typed overload parameter list for an entity's keys (PK + SK) in declaration order.
    /// For computed keys, resolves individual source property parameters.
    /// For non-computed keys, uses a single string parameter with standard naming ("pK" or "sK").
    /// Returns null if any source property resolution fails.
    /// </summary>
    /// <param name="entity">The entity model to resolve parameters for.</param>
    /// <returns>The combined parameter list, or null if resolution fails.</returns>
    internal static List<ParameterInfo>? GetTypedOverloadParameters(EntityModel entity)
    {
        var parameters = new List<ParameterInfo>();
        var pk = entity.PartitionKeyProperty;
        var sk = entity.SortKeyProperty;

        if (pk?.IsComputed == true)
        {
            var pkParams = ResolveParameters(entity, pk);
            if (pkParams == null) return null;
            parameters.AddRange(pkParams);
        }
        else if (pk != null && !pk.IsConstantKey)
        {
            parameters.Add(new ParameterInfo("pK", "string", false));
        }

        if (sk?.IsComputed == true)
        {
            var skParams = ResolveParameters(entity, sk);
            if (skParams == null) return null;
            parameters.AddRange(skParams);
        }
        else if (sk != null && !sk.IsConstantKey)
        {
            parameters.Add(new ParameterInfo("sK", "string", false));
        }

        return parameters;
    }

    /// <summary>
    /// Returns the standard overload parameter types for the entity's existing string accessor methods.
    /// Uses "pK" for partition key and "sK" for sort key.
    /// </summary>
    /// <param name="entity">The entity model to get standard parameters for.</param>
    /// <returns>A list of standard overload parameters.</returns>
    internal static List<ParameterInfo> GetStandardOverloadParameters(EntityModel entity)
    {
        var parameters = new List<ParameterInfo>();
        if (entity.PartitionKeyProperty != null && !entity.PartitionKeyProperty.IsConstantKey)
            parameters.Add(new ParameterInfo("pK", "string", false));
        if (entity.SortKeyProperty != null && !entity.SortKeyProperty.IsConstantKey)
            parameters.Add(new ParameterInfo("sK", "string", false));
        return parameters;
    }

    /// <summary>
    /// Converts a property name to camelCase (first character lowercased, rest unchanged).
    /// </summary>
    /// <param name="propertyName">The property name to convert.</param>
    /// <returns>The camelCase version of the property name.</returns>
    internal static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}
