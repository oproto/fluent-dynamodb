using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Provisioning;

/// <summary>
/// Creates DynamoDB tables from entity metadata.
/// </summary>
public class TableCreator
{
    /// <summary>
    /// Creates a DynamoDB table based on the provided entity metadata.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for CreateTable.</param>
    /// <param name="tableName">The name of the table to create.</param>
    /// <param name="metadata">The entity metadata defining the table schema.</param>
    /// <param name="options">Optional creation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The creation result containing table information.</returns>
    public async Task<TableCreationResult> CreateAsync(
        IAmazonDynamoDB client,
        string tableName,
        EntityMetadata metadata,
        TableCreationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TableCreationOptions();
        
        var request = BuildCreateTableRequest(tableName, metadata, options);
        var response = await client.CreateTableAsync(request, cancellationToken);
        
        var tableStatus = response.TableDescription.TableStatus;
        
        // Wait for table to become active if requested
        if (options.WaitForActive)
        {
            await WaitForTableActiveAsync(client, tableName, options, cancellationToken);
            
            // Update status after waiting
            var describeResponse = await client.DescribeTableAsync(
                new DescribeTableRequest { TableName = tableName }, 
                cancellationToken);
            tableStatus = describeResponse.Table.TableStatus;
        }
        
        // Enable TTL if requested and metadata has TTL attribute
        var ttlEnabled = false;
        if (options.EnableTtl && !string.IsNullOrEmpty(metadata.TtlAttributeName))
        {
            await client.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
            {
                TableName = tableName,
                TimeToLiveSpecification = new TimeToLiveSpecification
                {
                    Enabled = true,
                    AttributeName = metadata.TtlAttributeName
                }
            }, cancellationToken);
            ttlEnabled = true;
        }
        
