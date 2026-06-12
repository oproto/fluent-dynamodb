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
    /// For multi-entity tables, table creation aggregates indexes from all entities
    /// into a single merged EntityMetadata to ensure all GSIs/LSIs are provisioned.
    /// </summary>
    /// <param name="sb">The StringBuilder to append the generated code to.</param>
    /// <param name="defaultEntity">The default entity model for the table.</param>
    /// <param name="entities">All entities sharing this table (including the default entity).</param>
    public static void GenerateCreateTableAsyncMethodForMultiEntity(StringBuilder sb, EntityModel defaultEntity, List<EntityModel> entities)
    {
        // If there's only one entity or no non-default entities have indexes,
        // we can still use the aggregation approach for consistency (it's a no-op for single entity)
        var nonDefaultEntitiesWithIndexes = entities.Where(e => e.ClassName != defaultEntity.ClassName && e.Indexes.Length > 0).ToList();

        // If no non-default entities have indexes, delegate to single-entity method
        if (nonDefaultEntitiesWithIndexes.Count == 0)
        {
            GenerateCreateTableAsyncMethod(sb, defaultEntity);
            return;
        }

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Creates a DynamoDB table based on the aggregated entity metadata from all entities.");
        sb.AppendLine("    /// This method aggregates indexes from all entities sharing this table to ensure");
        sb.AppendLine("    /// all Global Secondary Indexes and Local Secondary Indexes are provisioned.");
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

        // Generate code to get metadata from default entity as base
        sb.AppendLine($"        var metadata = {defaultEntity.ClassName}.GetEntityMetadata();");
        sb.AppendLine();

        // Generate code to aggregate indexes from all entities
        sb.AppendLine("        // Aggregate indexes from all entities sharing this table");
        sb.AppendLine("        var allIndexes = new System.Collections.Generic.List<Oproto.FluentDynamoDb.Metadata.IndexMetadata>(metadata.Indexes);");

        // For each non-default entity that has indexes, add their indexes (with deduplication)
        foreach (var entity in nonDefaultEntitiesWithIndexes)
        {
            sb.AppendLine();
            sb.AppendLine($"        // Add indexes from {entity.ClassName}");
            sb.AppendLine($"        var {ToCamelCase(entity.ClassName)}Indexes = {entity.ClassName}.GetEntityMetadata().Indexes;");
            sb.AppendLine($"        for (var i = 0; i < {ToCamelCase(entity.ClassName)}Indexes.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var idx = {ToCamelCase(entity.ClassName)}Indexes[i];");
            sb.AppendLine("            var alreadyExists = false;");
            sb.AppendLine("            for (var j = 0; j < allIndexes.Count; j++)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (allIndexes[j].IndexName == idx.IndexName)");
            sb.AppendLine("                {");
            sb.AppendLine("                    alreadyExists = true;");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            if (!alreadyExists)");
            sb.AppendLine("            {");
            sb.AppendLine("                allIndexes.Add(idx);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        sb.AppendLine();

        // Create merged metadata with all indexes
        sb.AppendLine("        // Create merged metadata with aggregated indexes from all entities");
        sb.AppendLine("        var mergedMetadata = new Oproto.FluentDynamoDb.Metadata.EntityMetadata");
        sb.AppendLine("        {");
        sb.AppendLine("            TableName = metadata.TableName,");
        sb.AppendLine("            PartitionKeyAttributeName = metadata.PartitionKeyAttributeName,");
        sb.AppendLine("            PartitionKeyAttributeType = metadata.PartitionKeyAttributeType,");
        sb.AppendLine("            SortKeyAttributeName = metadata.SortKeyAttributeName,");
        sb.AppendLine("            SortKeyAttributeType = metadata.SortKeyAttributeType,");
        sb.AppendLine("            TtlAttributeName = metadata.TtlAttributeName,");
        sb.AppendLine("            Indexes = allIndexes.ToArray(),");
        sb.AppendLine("            Properties = metadata.Properties,");
        sb.AppendLine("            Relationships = metadata.Relationships,");
        sb.AppendLine("            EntityDiscriminator = metadata.EntityDiscriminator,");
        sb.AppendLine("            IsMultiItemEntity = metadata.IsMultiItemEntity,");
        sb.AppendLine("            RequiresWriteTransaction = metadata.RequiresWriteTransaction");
        sb.AppendLine("        };");

        sb.AppendLine();
        sb.AppendLine($"        var creator = new Oproto.FluentDynamoDb.Provisioning.TableCreator();");
        sb.AppendLine($"        return await creator.CreateAsync(");
        sb.AppendLine($"            client,");
        sb.AppendLine($"            tableName,");
        sb.AppendLine($"            mergedMetadata,");
        sb.AppendLine($"            options ?? new Oproto.FluentDynamoDb.Provisioning.TableCreationOptions(),");
        sb.AppendLine($"            cancellationToken).ConfigureAwait(false);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Converts a PascalCase string to camelCase.
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
