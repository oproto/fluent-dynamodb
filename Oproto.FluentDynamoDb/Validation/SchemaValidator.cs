using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Validates that a DynamoDB table schema matches the expected entity metadata.
/// </summary>
public class SchemaValidator
{
    /// <summary>
    /// Validates that the DynamoDB table schema matches the entity metadata.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for DescribeTable.</param>
    /// <param name="tableName">The name of the DynamoDB table to validate.</param>
    /// <param name="metadata">The entity metadata defining the expected schema.</param>
    /// <param name="options">Optional validation options.</param>
    /// <returns>The validation result containing any errors and warnings.</returns>
    public async Task<SchemaValidationResult> ValidateAsync(
        IAmazonDynamoDB client,
        string tableName,
        EntityMetadata metadata,
        SchemaValidationOptions? options = null)
    {
        options ??= new SchemaValidationOptions();
        var result = new SchemaValidationResult();

        // Get the actual table description from DynamoDB
        var describeTableResponse = await client.DescribeTableAsync(new DescribeTableRequest
        {
            TableName = tableName
        });

        var tableDescription = describeTableResponse.Table;

        // Validate primary key
        ValidatePrimaryKey(tableDescription, metadata, result);

        // Validate Global Secondary Indexes
        ValidateGlobalSecondaryIndexes(tableDescription, metadata, result, options);

        // Validate Local Secondary Indexes
        ValidateLocalSecondaryIndexes(tableDescription, metadata, result, options);

        // Validate TTL
        await ValidateTtlAsync(client, tableName, metadata, result);

        return result;
    }


    /// <summary>
    /// Validates the primary key configuration.
    /// </summary>
    private void ValidatePrimaryKey(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result)
    {
        var keySchema = tableDescription.KeySchema;
        var attributeDefinitions = tableDescription.AttributeDefinitions;

        // Find partition key in table
        var tablePartitionKey = keySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
        var tableSortKey = keySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);

