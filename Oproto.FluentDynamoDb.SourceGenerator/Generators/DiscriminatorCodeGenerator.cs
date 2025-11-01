using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates discriminator matching code for entity and projection hydration.
/// </summary>
internal static class DiscriminatorCodeGenerator
{
    /// <summary>
    /// Generates discriminator validation code for FromDynamoDb methods.
    /// </summary>
    /// <param name="discriminator">The discriminator configuration.</param>
    /// <param name="projectionTypeName">The name of the projection type for error messages.</param>
    /// <returns>Generated C# code for discriminator validation.</returns>
    public static string GenerateDiscriminatorValidation(DiscriminatorConfig discriminator, string projectionTypeName)
    {
        if (discriminator == null || !discriminator.IsValid)
            return string.Empty;

        var sb = new StringBuilder();
        
        sb.AppendLine("                // Validate discriminator value");
        sb.AppendLine($"                if (item.TryGetValue(\"{discriminator.PropertyName}\", out var discriminatorAttr))");
        sb.AppendLine("                {");
        sb.AppendLine("                    var actualDiscriminator = discriminatorAttr.S;");
        
        // Generate the matching logic based on strategy
        string matchCondition = GenerateMatchCondition(discriminator);
        
        sb.AppendLine($"                    if (!({matchCondition}))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        throw DiscriminatorMismatchException.Create(");
        sb.AppendLine($"                            typeof({projectionTypeName}),");
        sb.AppendLine($"                            \"{GetExpectedValueForError(discriminator)}\",");
        sb.AppendLine("                            actualDiscriminator);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    throw DiscriminatorMismatchException.Create(");
        sb.AppendLine($"                        typeof({projectionTypeName}),");
        sb.AppendLine($"                        \"{GetExpectedValueForError(discriminator)}\",");
        sb.AppendLine("                        null);");
        sb.AppendLine("                }");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates the condition expression for matching discriminator values.
    /// </summary>
    private static string GenerateMatchCondition(DiscriminatorConfig discriminator)
    {
        return discriminator.Strategy switch
        {
            DiscriminatorStrategy.ExactMatch => 
                $"actualDiscriminator == \"{EscapeString(discriminator.ExactValue ?? discriminator.Pattern)}\"",
            
            DiscriminatorStrategy.StartsWith =>
                $"actualDiscriminator != null && actualDiscriminator.StartsWith(\"{EscapeString(GetPatternText(discriminator))}\")",
            
            DiscriminatorStrategy.EndsWith =>
                $"actualDiscriminator != null && actualDiscriminator.EndsWith(\"{EscapeString(GetPatternText(discriminator))}\")",
            
            DiscriminatorStrategy.Contains =>
                $"actualDiscriminator != null && actualDiscriminator.Contains(\"{EscapeString(GetPatternText(discriminator))}\")",
            
            DiscriminatorStrategy.Complex =>
                GenerateComplexPatternMatch(discriminator.Pattern!),
            
            _ => "true"
        };
    }

    /// <summary>
    /// Generates matching code for complex patterns with multiple wildcards.
    /// </summary>
    private static string GenerateComplexPatternMatch(string pattern)
    {
        // For complex patterns, we'll generate a simple regex-like match
        // This is a simplified implementation - could be enhanced
        var parts = pattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return "true"; // Pattern is just "*"
        
        if (parts.Length == 1)
            return $"actualDiscriminator != null && actualDiscriminator.Contains(\"{EscapeString(parts[0])}\")";
        
        // Multiple parts - need to check they appear in order
        var conditions = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i == 0 && !pattern.StartsWith("*"))
            {
                conditions.Add($"actualDiscriminator.StartsWith(\"{EscapeString(parts[i])}\")");
            }
            else if (i == parts.Length - 1 && !pattern.EndsWith("*"))
            {
                conditions.Add($"actualDiscriminator.EndsWith(\"{EscapeString(parts[i])}\")");
            }
            else
            {
                conditions.Add($"actualDiscriminator.Contains(\"{EscapeString(parts[i])}\")");
            }
        }
        
        return $"actualDiscriminator != null && {string.Join(" && ", conditions)}";
    }

    /// <summary>
    /// Gets the pattern text without wildcards.
    /// </summary>
    private static string GetPatternText(DiscriminatorConfig discriminator)
    {
        if (string.IsNullOrEmpty(discriminator.Pattern))
            return string.Empty;

        return discriminator.Strategy switch
        {
            DiscriminatorStrategy.StartsWith => discriminator.Pattern.TrimEnd('*'),
            DiscriminatorStrategy.EndsWith => discriminator.Pattern.TrimStart('*'),
            DiscriminatorStrategy.Contains => discriminator.Pattern.Trim('*'),
            _ => discriminator.Pattern
        };
    }

    /// <summary>
    /// Gets the expected value string for error messages.
    /// </summary>
    private static string GetExpectedValueForError(DiscriminatorConfig discriminator)
    {
        if (discriminator.Strategy == DiscriminatorStrategy.ExactMatch && !string.IsNullOrEmpty(discriminator.ExactValue))
            return EscapeString(discriminator.ExactValue);
        
        if (!string.IsNullOrEmpty(discriminator.Pattern))
            return EscapeString(discriminator.Pattern);
        
        return "unknown";
    }

    /// <summary>
    /// Escapes a string for use in generated C# code.
    /// </summary>
    private static string EscapeString(string? value)
    {
        if (value == null)
            return string.Empty;
        
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Gets the discriminator property name to include in projection expressions.
    /// </summary>
    public static string? GetDiscriminatorPropertyName(DiscriminatorConfig? discriminator)
    {
        return discriminator?.IsValid == true ? discriminator.PropertyName : null;
    }
}
