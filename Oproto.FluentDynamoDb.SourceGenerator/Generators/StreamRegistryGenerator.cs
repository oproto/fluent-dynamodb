using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates StreamDiscriminatorRegistry and OnStream method for table classes.
/// </summary>
internal static class StreamRegistryGenerator
{
    /// <summary>
    /// Generates the OnStream method and StreamDiscriminatorRegistry for a table class.
    /// </summary>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="entities">List of entities that share this table and have GenerateStreamConversion enabled.</param>
    /// <param name="tableClassName">The table class name.</param>
    /// <param name="primaryNamespace">The namespace for the generated code.</param>
    /// <returns>The generated OnStream method and registry code, or empty string if no entities have stream conversion.</returns>
    public static string GenerateOnStreamMethod(
        string tableName,
        List<EntityModel> entities,
        string tableClassName,
        string primaryNamespace)
    {
        // Filter to only entities with GenerateStreamConversion enabled
        var streamEntities = entities.Where(e => e.GenerateStreamConversion).ToList();
        
        if (streamEntities.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        
        // File header with auto-generated comment, nullable directive, timestamp, and version
        FileHeaderGenerator.GenerateFileHeader(sb);
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using Amazon.Lambda.DynamoDBEvents;");
        sb.AppendLine("using Oproto.FluentDynamoDb.Streams.Processing;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {primaryNamespace};");
        sb.AppendLine();
        
        // Partial class declaration
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Stream processing extensions for {tableClassName}.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public partial class {tableClassName}");
        sb.AppendLine("{");
        
        // Generate the StreamDiscriminatorRegistry nested class
        GenerateStreamDiscriminatorRegistry(sb, streamEntities);
        
        // Generate the OnStream method
        GenerateOnStreamMethodImplementation(sb, streamEntities);
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates the StreamDiscriminatorRegistry nested class.
    /// </summary>
    private static void GenerateStreamDiscriminatorRegistry(StringBuilder sb, List<EntityModel> streamEntities)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Static registry for discriminator metadata used in stream processing.");
        sb.AppendLine("    /// Provides AOT-compatible, reflection-free lookup of discriminator information.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    private static class StreamDiscriminatorRegistry");
        sb.AppendLine("    {");
        
        // Generate the static dictionary
        sb.AppendLine("        private static readonly System.Collections.Generic.Dictionary<System.Type, Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorInfo> _registry = new()");
        sb.AppendLine("        {");
        
        foreach (var entity in streamEntities)
        {
            var discriminator = entity.Discriminator;
            if (discriminator == null)
            {
                continue;
            }

            // Determine the strategy and pattern based on discriminator configuration
            var (strategy, pattern, value) = ParseDiscriminatorPattern(discriminator);
            
            sb.AppendLine($"            [typeof({entity.ClassName})] = new Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorInfo");
            sb.AppendLine("            {");
            sb.AppendLine($"                Property = \"{discriminator.PropertyName}\",");
            
            if (pattern != null)
            {
                sb.AppendLine($"                Pattern = \"{pattern}\",");
            }
            else
            {
                sb.AppendLine("                Pattern = null,");
            }
            
            sb.AppendLine($"                Strategy = Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorStrategy.{strategy},");
            sb.AppendLine($"                Value = \"{value}\"");
            sb.AppendLine("            },");
        }
        
        sb.AppendLine("        };");
        sb.AppendLine();
        
        // Generate the GetInfo method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets discriminator information for an entity type.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"entityType\">The entity type to look up.</param>");
        sb.AppendLine("        /// <returns>The discriminator information, or null if not found.</returns>");
        sb.AppendLine("        public static Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorInfo? GetInfo(System.Type entityType)");
        sb.AppendLine("        {");
        sb.AppendLine("            return _registry.TryGetValue(entityType, out var info) ? info : null;");
        sb.AppendLine("        }");
        
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Generates the OnStream method implementation.
    /// </summary>
    private static void GenerateOnStreamMethodImplementation(StringBuilder sb, List<EntityModel> streamEntities)
    {
        // Get the discriminator property from the first entity (they should all be the same)
        var firstDiscriminator = streamEntities.FirstOrDefault()?.Discriminator;
        if (firstDiscriminator == null)
        {
            return;
        }

        var discriminatorProperty = firstDiscriminator.PropertyName;
        
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a discriminator-based stream processor for this table.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"record\">The DynamoDB stream record to process.</param>");
        sb.AppendLine("    /// <returns>");
        sb.AppendLine("    /// A DiscriminatorStreamProcessorBuilder configured with the table's discriminator property");
        sb.AppendLine("    /// and registry for automatic discriminator lookup.");
        sb.AppendLine("    /// </returns>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine("    /// This method provides a convenient entry point for stream processing with automatic");
        sb.AppendLine("    /// discriminator configuration. Use For&lt;TEntity&gt;() without parameters to automatically");
        sb.AppendLine("    /// look up discriminator values from entity configuration.");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine("    /// The returned builder is pre-configured with:");
        sb.AppendLine($"    /// - Discriminator property: \"{discriminatorProperty}\"");
        sb.AppendLine("    /// - Registry lookup for automatic discriminator resolution");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// await table.OnStream(record)");
        sb.AppendLine("    ///     .For&lt;UserEntity&gt;()  // Discriminator looked up automatically");
        sb.AppendLine("    ///         .Where(u => u.Status == \"active\")");
        sb.AppendLine("    ///         .OnInsert(async (_, user) => await ProcessUser(user))");
        sb.AppendLine("    ///     .For&lt;OrderEntity&gt;()  // Discriminator looked up automatically");
        sb.AppendLine("    ///         .OnInsert(async (_, order) => await ProcessOrder(order))");
        sb.AppendLine("    ///     .ProcessAsync();");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine("    public Oproto.FluentDynamoDb.Streams.Processing.DiscriminatorStreamProcessorBuilder OnStream(");
        sb.AppendLine("        Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.DynamodbStreamRecord record)");
        sb.AppendLine("    {");
        sb.AppendLine("        return record.Process()");
        sb.AppendLine($"            .WithDiscriminator(\"{discriminatorProperty}\")");
        sb.AppendLine("            .WithRegistry(StreamDiscriminatorRegistry.GetInfo);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Parses discriminator configuration to determine strategy, pattern, and value.
    /// </summary>
    /// <param name="discriminator">The discriminator configuration.</param>
    /// <returns>A tuple of (strategy, pattern, value).</returns>
    private static (string strategy, string? pattern, string value) ParseDiscriminatorPattern(DiscriminatorConfig discriminator)
    {
        // Use the strategy from the discriminator config
        switch (discriminator.Strategy)
        {
            case DiscriminatorStrategy.StartsWith:
                // Prefix match: USER#*
                return ("StartsWith", discriminator.Pattern, discriminator.Pattern?.TrimEnd('*') ?? string.Empty);
                
            case DiscriminatorStrategy.EndsWith:
                // Suffix match: *#USER
                return ("EndsWith", discriminator.Pattern, discriminator.Pattern?.TrimStart('*') ?? string.Empty);
                
            case DiscriminatorStrategy.Contains:
                // Contains match: *#USER#*
                return ("Contains", discriminator.Pattern, discriminator.Pattern?.Trim('*') ?? string.Empty);
                
            case DiscriminatorStrategy.ExactMatch:
                // Exact match
                return ("ExactMatch", null, discriminator.ExactValue ?? string.Empty);
                
            default:
                // Fallback to exact match
                return ("ExactMatch", null, discriminator.ExactValue ?? string.Empty);
        }
    }

    /// <summary>
    /// Validates that all entities use the same discriminator property.
    /// </summary>
    /// <param name="entities">The entities to validate.</param>
    /// <returns>True if all entities use the same discriminator property, false otherwise.</returns>
    public static bool ValidateConsistentDiscriminatorProperty(List<EntityModel> entities)
    {
        var streamEntities = entities.Where(e => e.GenerateStreamConversion && e.Discriminator != null).ToList();
        
        if (streamEntities.Count <= 1)
        {
            return true;
        }

        var firstProperty = streamEntities[0].Discriminator?.PropertyName;
        return streamEntities.All(e => e.Discriminator?.PropertyName == firstProperty);
    }

    /// <summary>
    /// Gets the discriminator properties that differ across entities.
    /// </summary>
    /// <param name="entities">The entities to check.</param>
    /// <returns>A list of distinct discriminator properties found.</returns>
    public static List<string> GetDistinctDiscriminatorProperties(List<EntityModel> entities)
    {
        return entities
            .Where(e => e.GenerateStreamConversion && e.Discriminator != null)
            .Select(e => e.Discriminator!.PropertyName)
            .Distinct()
            .ToList();
    }
}

