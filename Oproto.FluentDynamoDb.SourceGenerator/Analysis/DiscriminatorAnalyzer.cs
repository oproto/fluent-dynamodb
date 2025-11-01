using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes discriminator configuration from attributes and determines matching strategy.
/// </summary>
internal static class DiscriminatorAnalyzer
{
    /// <summary>
    /// Extracts discriminator configuration from a DynamoDbTable attribute.
    /// </summary>
    /// <param name="tableAttribute">The table attribute syntax.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="entityName">The entity class name for diagnostic reporting.</param>
    /// <param name="diagnostics">List to collect diagnostics.</param>
    /// <returns>The discriminator configuration, or null if no discriminator is configured.</returns>
    public static DiscriminatorConfig? AnalyzeTableDiscriminator(
        AttributeSyntax tableAttribute,
        SemanticModel semanticModel,
        string entityName,
        List<Diagnostic> diagnostics)
    {
        string? discriminatorProperty = null;
        string? discriminatorValue = null;
        string? discriminatorPattern = null;
        string? legacyEntityDiscriminator = null;

        if (tableAttribute.ArgumentList != null)
        {
            foreach (var arg in tableAttribute.ArgumentList.Arguments)
            {
                var argName = arg.NameEquals?.Name.Identifier.ValueText;
                
                if (argName == "DiscriminatorProperty" && arg.Expression is LiteralExpressionSyntax propLiteral)
                {
                    discriminatorProperty = propLiteral.Token.ValueText;
                }
                else if (argName == "DiscriminatorValue" && arg.Expression is LiteralExpressionSyntax valueLiteral)
                {
                    discriminatorValue = valueLiteral.Token.ValueText;
                }
                else if (argName == "DiscriminatorPattern" && arg.Expression is LiteralExpressionSyntax patternLiteral)
                {
                    discriminatorPattern = patternLiteral.Token.ValueText;
                }
                else if (argName == "EntityDiscriminator" && arg.Expression is LiteralExpressionSyntax legacyLiteral)
                {
                    legacyEntityDiscriminator = legacyLiteral.Token.ValueText;
                }
            }
        }

        // Handle legacy EntityDiscriminator property
        if (!string.IsNullOrEmpty(legacyEntityDiscriminator) && string.IsNullOrEmpty(discriminatorProperty))
        {
            discriminatorProperty = "entity_type";
            discriminatorValue = legacyEntityDiscriminator;
        }

        // Validate discriminator configuration
        ValidateDiscriminatorConfiguration(
            discriminatorProperty,
            discriminatorValue,
            discriminatorPattern,
            entityName,
            tableAttribute.GetLocation(),
            diagnostics);

        // No discriminator configured
        if (string.IsNullOrEmpty(discriminatorProperty))
            return null;

        // If both value and pattern are specified, prefer value (warning already reported)
        if (!string.IsNullOrEmpty(discriminatorValue) && !string.IsNullOrEmpty(discriminatorPattern))
        {
            discriminatorPattern = null;
        }

        return CreateDiscriminatorConfig(discriminatorProperty, discriminatorValue, discriminatorPattern);
    }

    /// <summary>
    /// Extracts GSI-specific discriminator configuration from a GlobalSecondaryIndex attribute.
    /// </summary>
    /// <param name="gsiAttribute">The GSI attribute syntax.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="gsiName">The GSI name for diagnostic reporting.</param>
    /// <param name="diagnostics">List to collect diagnostics.</param>
    /// <returns>The discriminator configuration, or null if no GSI discriminator is configured.</returns>
    public static DiscriminatorConfig? AnalyzeGsiDiscriminator(
        AttributeSyntax gsiAttribute,
        SemanticModel semanticModel,
        string gsiName,
        List<Diagnostic> diagnostics)
    {
        string? discriminatorProperty = null;
        string? discriminatorValue = null;
        string? discriminatorPattern = null;

        if (gsiAttribute.ArgumentList != null)
        {
            foreach (var arg in gsiAttribute.ArgumentList.Arguments)
            {
                var argName = arg.NameEquals?.Name.Identifier.ValueText;
                
                if (argName == "DiscriminatorProperty" && arg.Expression is LiteralExpressionSyntax propLiteral)
                {
                    discriminatorProperty = propLiteral.Token.ValueText;
                }
                else if (argName == "DiscriminatorValue" && arg.Expression is LiteralExpressionSyntax valueLiteral)
                {
                    discriminatorValue = valueLiteral.Token.ValueText;
                }
                else if (argName == "DiscriminatorPattern" && arg.Expression is LiteralExpressionSyntax patternLiteral)
                {
                    discriminatorPattern = patternLiteral.Token.ValueText;
                }
            }
        }

        // Validate discriminator configuration
        ValidateDiscriminatorConfiguration(
            discriminatorProperty,
            discriminatorValue,
            discriminatorPattern,
            $"GSI '{gsiName}'",
            gsiAttribute.GetLocation(),
            diagnostics);

        // No GSI discriminator configured
        if (string.IsNullOrEmpty(discriminatorProperty))
            return null;

        // If both value and pattern are specified, prefer value (warning already reported)
        if (!string.IsNullOrEmpty(discriminatorValue) && !string.IsNullOrEmpty(discriminatorPattern))
        {
            discriminatorPattern = null;
        }

        return CreateDiscriminatorConfig(discriminatorProperty, discriminatorValue, discriminatorPattern);
    }

