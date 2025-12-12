using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates security metadata for entities with sensitive or encrypted fields.
/// </summary>
internal static class SecurityMetadataGenerator
{
    /// <summary>
    /// Generates a static metadata class with sensitive field information.
    /// </summary>
    /// <param name="entity">The entity model.</param>
    /// <returns>The generated metadata class code, or empty string if no security attributes.</returns>
    public static string GenerateSecurityMetadata(EntityModel entity)
    {
        var sensitiveProperties = entity.Properties
            .Where(p => p.Security?.IsSensitive == true)
            .ToArray();

        // Check if dynamic fields are enabled with sensitive logging
        var hasDynamicFieldsSensitive = entity.EnableDynamicFields && entity.DynamicFieldsSensitiveLogging;

        // Only generate if there are sensitive fields or dynamic fields with sensitive logging
        if (sensitiveProperties.Length == 0 && !hasDynamicFieldsSensitive)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);

        // Using statements
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {entity.Namespace}");
        sb.AppendLine("{");

        // Static metadata class
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Security metadata for {entity.ClassName}.");
        sb.AppendLine($"    /// Contains information about sensitive fields for logging redaction.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    internal static class {entity.ClassName}SecurityMetadata");
        sb.AppendLine("    {");

        // Generate HashSet of sensitive field names
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Set of DynamoDB attribute names that are marked as sensitive.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        private static readonly HashSet<string> SensitiveFields = new()");
        sb.AppendLine("        {");

        foreach (var property in sensitiveProperties)
        {
            sb.AppendLine($"            \"{property.AttributeName}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();

        // Generate dynamic fields sensitivity flag
        if (entity.EnableDynamicFields)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Indicates whether dynamic field values should be treated as sensitive.");
            sb.AppendLine("        /// When true, dynamic field values are redacted in logs (only field names are shown).");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static bool DynamicFieldsAreSensitive => {entity.DynamicFieldsSensitiveLogging.ToString().ToLowerInvariant()};");
            sb.AppendLine();
        }

        // Generate IsSensitiveField helper method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Checks if a DynamoDB attribute name is marked as sensitive.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"attributeName\">The DynamoDB attribute name to check.</param>");
        sb.AppendLine("        /// <returns>True if the attribute is sensitive, false otherwise.</returns>");
        sb.AppendLine("        public static bool IsSensitiveField(string attributeName)");
        sb.AppendLine("        {");
        sb.AppendLine("            return SensitiveFields.Contains(attributeName);");
        sb.AppendLine("        }");

        // Generate IsDynamicFieldSensitive helper method if dynamic fields are enabled
        if (entity.EnableDynamicFields)
        {
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Checks if a dynamic field value should be treated as sensitive.");
            sb.AppendLine("        /// Dynamic fields are fields not explicitly mapped to entity properties.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"fieldName\">The dynamic field name to check.</param>");
            sb.AppendLine("        /// <returns>True if the dynamic field value should be redacted in logs, false otherwise.</returns>");
            sb.AppendLine("        public static bool IsDynamicFieldSensitive(string fieldName)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return DynamicFieldsAreSensitive;");
            sb.AppendLine("        }");
        }

        // Close class and namespace
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
