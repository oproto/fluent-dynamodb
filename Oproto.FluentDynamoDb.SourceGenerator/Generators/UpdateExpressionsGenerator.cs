using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates UpdateExpressions and UpdateModel classes for type-safe update operations.
/// </summary>
/// <remarks>
/// <para>
/// For each entity with [DynamoDbTable] attribute, this generator creates two helper classes:
/// </para>
/// <list type="bullet">
/// <item><description>{Entity}UpdateExpressions: Contains UpdateExpressionProperty&lt;T&gt; properties for use in expressions</description></item>
/// <item><description>{Entity}UpdateModel: Contains nullable properties representing values to update</description></item>
/// </list>
/// <para>
/// These classes enable type-safe, expression-based update operations with IntelliSense support
/// and compile-time validation.
/// </para>
/// </remarks>
internal static class UpdateExpressionsGenerator
{
    /// <summary>
    /// Generates the UpdateExpressions class for an entity.
    /// </summary>
    /// <param name="entity">The entity model to generate the UpdateExpressions class for.</param>
    /// <returns>The generated C# source code.</returns>
    public static string GenerateUpdateExpressionsClass(EntityModel entity)
    {
        var sb = new StringBuilder();

        // File header
        FileHeaderGenerator.GenerateFileHeader(sb);

        // Using statements
        sb.AppendLine("using Oproto.FluentDynamoDb.Expressions;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // Class XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Expression parameter class for {entity.ClassName} update operations.");
        sb.AppendLine("    /// Properties are wrapped in UpdateExpressionProperty&lt;T&gt; to enable type-safe extension methods.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine("    /// This class is used as the parameter type in update expression lambdas. Each property");
        sb.AppendLine("    /// is wrapped in UpdateExpressionProperty&lt;T&gt; which enables extension methods like Add(),");
        sb.AppendLine("    /// Remove(), Delete(), and DynamoDB functions to be available based on the property type.");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// <para><strong>Usage:</strong></para>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine($"    /// table.{entity.ClassName}.Update(key)");
        sb.AppendLine($"    ///     .Set(x => new {entity.ClassName}UpdateModel");
        sb.AppendLine("    ///     {");
        sb.AppendLine("    ///         // Use x.PropertyName to access UpdateExpressionProperty&lt;T&gt; for operations");
        sb.AppendLine("    ///     })");
        sb.AppendLine("    ///     .UpdateAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </remarks>");

        // Class declaration
        sb.AppendLine($"    public partial class {entity.ClassName}UpdateExpressions");
        sb.AppendLine("    {");

        // Generate properties
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GenerateUpdateExpressionProperty(sb, property, entity);
        }

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the UpdateModel class for an entity.
    /// </summary>
    /// <param name="entity">The entity model to generate the UpdateModel class for.</param>
    /// <returns>The generated C# source code.</returns>
    public static string GenerateUpdateModelClass(EntityModel entity)
    {
        var sb = new StringBuilder();

        // File header
        FileHeaderGenerator.GenerateFileHeader(sb);

        // Using statements (add System for nullable types)
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // Class XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Return type for {entity.ClassName} update expressions.");
        sb.AppendLine("    /// Properties are nullable to indicate which attributes to update.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine("    /// This class is used as the return type in update expression lambdas. Properties can be");
        sb.AppendLine("    /// set to constant values, variables, or the result of operations like Add(), Remove(), etc.");
        sb.AppendLine("    /// Only properties that are assigned in the expression will be included in the update.");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// <para><strong>Usage:</strong></para>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine($"    /// table.{entity.ClassName}.Update(key)");
        sb.AppendLine($"    ///     .Set(x => new {entity.ClassName}UpdateModel");
        sb.AppendLine("    ///     {");
        sb.AppendLine("    ///         // Assign values to properties you want to update");
        sb.AppendLine("    ///     })");
        sb.AppendLine("    ///     .UpdateAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </remarks>");

        // Class declaration
        sb.AppendLine($"    public partial class {entity.ClassName}UpdateModel");
        sb.AppendLine("    {");

        // Generate properties
        foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
        {
            GenerateUpdateModelProperty(sb, property, entity);
        }

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a single UpdateExpressionProperty property.
    /// </summary>
    private static void GenerateUpdateExpressionProperty(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        var isKeyProperty = property.IsPartitionKey || property.IsSortKey;

        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Gets the {propertyName} property for use in update expressions.");
        sb.AppendLine("        /// </summary>");

        // Add additional documentation for key properties
        if (isKeyProperty)
        {
            sb.AppendLine("        /// <remarks>");
            sb.AppendLine("        /// <para><strong>Warning:</strong></para>");
            sb.AppendLine("        /// <para>");
            if (property.IsPartitionKey && property.IsSortKey)
            {
                sb.AppendLine("        /// This property is both a partition key and sort key and cannot be updated.");
            }
            else if (property.IsPartitionKey)
            {
                sb.AppendLine("        /// This property is a partition key and cannot be updated.");
            }
            else
            {
                sb.AppendLine("        /// This property is a sort key and cannot be updated.");
            }
            sb.AppendLine("        /// Attempting to update this property will result in an InvalidUpdateOperationException.");
            sb.AppendLine("        /// </para>");
            sb.AppendLine("        /// </remarks>");
        }

        // Add documentation about available operations based on type
        if (!isKeyProperty)
        {
            var operations = GetAvailableOperations(propertyType);
            if (operations.Count > 0)
            {
                if (!isKeyProperty)
                {
                    sb.AppendLine("        /// <remarks>");
                }
                sb.AppendLine("        /// <para><strong>Available Operations:</strong></para>");
                sb.AppendLine("        /// <list type=\"bullet\">");
                foreach (var operation in operations)
                {
                    sb.AppendLine($"        /// <item><description>{operation}</description></item>");
                }
                sb.AppendLine("        /// </list>");
                sb.AppendLine("        /// </remarks>");
            }
        }

        // Property declaration
        sb.AppendLine($"        public UpdateExpressionProperty<{propertyType}> {propertyName} {{ get; }} = new();");
    }

    /// <summary>
    /// Generates a single UpdateModel property.
    /// </summary>
    private static void GenerateUpdateModelProperty(StringBuilder sb, PropertyModel property, EntityModel entity)
    {
        var propertyName = property.PropertyName;
        var propertyType = property.PropertyType;
        var nullableType = MakeNullable(propertyType, property.IsNullable);
        var isKeyProperty = property.IsPartitionKey || property.IsSortKey;

        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Gets or sets the {propertyName} value to update.");
        sb.AppendLine("        /// </summary>");

        // Add documentation about setting values
        sb.AppendLine("        /// <remarks>");
        sb.AppendLine("        /// <para>");
        sb.AppendLine("        /// Can be set to a constant value, variable, or the result of an operation like Add(), Remove(), etc.");
        sb.AppendLine("        /// </para>");

        // Add warning for key properties
        if (isKeyProperty)
        {
            sb.AppendLine("        /// <para><strong>Warning:</strong></para>");
            sb.AppendLine("        /// <para>");
            if (property.IsPartitionKey && property.IsSortKey)
            {
                sb.AppendLine("        /// This property is both a partition key and sort key and cannot be updated.");
            }
            else if (property.IsPartitionKey)
            {
                sb.AppendLine("        /// This property is a partition key and cannot be updated.");
            }
            else
            {
                sb.AppendLine("        /// This property is a sort key and cannot be updated.");
            }
            sb.AppendLine("        /// Attempting to update this property will result in an InvalidUpdateOperationException.");
            sb.AppendLine("        /// </para>");
        }

        sb.AppendLine("        /// </remarks>");

        // Property declaration
        sb.AppendLine($"        public {nullableType} {propertyName} {{ get; set; }}");
    }

    /// <summary>
    /// Gets a list of available operations for a property type.
    /// </summary>
    private static List<string> GetAvailableOperations(string propertyType)
    {
        var operations = new List<string>();

        // Check for numeric types (support Add)
        if (IsNumericType(propertyType))
        {
            operations.Add("Add() - Atomic increment/decrement");
            operations.Add("Arithmetic (+, -) - Arithmetic operations in SET");
        }

        // Check for HashSet types (support Add and Delete)
        if (propertyType.StartsWith("HashSet<") || propertyType.StartsWith("System.Collections.Generic.HashSet<"))
        {
            operations.Add("Add() - Add elements to set");
            operations.Add("Delete() - Remove elements from set");
        }

        // Check for List types (support ListAppend and ListPrepend)
        if (propertyType.StartsWith("List<") || propertyType.StartsWith("System.Collections.Generic.List<"))
        {
            operations.Add("ListAppend() - Append elements to list");
            operations.Add("ListPrepend() - Prepend elements to list");
        }

        // All types support Remove and IfNotExists
        operations.Add("Remove() - Remove attribute");
        operations.Add("IfNotExists() - Set value if attribute doesn't exist");

        return operations;
    }

    /// <summary>
    /// Checks if a type is numeric.
    /// </summary>
    private static bool IsNumericType(string propertyType)
    {
        var numericTypes = new[]
        {
            "int", "System.Int32",
            "long", "System.Int64",
            "decimal", "System.Decimal",
            "double", "System.Double",
            "float", "System.Single",
            "short", "System.Int16",
            "byte", "System.Byte",
            "int?", "System.Int32?",
            "long?", "System.Int64?",
            "decimal?", "System.Decimal?",
            "double?", "System.Double?",
            "float?", "System.Single?",
            "short?", "System.Int16?",
            "byte?", "System.Byte?"
        };

        return numericTypes.Contains(propertyType);
    }

    /// <summary>
    /// Makes a type nullable if it isn't already.
    /// </summary>
    private static string MakeNullable(string propertyType, bool isAlreadyNullable)
    {
        // If already nullable, return as-is
        if (isAlreadyNullable || propertyType.EndsWith("?"))
        {
            return propertyType;
        }

        // Reference types are already nullable in nullable context
        if (IsReferenceType(propertyType))
        {
            return propertyType + "?";
        }

        // Value types need ? suffix
        return propertyType + "?";
    }

    /// <summary>
    /// Checks if a type is a reference type.
    /// </summary>
    private static bool IsReferenceType(string propertyType)
    {
        // Common reference types
        var referenceTypes = new[]
        {
            "string", "System.String",
            "object", "System.Object"
        };

        if (referenceTypes.Contains(propertyType))
        {
            return true;
        }

        // Collections are reference types
        if (propertyType.StartsWith("List<") || propertyType.StartsWith("System.Collections.Generic.List<") ||
            propertyType.StartsWith("HashSet<") || propertyType.StartsWith("System.Collections.Generic.HashSet<") ||
            propertyType.StartsWith("Dictionary<") || propertyType.StartsWith("System.Collections.Generic.Dictionary<") ||
            propertyType.EndsWith("[]"))
        {
            return true;
        }

        // Value types
        var valueTypes = new[]
        {
            "int", "System.Int32",
            "long", "System.Int64",
            "decimal", "System.Decimal",
            "double", "System.Double",
            "float", "System.Single",
            "short", "System.Int16",
            "byte", "System.Byte",
            "bool", "System.Boolean",
            "DateTime", "System.DateTime",
            "DateTimeOffset", "System.DateTimeOffset",
            "TimeSpan", "System.TimeSpan",
            "Guid", "System.Guid"
        };

        return !valueTypes.Contains(propertyType);
    }
}