        // Validate partition key name
        if (tablePartitionKey == null)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.PartitionKeyNameMismatch,
                element: tableDescription.TableName,
                expected: metadata.PartitionKeyAttributeName,
                actual: "not found",
                message: "Table does not have a partition key defined"));
        }
        else if (tablePartitionKey.AttributeName != metadata.PartitionKeyAttributeName)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.PartitionKeyNameMismatch,
                element: tableDescription.TableName,
                expected: metadata.PartitionKeyAttributeName,
                actual: tablePartitionKey.AttributeName,
                message: $"Partition key name mismatch"));
        }
        else
        {
            // Validate partition key type
            var pkAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == tablePartitionKey.AttributeName);
            if (pkAttributeDef != null && pkAttributeDef.AttributeType.Value != metadata.PartitionKeyAttributeType)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.PartitionKeyTypeMismatch,
                    element: tableDescription.TableName,
                    expected: metadata.PartitionKeyAttributeType,
                    actual: pkAttributeDef.AttributeType.Value,
                    message: $"Partition key type mismatch"));
            }
        }

        // Validate sort key
        ValidateSortKey(tableDescription, tableSortKey, attributeDefinitions, metadata, result);
    }

    /// <summary>
    /// Validates the sort key configuration.
    /// </summary>
    private void ValidateSortKey(
        TableDescription tableDescription,
        KeySchemaElement? tableSortKey,
        List<AttributeDefinition> attributeDefinitions,
        EntityMetadata metadata,
        SchemaValidationResult result)
    {
        var expectedHasSortKey = !string.IsNullOrEmpty(metadata.SortKeyAttributeName);
        var actualHasSortKey = tableSortKey != null;

        if (expectedHasSortKey && !actualHasSortKey)
        {
            // Entity expects sort key but table doesn't have one
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.SortKeyMissing,
                element: tableDescription.TableName,
                expected: metadata.SortKeyAttributeName!,
                actual: "not found",
                message: "Entity metadata defines a sort key but the table does not have one"));
        }
        else if (!expectedHasSortKey && actualHasSortKey)
        {
            // Table has sort key but entity doesn't expect one
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.SortKeyUnexpected,
                element: tableDescription.TableName,
                expected: "none",
                actual: tableSortKey!.AttributeName,
                message: "Table has a sort key but the entity metadata does not define one"));
        }
        else if (expectedHasSortKey && actualHasSortKey)
        {
            // Both have sort key - validate name and type
            if (tableSortKey!.AttributeName != metadata.SortKeyAttributeName)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.SortKeyNameMismatch,
                    element: tableDescription.TableName,
                    expected: metadata.SortKeyAttributeName!,
                    actual: tableSortKey.AttributeName,
                    message: "Sort key name mismatch"));
            }
            else
            {
                // Validate sort key type
                var skAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == tableSortKey.AttributeName);
                if (skAttributeDef != null && metadata.SortKeyAttributeType != null &&
                    skAttributeDef.AttributeType.Value != metadata.SortKeyAttributeType)
                {
                    result.AddError(new SchemaValidationError(
                        SchemaValidationErrorCode.SortKeyTypeMismatch,
                        element: tableDescription.TableName,
                        expected: metadata.SortKeyAttributeType,
                        actual: skAttributeDef.AttributeType.Value,
                        message: "Sort key type mismatch"));
                }
            }
        }
    }


    /// <summary>
    /// Validates Global Secondary Index configuration.
    /// </summary>
    private void ValidateGlobalSecondaryIndexes(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var tableGsis = tableDescription.GlobalSecondaryIndexes ?? new List<GlobalSecondaryIndexDescription>();
        var expectedGsis = metadata.Indexes
            .Where(i => i.IndexType == IndexType.GlobalSecondaryIndex)
            .ToList();

        // Check for missing GSIs (defined in metadata but not in table)
        foreach (var expectedGsi in expectedGsis)
        {
            var tableGsi = tableGsis.FirstOrDefault(g => g.IndexName == expectedGsi.IndexName);
            if (tableGsi == null)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.GsiNotFound,
                    element: expectedGsi.IndexName,
                    expected: expectedGsi.IndexName,
                    actual: "not found",
                    message: $"GSI '{expectedGsi.IndexName}' defined in entity metadata does not exist on the table"));
            }
            else
            {
                // Validate GSI key schema
                ValidateGsiKeySchema(tableGsi, expectedGsi, tableDescription.AttributeDefinitions, result, options);
            }
        }

        // Check for unexpected GSIs (in table but not in metadata)
        foreach (var tableGsi in tableGsis)
        {
            var expectedGsi = expectedGsis.FirstOrDefault(g => g.IndexName == tableGsi.IndexName);
            if (expectedGsi == null)
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.UnexpectedGsi,
                    element: tableGsi.IndexName,
                    message: $"GSI '{tableGsi.IndexName}' exists on the table but is not defined in entity metadata. This may be intentional if the index is used by other entities."));
            }
        }
    }

    /// <summary>
    /// Validates GSI key schema.
    /// </summary>
    private void ValidateGsiKeySchema(
        GlobalSecondaryIndexDescription tableGsi,
        IndexMetadata expectedGsi,
        List<AttributeDefinition> attributeDefinitions,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var gsiPartitionKey = tableGsi.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
        var gsiSortKey = tableGsi.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);

        // Validate partition key name
        if (gsiPartitionKey != null && gsiPartitionKey.AttributeName != expectedGsi.PartitionKeyAttributeName)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.GsiPartitionKeyNameMismatch,
                element: expectedGsi.IndexName,
                expected: expectedGsi.PartitionKeyAttributeName,
                actual: gsiPartitionKey.AttributeName,
                message: $"GSI partition key name mismatch"));
        }
        else if (gsiPartitionKey != null)
        {
            // Validate partition key type
            var pkAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == gsiPartitionKey.AttributeName);
            if (pkAttributeDef != null && pkAttributeDef.AttributeType.Value != expectedGsi.PartitionKeyAttributeType)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.GsiPartitionKeyTypeMismatch,
                    element: expectedGsi.IndexName,
                    expected: expectedGsi.PartitionKeyAttributeType,
                    actual: pkAttributeDef.AttributeType.Value,
                    message: $"GSI partition key type mismatch"));
            }
        }

        // Validate sort key
        ValidateGsiSortKey(tableGsi.IndexName, gsiSortKey, expectedGsi, attributeDefinitions, result);

        // Validate projection
        ValidateIndexProjection(tableGsi.IndexName, tableGsi.Projection, expectedGsi, result, options);
    }

    /// <summary>
    /// Validates GSI sort key configuration.
    /// </summary>
    private void ValidateGsiSortKey(
        string indexName,
        KeySchemaElement? gsiSortKey,
        IndexMetadata expectedGsi,
        List<AttributeDefinition> attributeDefinitions,
        SchemaValidationResult result)
    {
        var expectedHasSortKey = !string.IsNullOrEmpty(expectedGsi.SortKeyAttributeName);
        var actualHasSortKey = gsiSortKey != null;

        if (expectedHasSortKey != actualHasSortKey)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.GsiSortKeyMismatch,
                element: indexName,
                expected: expectedHasSortKey ? expectedGsi.SortKeyAttributeName! : "none",
                actual: actualHasSortKey ? gsiSortKey!.AttributeName : "none",
                message: expectedHasSortKey
                    ? $"GSI sort key expected but not found"
                    : $"GSI has unexpected sort key"));
        }
        else if (expectedHasSortKey && actualHasSortKey)
        {
            // Both have sort key - validate name and type
            if (gsiSortKey!.AttributeName != expectedGsi.SortKeyAttributeName)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.GsiSortKeyMismatch,
                    element: indexName,
                    expected: expectedGsi.SortKeyAttributeName!,
                    actual: gsiSortKey.AttributeName,
                    message: "GSI sort key name mismatch"));
            }
            else if (expectedGsi.SortKeyAttributeType != null)
            {
                // Validate sort key type
                var skAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == gsiSortKey.AttributeName);
                if (skAttributeDef != null && skAttributeDef.AttributeType.Value != expectedGsi.SortKeyAttributeType)
                {
                    result.AddError(new SchemaValidationError(
                        SchemaValidationErrorCode.GsiSortKeyMismatch,
                        element: indexName,
                        expected: expectedGsi.SortKeyAttributeType,
                        actual: skAttributeDef.AttributeType.Value,
                        message: "GSI sort key type mismatch"));
                }
            }
        }
    }


    /// <summary>
    /// Validates Local Secondary Index configuration.
    /// </summary>
    private void ValidateLocalSecondaryIndexes(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var tableLsis = tableDescription.LocalSecondaryIndexes ?? new List<LocalSecondaryIndexDescription>();
        var expectedLsis = metadata.Indexes
            .Where(i => i.IndexType == IndexType.LocalSecondaryIndex)
            .ToList();

        // Check for missing LSIs (defined in metadata but not in table)
        foreach (var expectedLsi in expectedLsis)
        {
            var tableLsi = tableLsis.FirstOrDefault(l => l.IndexName == expectedLsi.IndexName);
            if (tableLsi == null)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.LsiNotFound,
                    element: expectedLsi.IndexName,
                    expected: expectedLsi.IndexName,
                    actual: "not found",
                    message: $"LSI '{expectedLsi.IndexName}' defined in entity metadata does not exist on the table"));
            }
            else
            {
                // Validate LSI key schema (LSIs only have sort key to validate - partition key is same as table)
                ValidateLsiKeySchema(tableLsi, expectedLsi, tableDescription.AttributeDefinitions, result, options);
            }
        }

        // Check for unexpected LSIs (in table but not in metadata)
        foreach (var tableLsi in tableLsis)
        {
            var expectedLsi = expectedLsis.FirstOrDefault(l => l.IndexName == tableLsi.IndexName);
            if (expectedLsi == null)
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.UnexpectedLsi,
                    element: tableLsi.IndexName,
                    message: $"LSI '{tableLsi.IndexName}' exists on the table but is not defined in entity metadata. This may be intentional if the index is used by other entities."));
            }
        }
    }

    /// <summary>
    /// Validates LSI key schema.
    /// </summary>
    private void ValidateLsiKeySchema(
        LocalSecondaryIndexDescription tableLsi,
        IndexMetadata expectedLsi,
        List<AttributeDefinition> attributeDefinitions,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        // LSIs share the partition key with the base table, so we only validate the sort key
        var lsiSortKey = tableLsi.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);

        if (lsiSortKey == null)
        {
            // LSI must have a sort key
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.LsiSortKeyNameMismatch,
                element: expectedLsi.IndexName,
                expected: expectedLsi.SortKeyAttributeName ?? "unknown",
                actual: "not found",
                message: "LSI does not have a sort key defined"));
        }
        else if (lsiSortKey.AttributeName != expectedLsi.SortKeyAttributeName)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.LsiSortKeyNameMismatch,
                element: expectedLsi.IndexName,
                expected: expectedLsi.SortKeyAttributeName ?? "unknown",
                actual: lsiSortKey.AttributeName,
                message: "LSI sort key name mismatch"));
        }
        else if (expectedLsi.SortKeyAttributeType != null)
        {
            // Validate sort key type
            var skAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == lsiSortKey.AttributeName);
            if (skAttributeDef != null && skAttributeDef.AttributeType.Value != expectedLsi.SortKeyAttributeType)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.LsiSortKeyTypeMismatch,
                    element: expectedLsi.IndexName,
                    expected: expectedLsi.SortKeyAttributeType,
                    actual: skAttributeDef.AttributeType.Value,
                    message: "LSI sort key type mismatch"));
            }
        }

        // Validate projection
        ValidateIndexProjection(tableLsi.IndexName, tableLsi.Projection, expectedLsi, result, options);
    }


    /// <summary>
    /// Validates index projection configuration.
    /// </summary>
    private void ValidateIndexProjection(
        string indexName,
        Projection tableProjection,
        IndexMetadata expectedIndex,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        // Convert DynamoDB projection type to our enum
        var actualProjectionType = tableProjection.ProjectionType.Value switch
        {
            "ALL" => Metadata.ProjectionType.All,
            "KEYS_ONLY" => Metadata.ProjectionType.KeysOnly,
            "INCLUDE" => Metadata.ProjectionType.Include,
            _ => Metadata.ProjectionType.All
        };

        // If projection is not ALL and no projection model is defined, report based on strictness
        if (actualProjectionType != Metadata.ProjectionType.All && !expectedIndex.HasProjectionModel)
        {
            if (options.Strictness == ValidationStrictness.Strict)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.ProjectionModelRequired,
                    element: indexName,
                    expected: "projection model defined",
                    actual: "no projection model",
                    message: $"Index '{indexName}' has projection type '{actualProjectionType}' but no projection model is defined. In Strict mode, a projection model is required for non-ALL projections."));
            }
            else
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.ProjectionModelRecommended,
                    element: indexName,
                    message: $"Index '{indexName}' has projection type '{actualProjectionType}' but no projection model is defined. Consider defining a projection model to ensure type-safe access to projected attributes."));
            }
        }
    }

    /// <summary>
    /// Validates TTL configuration.
    /// </summary>
    private async Task ValidateTtlAsync(
        IAmazonDynamoDB client,
        string tableName,
        EntityMetadata metadata,
        SchemaValidationResult result)
    {
        // Get TTL description
        var ttlResponse = await client.DescribeTimeToLiveAsync(new DescribeTimeToLiveRequest
        {
            TableName = tableName
        });

        var ttlDescription = ttlResponse.TimeToLiveDescription;
        var ttlEnabled = ttlDescription.TimeToLiveStatus == TimeToLiveStatus.ENABLED ||
                         ttlDescription.TimeToLiveStatus == TimeToLiveStatus.ENABLING;
        var tableTtlAttributeName = ttlDescription.AttributeName;

        var expectedHasTtl = !string.IsNullOrEmpty(metadata.TtlAttributeName);

        if (expectedHasTtl && !ttlEnabled)
        {
            // Entity expects TTL but table doesn't have it enabled
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.TtlNotEnabled,
                element: tableName,
                expected: $"TTL enabled on attribute '{metadata.TtlAttributeName}'",
                actual: "TTL not enabled",
                message: "Entity metadata defines a TTL attribute but TTL is not enabled on the table"));
        }
        else if (expectedHasTtl && ttlEnabled && tableTtlAttributeName != metadata.TtlAttributeName)
        {
            // Both have TTL but attribute names don't match
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.TtlAttributeNameMismatch,
                element: tableName,
                expected: metadata.TtlAttributeName!,
                actual: tableTtlAttributeName ?? "unknown",
                message: "TTL attribute name mismatch"));
        }
        else if (!expectedHasTtl && ttlEnabled)
        {
            // Table has TTL but entity doesn't expect it
            result.AddWarning(new SchemaValidationWarning(
                SchemaValidationWarningCode.UnexpectedTtl,
                element: tableName,
                message: $"Table has TTL enabled on attribute '{tableTtlAttributeName}' but the entity metadata does not define a TTL attribute. This may be intentional if TTL is managed separately."));
        }
    }
}
