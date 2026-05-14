using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text;

namespace Oproto.FluentDynamoDb.SourceGenerator.Generators;

/// <summary>
/// Generates table creation methods for table classes.
/// The generated CreateTableAsync method allows developers to create DynamoDB tables
/// from entity metadata, primarily for integration testing scenarios.
/// </summary>
internal static class TableCreationGenerator
{
    /// <summary>
    /// Generates the CreateTableAsync static method for a table class.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    /// <param name="entity">The primary entity model for the table.</param>
    public static void GenerateCreateTableAsyncMethod(StringBuilder sb, EntityModel entity)
    {
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a DynamoDB table based on the entity metadata.");
        sb.AppendLine("    /// This method is primarily designed for integration testing scenarios where developers");
        sb.AppendLine("    /// need to create tables matching their entity definitions without manual infrastructure setup.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"client\">The DynamoDB client to use for CreateTable.</param>");
        sb.AppendLine("    /// <param name=\"tableName\">The name of the table to create.</param>");
        sb.AppendLine("    /// <param name=\"options\">Optional creation options for billing mode, throughput, TTL, and wait behavior.</param>");
        sb.AppendLine("    /// <param name=\"cancellationToken\">Cancellation token for the async operation.</param>");
        sb.AppendLine("    /// <returns>The creation result containing table name, ARN, status, and TTL enablement.</returns>");
        sb.AppendLine("    /// <example>");
        sb.AppendLine("    /// <code>");
        sb.AppendLine("    /// // Basic table creation for integration tests");
        sb.AppendLine("    /// var result = await MyTable.CreateTableAsync(dynamoDbClient, \"test-table\");");
        sb.AppendLine("    /// ");
        sb.AppendLine("    /// // With custom options");
        sb.AppendLine("    /// var result = await MyTable.CreateTableAsync(dynamoDbClient, \"test-table\", new TableCreationOptions");
        sb.AppendLine("    /// {");
        sb.AppendLine("    ///     EnableTtl = true,");
        sb.AppendLine("    ///     WaitForActive = true,");
        sb.AppendLine("    ///     WaitTimeout = TimeSpan.FromSeconds(30)");
        sb.AppendLine("    /// });");
        sb.AppendLine("    /// </code>");
        sb.AppendLine("    /// </example>");
        sb.AppendLine($"    public static async System.Threading.Tasks.Task<Oproto.FluentDynamoDb.Provisioning.TableCreationResult> CreateTableAsync(");
        sb.AppendLine($"        IAmazonDynamoDB client,");
        sb.AppendLine($"        string tableName,");
        sb.AppendLine($"        Oproto.FluentDynamoDb.Provisioning.TableCreationOptions? options = null,");
        sb.AppendLine($"        System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var creator = new Oproto.FluentDynamoDb.Provisioning.TableCreator();");
        sb.AppendLine($"        return await creator.CreateAsync(");
        sb.AppendLine($"            client,");
        sb.AppendLine($"            tableName,");
        sb.AppendLine($"            {entity.ClassName}.GetEntityMetadata(),");
        sb.AppendLine($"            options ?? new Oproto.FluentDynamoDb.Provisioning.TableCreationOptions(),");
        sb.AppendLine($"            cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates the CreateTableAsync static method for a multi-entity table class.
    /// For multi-entity tables, table creation uses the default entity's metadata.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    /// <param name="defaultEntity">The default entity model for the table.</param>
    public static void GenerateCreateTableAsyncMethodForMultiEntity(StringBuilder sb, EntityModel defaultEntity)
    {
        // For multi-entity tables, we use the same generation logic but with the default entity
        GenerateCreateTableAsyncMethod(sb, defaultEntity);
    }
}
