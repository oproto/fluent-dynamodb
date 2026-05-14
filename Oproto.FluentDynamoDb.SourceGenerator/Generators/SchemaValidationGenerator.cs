using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates schema validation methods for table classes.
/// The generated ValidateSchemaAsync method allows developers to verify that
/// the actual DynamoDB table schema matches the entity metadata at runtime.
/// </summary>
internal static class SchemaValidationGenerator
{
    /// <summary>
    /// Generates the ValidateSchemaAsync static method for a table class.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="entity">The primary entity model for the table.</param>
    public static void GenerateValidateSchemaAsyncMethod(StringBuilder sb, string tableName, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Validates that the DynamoDB table schema matches the entity metadata.");
        sb.AppendLine("    /// This method is designed to be called during application startup (e.g., Lambda cold start)");
        sb.AppendLine("    /// to provide fail-fast validation without impacting per-request performance.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"client\">The DynamoDB client to use for DescribeTable.</param>");
        sb.AppendLine("    /// <param name=\"options\">Optional validation options to control strictness.</param>");
        sb.AppendLine("    /// <returns>The validation result containing any errors and warnings.</returns>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Basic validation");
        sb.AppendLine("    /// var result = await MyTable.ValidateSchemaAsync(dynamoDbClient);");
        sb.AppendLine("    /// if (!result.IsValid)");
        sb.AppendLine("    /// {");
        sb.AppendLine("    ///     // Handle validation errors");
        sb.AppendLine("    ///     foreach (var error in result.Errors)");
        sb.AppendLine("    ///     {");
        sb.AppendLine("    ///         Console.WriteLine($\"Error: {error.Message}\");");
        sb.AppendLine("    ///     }");
        sb.AppendLine("    /// }");
        sb.AppendLine("    /// ");
        sb.AppendLine("    /// // Fail-fast validation");
        sb.AppendLine("    /// var result = await MyTable.ValidateSchemaAsync(dynamoDbClient);");
        sb.AppendLine("    /// result.ThrowOnError(); // Throws SchemaValidationException if errors exist");
        sb.AppendLine("    /// ");
        sb.AppendLine("    /// // Strict validation (missing projection models are errors)");
        sb.AppendLine("    /// var result = await MyTable.ValidateSchemaAsync(dynamoDbClient, new SchemaValidationOptions");
        sb.AppendLine("    /// {");
        sb.AppendLine("    ///     Strictness = ValidationStrictness.Strict");
        sb.AppendLine("    /// });");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine($"    public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Validation.SchemaValidationResult> ValidateSchemaAsync(");
        sb.AppendLine($"        IAmazonDynamoDB client,");
        sb.AppendLine($"        Oproto.FluentDynamoDb.Validation.SchemaValidationOptions? options = null)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var validator = new Oproto.FluentDynamoDb.Validation.SchemaValidator();");
        sb.AppendLine($"        return await validator.ValidateAsync(");
        sb.AppendLine($"            client,");
        sb.AppendLine($"            \"{tableName}\",");
        sb.AppendLine($"            {entity.ClassName}.GetEntityMetadata(),");
        sb.AppendLine($"            options ?? new Oproto.FluentDynamoDb.Validation.SchemaValidationOptions()).ConfigureAwait(false);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates the ValidateSchemaAsync static method for a multi-entity table class.
    /// For multi-entity tables, validation is performed against the default entity's metadata.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    /// <param name="tableName">The DynamoDB table name.</param>
    /// <param name="defaultEntity">The default entity model for the table.</param>
    public static void GenerateValidateSchemaAsyncMethodForMultiEntity(StringBuilder sb, string tableName, EntityModel defaultEntity)
    {
        // For multi-entity tables, we use the same generation logic but with the default entity
        GenerateValidateSchemaAsyncMethod(sb, tableName, defaultEntity);
    }
}