    /// <summary>
    /// Creates a discriminator configuration and determines the matching strategy.
    /// </summary>
    private static DiscriminatorConfig CreateDiscriminatorConfig(
        string propertyName,
        string? exactValue,
        string? pattern)
    {
        var config = new DiscriminatorConfig
        {
            PropertyName = propertyName
        };

        // Determine strategy based on what's provided
        if (!string.IsNullOrEmpty(exactValue))
        {
            config.ExactValue = exactValue;
            config.Strategy = DiscriminatorStrategy.ExactMatch;
        }
        else if (!string.IsNullOrEmpty(pattern))
        {
            config.Pattern = pattern;
            config.Strategy = DeterminePatternStrategy(pattern);
        }
        else
        {
            config.Strategy = DiscriminatorStrategy.None;
        }

        return config;
    }

    /// <summary>
    /// Determines the matching strategy based on the pattern.
    /// </summary>
    private static DiscriminatorStrategy DeterminePatternStrategy(string pattern)
    {
        var wildcardCount = pattern.Count(c => c == '*');

        if (wildcardCount == 0)
        {
            // No wildcards - treat as exact match
            return DiscriminatorStrategy.ExactMatch;
        }
        else if (wildcardCount == 2 && pattern.StartsWith("*") && pattern.EndsWith("*") && pattern.Length > 2)
        {
            // *text* - contains (exactly 2 wildcards at start and end)
            return DiscriminatorStrategy.Contains;
        }
        else if (wildcardCount == 1)
        {
            if (pattern.StartsWith("*"))
            {
                // *text - ends with
                return DiscriminatorStrategy.EndsWith;
            }
            else if (pattern.EndsWith("*"))
            {
                // text* - starts with
                return DiscriminatorStrategy.StartsWith;
            }
            else
            {
                // *text*something - complex
                return DiscriminatorStrategy.Complex;
            }
        }
        else
        {
            // Multiple wildcards - complex pattern
            return DiscriminatorStrategy.Complex;
        }
    }

    /// <summary>
    /// Gets the pattern text without wildcards for simple patterns.
    /// </summary>
    public static string GetPatternText(string pattern, DiscriminatorStrategy strategy)
    {
        return strategy switch
        {
            DiscriminatorStrategy.StartsWith => pattern.TrimEnd('*'),
            DiscriminatorStrategy.EndsWith => pattern.TrimStart('*'),
            DiscriminatorStrategy.Contains => pattern.Trim('*'),
            _ => pattern
        };
    }

    /// <summary>
    /// Validates discriminator configuration and reports diagnostics.
    /// </summary>
    private static void ValidateDiscriminatorConfiguration(
        string? discriminatorProperty,
        string? discriminatorValue,
        string? discriminatorPattern,
        string entityName,
        Location location,
        List<Diagnostic> diagnostics)
    {
        // DISC001: Warn if both DiscriminatorValue and DiscriminatorPattern are specified
        if (!string.IsNullOrEmpty(discriminatorValue) && !string.IsNullOrEmpty(discriminatorPattern))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.BothDiscriminatorValueAndPattern,
                location,
                entityName);
            diagnostics.Add(diagnostic);
        }

        // DISC002: Error if DiscriminatorValue or DiscriminatorPattern is specified without DiscriminatorProperty
        if (string.IsNullOrEmpty(discriminatorProperty) &&
            (!string.IsNullOrEmpty(discriminatorValue) || !string.IsNullOrEmpty(discriminatorPattern)))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.DiscriminatorValueWithoutProperty,
                location,
                entityName);
            diagnostics.Add(diagnostic);
        }

        // DISC003: Validate pattern syntax if pattern is specified
        if (!string.IsNullOrEmpty(discriminatorPattern))
        {
            var validationError = ValidatePatternSyntax(discriminatorPattern);
            if (validationError != null)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InvalidDiscriminatorPattern,
                    location,
                    entityName,
                    discriminatorPattern,
                    validationError);
                diagnostics.Add(diagnostic);
            }
        }
    }

    /// <summary>
    /// Validates pattern syntax and returns an error message if invalid.
    /// </summary>
    private static string? ValidatePatternSyntax(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "Pattern cannot be empty or whitespace";
        }

        // Check for invalid characters
        if (pattern.Contains('\0') || pattern.Contains('\n') || pattern.Contains('\r'))
        {
            return "Pattern contains invalid control characters";
        }

        // Count wildcards
        var wildcardCount = pattern.Count(c => c == '*');

        if (wildcardCount == 0)
        {
            // No wildcards - this is fine, treated as exact match
            return null;
        }

        if (wildcardCount == 1)
        {
            // Single wildcard - must be at start, end, or both
            var wildcardIndex = pattern.IndexOf('*');
            if (wildcardIndex == 0 || wildcardIndex == pattern.Length - 1)
            {
                // Valid: *text or text*
                return null;
            }
            else
            {
                return "Single wildcard must be at the start or end of the pattern";
            }
        }

        if (wildcardCount == 2)
        {
            // Two wildcards - must be at both ends
            if (pattern.StartsWith("*") && pattern.EndsWith("*"))
            {
                // Valid: *text*
                return null;
            }
            else
            {
                return "Two wildcards must be at both the start and end of the pattern (e.g., '*text*')";
            }
        }

        // More than 2 wildcards - complex pattern
        return "Complex patterns with more than 2 wildcards are not supported. Use simple patterns like 'USER#*', '*#USER', or '*USER*'";
    }
}