        return new TableCreationResult
        {
            TableName = response.TableDescription.TableName,
            TableArn = response.TableDescription.TableArn,
            TableStatus = tableStatus,
            TtlEnabled = ttlEnabled
        };
    }

    /// <summary>
    /// Builds a CreateTableRequest from entity metadata without executing it.
    /// Useful for inspection or custom execution scenarios.
    /// </summary>
    /// <param name="tableName">The name of the table to create.</param>
    /// <param name="metadata">The entity metadata defining the table schema.</param>
    /// <param name="options">Optional creation options.</param>
    /// <returns>The CreateTableRequest ready for execution.</returns>
    public CreateTableRequest BuildCreateTableRequest(
        string tableName,
        EntityMetadata metadata,
        TableCreationOptions? options = null)
    {
        ValidateInputs(tableName, metadata);
        
        options ??= new TableCreationOptions();
        
        var request = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = BuildKeySchema(metadata),
            AttributeDefinitions = BuildAttributeDefinitions(metadata),
            BillingMode = options.BillingMode
        };
        
        // Add provisioned throughput if using PROVISIONED billing mode
        if (options.BillingMode == BillingMode.PROVISIONED)
        {
            var throughput = options.ProvisionedThroughput ?? new ProvisionedThroughputConfig();
            request.ProvisionedThroughput = new ProvisionedThroughput
            {
                ReadCapacityUnits = throughput.ReadCapacityUnits,
                WriteCapacityUnits = throughput.WriteCapacityUnits
            };
        }
        
        // Add Global Secondary Indexes
        var gsis = BuildGlobalSecondaryIndexes(metadata, options);
        if (gsis.Count > 0)
        {
            request.GlobalSecondaryIndexes = gsis;
        }
        
        // Add Local Secondary Indexes
        var lsis = BuildLocalSecondaryIndexes(metadata);
        if (lsis.Count > 0)
        {
            request.LocalSecondaryIndexes = lsis;
        }
        
        return request;
    }


    /// <summary>
    /// Validates input parameters for table creation.
    /// </summary>
    private static void ValidateInputs(string tableName, EntityMetadata metadata)
    {
        if (string.IsNullOrEmpty(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty", nameof(tableName));
        }
        
        if (string.IsNullOrEmpty(metadata.PartitionKeyAttributeName))
        {
            throw new ArgumentException("EntityMetadata must have a partition key defined", nameof(metadata));
        }
        
        if (!IsValidAttributeType(metadata.PartitionKeyAttributeType))
        {
            throw new ArgumentException(
                $"Invalid partition key attribute type: {metadata.PartitionKeyAttributeType}. Must be S, N, or B.",
                nameof(metadata));
        }
        
        if (!string.IsNullOrEmpty(metadata.SortKeyAttributeName) && 
            !IsValidAttributeType(metadata.SortKeyAttributeType))
        {
            throw new ArgumentException(
                $"Invalid sort key attribute type: {metadata.SortKeyAttributeType}. Must be S, N, or B.",
                nameof(metadata));
        }
    }

    /// <summary>
    /// Checks if the attribute type is valid (S, N, or B).
    /// </summary>
    private static bool IsValidAttributeType(string? attributeType)
    {
        return attributeType is "S" or "N" or "B";
    }

    /// <summary>
    /// Builds the key schema for the table.
    /// </summary>
    private static List<KeySchemaElement> BuildKeySchema(EntityMetadata metadata)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new()
            {
                AttributeName = metadata.PartitionKeyAttributeName,
                KeyType = KeyType.HASH
            }
        };
        
        if (!string.IsNullOrEmpty(metadata.SortKeyAttributeName))
        {
            keySchema.Add(new KeySchemaElement
            {
                AttributeName = metadata.SortKeyAttributeName,
                KeyType = KeyType.RANGE
            });
        }
        
        return keySchema;
    }

    /// <summary>
    /// Builds attribute definitions for all key attributes in the table and indexes.
    /// </summary>
    private static List<AttributeDefinition> BuildAttributeDefinitions(EntityMetadata metadata)
    {
        var attributeDefinitions = new Dictionary<string, AttributeDefinition>();
        
        // Add table partition key
        AddAttributeDefinition(attributeDefinitions, 
            metadata.PartitionKeyAttributeName, 
            metadata.PartitionKeyAttributeType);
        
        // Add table sort key if present
        if (!string.IsNullOrEmpty(metadata.SortKeyAttributeName))
        {
            AddAttributeDefinition(attributeDefinitions, 
                metadata.SortKeyAttributeName, 
                metadata.SortKeyAttributeType!);
        }
        
        // Add index key attributes
        foreach (var index in metadata.Indexes)
        {
            // Add index partition key (for GSIs)
            if (index.IndexType == IndexType.GlobalSecondaryIndex)
            {
                AddAttributeDefinition(attributeDefinitions, 
                    index.PartitionKeyAttributeName, 
                    index.PartitionKeyAttributeType);
            }
            
            // Add index sort key if present
            if (!string.IsNullOrEmpty(index.SortKeyAttributeName))
            {
                AddAttributeDefinition(attributeDefinitions, 
                    index.SortKeyAttributeName, 
                    index.SortKeyAttributeType!);
            }
        }
        
        return attributeDefinitions.Values.ToList();
    }

    /// <summary>
    /// Adds an attribute definition to the dictionary if not already present.
    /// </summary>
    private static void AddAttributeDefinition(
        Dictionary<string, AttributeDefinition> definitions,
        string attributeName,
        string attributeType)
    {
        if (!definitions.ContainsKey(attributeName))
        {
            definitions[attributeName] = new AttributeDefinition
            {
                AttributeName = attributeName,
                AttributeType = new ScalarAttributeType(attributeType)
            };
        }
    }

    /// <summary>
    /// Builds Global Secondary Index definitions.
    /// </summary>
    private static List<GlobalSecondaryIndex> BuildGlobalSecondaryIndexes(
        EntityMetadata metadata,
        TableCreationOptions options)
    {
        var gsis = new List<GlobalSecondaryIndex>();
        
        foreach (var index in metadata.Indexes.Where(i => i.IndexType == IndexType.GlobalSecondaryIndex))
        {
            var gsi = new GlobalSecondaryIndex
            {
                IndexName = index.IndexName,
                KeySchema = BuildIndexKeySchema(index),
                Projection = BuildProjection(index)
            };
            
            // Add provisioned throughput for GSIs if using PROVISIONED billing mode
            if (options.BillingMode == BillingMode.PROVISIONED)
            {
                var throughput = options.GsiProvisionedThroughput 
                    ?? options.ProvisionedThroughput 
                    ?? new ProvisionedThroughputConfig();
                    
                gsi.ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = throughput.ReadCapacityUnits,
                    WriteCapacityUnits = throughput.WriteCapacityUnits
                };
            }
            
            gsis.Add(gsi);
        }
        
        return gsis;
    }

    /// <summary>
    /// Builds Local Secondary Index definitions.
    /// </summary>
    private static List<LocalSecondaryIndex> BuildLocalSecondaryIndexes(EntityMetadata metadata)
    {
        var lsis = new List<LocalSecondaryIndex>();
        
        foreach (var index in metadata.Indexes.Where(i => i.IndexType == IndexType.LocalSecondaryIndex))
        {
            var lsi = new LocalSecondaryIndex
            {
                IndexName = index.IndexName,
                KeySchema = BuildLsiKeySchema(metadata, index),
                Projection = BuildProjection(index)
            };
            
            lsis.Add(lsi);
        }
        
        return lsis;
    }

    /// <summary>
    /// Builds the key schema for a GSI.
    /// </summary>
    private static List<KeySchemaElement> BuildIndexKeySchema(IndexMetadata index)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new()
            {
                AttributeName = index.PartitionKeyAttributeName,
                KeyType = KeyType.HASH
            }
        };
        
        if (!string.IsNullOrEmpty(index.SortKeyAttributeName))
        {
            keySchema.Add(new KeySchemaElement
            {
                AttributeName = index.SortKeyAttributeName,
                KeyType = KeyType.RANGE
            });
        }
        
        return keySchema;
    }

    /// <summary>
    /// Builds the key schema for an LSI (uses table's partition key).
    /// </summary>
    private static List<KeySchemaElement> BuildLsiKeySchema(EntityMetadata metadata, IndexMetadata index)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new()
            {
                AttributeName = metadata.PartitionKeyAttributeName,
                KeyType = KeyType.HASH
            }
        };
        
        if (!string.IsNullOrEmpty(index.SortKeyAttributeName))
        {
            keySchema.Add(new KeySchemaElement
            {
                AttributeName = index.SortKeyAttributeName,
                KeyType = KeyType.RANGE
            });
        }
        
        return keySchema;
    }

    /// <summary>
    /// Builds the projection configuration for an index.
    /// </summary>
    private static Projection BuildProjection(IndexMetadata index)
    {
        var projection = new Projection
        {
            ProjectionType = index.ProjectionType switch
            {
                Metadata.ProjectionType.All => Amazon.DynamoDBv2.ProjectionType.ALL,
                Metadata.ProjectionType.KeysOnly => Amazon.DynamoDBv2.ProjectionType.KEYS_ONLY,
                Metadata.ProjectionType.Include => Amazon.DynamoDBv2.ProjectionType.INCLUDE,
                _ => Amazon.DynamoDBv2.ProjectionType.ALL
            }
        };
        
        // Add non-key attributes for INCLUDE projection
        if (index.ProjectionType == Metadata.ProjectionType.Include && 
            index.ProjectedProperties.Length > 0)
        {
            projection.NonKeyAttributes = index.ProjectedProperties.ToList();
        }
        
        return projection;
    }

    /// <summary>
    /// Waits for the table to become active.
    /// </summary>
    private static async Task WaitForTableActiveAsync(
        IAmazonDynamoDB client,
        string tableName,
        TableCreationOptions options,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        while (true)
        {
            var response = await client.DescribeTableAsync(
                new DescribeTableRequest { TableName = tableName },
                cancellationToken);
            
            if (response.Table.TableStatus == TableStatus.ACTIVE)
            {
                return;
            }
            
            if (DateTime.UtcNow - startTime > options.WaitTimeout)
            {
                throw new TimeoutException(
                    $"Table '{tableName}' did not become ACTIVE within {options.WaitTimeout.TotalSeconds} seconds");
            }
            
            await Task.Delay(options.PollingInterval, cancellationToken);
        }
    }
}
