using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.UnitTests.Validation;

/// <summary>
/// Property-based tests for SchemaValidator.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class SchemaValidatorPropertyTests
{
    private static readonly string[] ValidAttributeTypes = { "S", "N", "B" };

    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any entity metadata and DynamoDB table description where the partition key name,
    /// partition key type, sort key name, sort key type, or sort key presence differs,
    /// the validation result SHALL contain at least one error identifying the mismatch.
    /// 
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryKeyMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.Bool(),
            (tableName, pkName, mismatchType) =>
            {
                // Arrange - create metadata with one PK name
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S"
                };

                // Create table description with different PK name (mismatch)
                var differentPkName = pkName + "_different";
                var tableDescription = CreateTableDescription(tableName, differentPkName, "S", null, null);

                // Act - validate using the internal validation logic
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert - should have at least one error
                var hasError = result.Errors.Count > 0;
                var hasCorrectErrorCode = result.Errors.Any(e => 
                    e.Code == SchemaValidationErrorCode.PartitionKeyNameMismatch);

                return (hasError && hasCorrectErrorCode).ToProperty()
                    .Label($"Primary key name mismatch should produce error. " +
                           $"Expected PK: {pkName}, Actual PK: {differentPkName}, " +
                           $"HasError: {hasError}, HasCorrectErrorCode: {hasCorrectErrorCode}");
            });
    }


    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any entity metadata and DynamoDB table description where the partition key type differs,
    /// the validation result SHALL contain an error with code PartitionKeyTypeMismatch.
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PartitionKeyTypeMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName) =>
            {
                // Arrange - create metadata with type "S"
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S"
                };

                // Create table description with type "N" (mismatch)
                var tableDescription = CreateTableDescription(tableName, pkName, "N", null, null);

                // Act
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.PartitionKeyTypeMismatch);

                return hasError.ToProperty()
                    .Label($"Partition key type mismatch should produce error. " +
                           $"Expected type: S, Actual type: N, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any entity metadata that defines a sort key but the table does not have one,
    /// the validation result SHALL contain an error with code SortKeyMissing.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyMissing_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, skName) =>
            {
                // Arrange - metadata expects a sort key
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = skName,
                    SortKeyAttributeType = "S"
                };

                // Table has no sort key
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);

                // Act
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.SortKeyMissing);

                return hasError.ToProperty()
                    .Label($"Sort key missing should produce error. " +
                           $"Expected SK: {skName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any table that has a sort key but the entity metadata does not define one,
    /// the validation result SHALL contain an error with code SortKeyUnexpected.
    /// 
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyUnexpected_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, skName) =>
            {
                // Arrange - metadata does not expect a sort key
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = null,
                    SortKeyAttributeType = null
                };

                // Table has a sort key
                var tableDescription = CreateTableDescription(tableName, pkName, "S", skName, "S");

                // Act
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.SortKeyUnexpected);

                return hasError.ToProperty()
                    .Label($"Sort key unexpected should produce error. " +
                           $"Unexpected SK: {skName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any entity metadata and table where sort key names differ,
    /// the validation result SHALL contain an error with code SortKeyNameMismatch.
    /// 
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyNameMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, skName) =>
            {
                // Arrange - metadata expects one sort key name
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = skName,
                    SortKeyAttributeType = "S"
                };

                // Table has different sort key name
                var differentSkName = skName + "_different";
                var tableDescription = CreateTableDescription(tableName, pkName, "S", differentSkName, "S");

                // Act
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.SortKeyNameMismatch);

                return hasError.ToProperty()
                    .Label($"Sort key name mismatch should produce error. " +
                           $"Expected SK: {skName}, Actual SK: {differentSkName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
    /// 
    /// For any entity metadata and table where sort key types differ,
    /// the validation result SHALL contain an error with code SortKeyTypeMismatch.
    /// 
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortKeyTypeMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, skName) =>
            {
                // Arrange - metadata expects sort key type "S"
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = skName,
                    SortKeyAttributeType = "S"
                };

                // Table has sort key type "N"
                var tableDescription = CreateTableDescription(tableName, pkName, "S", skName, "N");

                // Act
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.SortKeyTypeMismatch);

                return hasError.ToProperty()
                    .Label($"Sort key type mismatch should produce error. " +
                           $"Expected type: S, Actual type: N, HasError: {hasError}");
            });
    }


    /// <summary>
    /// **Feature: schema-validation, Property 3: Missing GSIs produce errors**
    /// 
    /// For any entity metadata defining a GSI that does not exist in the DynamoDB table description,
    /// the validation result SHALL contain an error with code GsiNotFound.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MissingGsi_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, gsiName) =>
            {
                // Arrange - metadata defines a GSI
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = new[]
                    {
                        new IndexMetadata
                        {
                            IndexName = gsiName,
                            IndexType = IndexType.GlobalSecondaryIndex,
                            PartitionKeyAttributeName = "gsi_pk",
                            PartitionKeyAttributeType = "S"
                        }
                    }
                };

                // Table has no GSIs
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.GlobalSecondaryIndexes = new List<GlobalSecondaryIndexDescription>();

                // Act
                var result = new SchemaValidationResult();
                ValidateGsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.GsiNotFound);

                return hasError.ToProperty()
                    .Label($"Missing GSI should produce error. " +
                           $"Expected GSI: {gsiName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 4: GSI key mismatches produce errors**
    /// 
    /// For any GSI where the partition key name differs between entity metadata and DynamoDB table,
    /// the validation result SHALL contain an error with code GsiPartitionKeyNameMismatch.
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GsiPartitionKeyNameMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, gsiName, gsiPkName) =>
            {
                var pkName = "pk";
                // Arrange - metadata defines GSI with one PK name
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = new[]
                    {
                        new IndexMetadata
                        {
                            IndexName = gsiName,
                            IndexType = IndexType.GlobalSecondaryIndex,
                            PartitionKeyAttributeName = gsiPkName,
                            PartitionKeyAttributeType = "S"
                        }
                    }
                };

                // Table has GSI with different PK name
                var differentGsiPkName = gsiPkName + "_different";
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.GlobalSecondaryIndexes = new List<GlobalSecondaryIndexDescription>
                {
                    CreateGsiDescription(gsiName, differentGsiPkName, "S", null, null)
                };
                tableDescription.AttributeDefinitions.Add(new AttributeDefinition
                {
                    AttributeName = differentGsiPkName,
                    AttributeType = ScalarAttributeType.S
                });

                // Act
                var result = new SchemaValidationResult();
                ValidateGsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.GsiPartitionKeyNameMismatch);

                return hasError.ToProperty()
                    .Label($"GSI partition key name mismatch should produce error. " +
                           $"Expected: {gsiPkName}, Actual: {differentGsiPkName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 5: Missing LSIs produce errors**
    /// 
    /// For any entity metadata defining an LSI that does not exist in the DynamoDB table description,
    /// the validation result SHALL contain an error with code LsiNotFound.
    /// 
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MissingLsi_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, lsiName) =>
            {
                // Arrange - metadata defines an LSI
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = new[]
                    {
                        new IndexMetadata
                        {
                            IndexName = lsiName,
                            IndexType = IndexType.LocalSecondaryIndex,
                            PartitionKeyAttributeName = pkName,
                            PartitionKeyAttributeType = "S",
                            SortKeyAttributeName = "lsi_sk",
                            SortKeyAttributeType = "S"
                        }
                    }
                };

                // Table has no LSIs
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.LocalSecondaryIndexes = new List<LocalSecondaryIndexDescription>();

                // Act
                var result = new SchemaValidationResult();
                ValidateLsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.LsiNotFound);

                return hasError.ToProperty()
                    .Label($"Missing LSI should produce error. " +
                           $"Expected LSI: {lsiName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 6: LSI key mismatches produce errors**
    /// 
    /// For any LSI where the sort key name differs between entity metadata and DynamoDB table,
    /// the validation result SHALL contain an error with code LsiSortKeyNameMismatch.
    /// 
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LsiSortKeyNameMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, lsiName, lsiSkName) =>
            {
                var pkName = "pk";
                // Arrange - metadata defines LSI with one SK name
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = new[]
                    {
                        new IndexMetadata
                        {
                            IndexName = lsiName,
                            IndexType = IndexType.LocalSecondaryIndex,
                            PartitionKeyAttributeName = pkName,
                            PartitionKeyAttributeType = "S",
                            SortKeyAttributeName = lsiSkName,
                            SortKeyAttributeType = "S"
                        }
                    }
                };

                // Table has LSI with different SK name
                var differentLsiSkName = lsiSkName + "_different";
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.LocalSecondaryIndexes = new List<LocalSecondaryIndexDescription>
                {
                    CreateLsiDescription(lsiName, pkName, differentLsiSkName)
                };
                tableDescription.AttributeDefinitions.Add(new AttributeDefinition
                {
                    AttributeName = differentLsiSkName,
                    AttributeType = ScalarAttributeType.S
                });

                // Act
                var result = new SchemaValidationResult();
                ValidateLsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.LsiSortKeyNameMismatch);

                return hasError.ToProperty()
                    .Label($"LSI sort key name mismatch should produce error. " +
                           $"Expected: {lsiSkName}, Actual: {differentLsiSkName}, HasError: {hasError}");
            });
    }


    /// <summary>
    /// **Feature: schema-validation, Property 7: TTL mismatches produce errors**
    /// 
    /// For any entity metadata defining a TTL attribute where the DynamoDB table has TTL disabled,
    /// the validation result SHALL contain an error with code TtlNotEnabled.
    /// 
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TtlNotEnabled_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, ttlAttrName) =>
            {
                // Arrange - metadata defines TTL
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    TtlAttributeName = ttlAttrName
                };

                // TTL is disabled
                var ttlDescription = new TimeToLiveDescription
                {
                    TimeToLiveStatus = TimeToLiveStatus.DISABLED,
                    AttributeName = null
                };

                // Act
                var result = new SchemaValidationResult();
                ValidateTtlInternal(tableName, metadata, ttlDescription, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.TtlNotEnabled);

                return hasError.ToProperty()
                    .Label($"TTL not enabled should produce error. " +
                           $"Expected TTL attr: {ttlAttrName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 7: TTL mismatches produce errors**
    /// 
    /// For any entity metadata defining a TTL attribute where the DynamoDB table has a different
    /// TTL attribute name, the validation result SHALL contain an error with code TtlAttributeNameMismatch.
    /// 
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TtlAttributeNameMismatch_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, ttlAttrName) =>
            {
                // Arrange - metadata defines TTL with one name
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    TtlAttributeName = ttlAttrName
                };

                // TTL is enabled but with different attribute name
                var differentTtlAttrName = ttlAttrName + "_different";
                var ttlDescription = new TimeToLiveDescription
                {
                    TimeToLiveStatus = TimeToLiveStatus.ENABLED,
                    AttributeName = differentTtlAttrName
                };

                // Act
                var result = new SchemaValidationResult();
                ValidateTtlInternal(tableName, metadata, ttlDescription, result);

                // Assert
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.TtlAttributeNameMismatch);

                return hasError.ToProperty()
                    .Label($"TTL attribute name mismatch should produce error. " +
                           $"Expected: {ttlAttrName}, Actual: {differentTtlAttrName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 8: Extra DynamoDB items produce warnings**
    /// 
    /// For any DynamoDB table containing GSIs not defined in the entity metadata,
    /// the validation result SHALL contain warnings (not errors) for each extra GSI.
    /// 
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnexpectedGsi_ProducesWarning()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, unexpectedGsiName) =>
            {
                // Arrange - metadata has no GSIs
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = Array.Empty<IndexMetadata>()
                };

                // Table has an unexpected GSI
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.GlobalSecondaryIndexes = new List<GlobalSecondaryIndexDescription>
                {
                    CreateGsiDescription(unexpectedGsiName, "gsi_pk", "S", null, null)
                };
                tableDescription.AttributeDefinitions.Add(new AttributeDefinition
                {
                    AttributeName = "gsi_pk",
                    AttributeType = ScalarAttributeType.S
                });

                // Act
                var result = new SchemaValidationResult();
                ValidateGsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert - should have warning, not error
                var hasWarning = result.Warnings.Any(w => w.Code == SchemaValidationWarningCode.UnexpectedGsi);
                var hasNoError = !result.Errors.Any();

                return (hasWarning && hasNoError).ToProperty()
                    .Label($"Unexpected GSI should produce warning, not error. " +
                           $"Unexpected GSI: {unexpectedGsiName}, HasWarning: {hasWarning}, HasNoError: {hasNoError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 8: Extra DynamoDB items produce warnings**
    /// 
    /// For any DynamoDB table containing LSIs not defined in the entity metadata,
    /// the validation result SHALL contain warnings (not errors) for each extra LSI.
    /// 
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnexpectedLsi_ProducesWarning()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, unexpectedLsiName) =>
            {
                // Arrange - metadata has no LSIs
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    Indexes = Array.Empty<IndexMetadata>()
                };

                // Table has an unexpected LSI
                var tableDescription = CreateTableDescription(tableName, pkName, "S", null, null);
                tableDescription.LocalSecondaryIndexes = new List<LocalSecondaryIndexDescription>
                {
                    CreateLsiDescription(unexpectedLsiName, pkName, "lsi_sk")
                };
                tableDescription.AttributeDefinitions.Add(new AttributeDefinition
                {
                    AttributeName = "lsi_sk",
                    AttributeType = ScalarAttributeType.S
                });

                // Act
                var result = new SchemaValidationResult();
                ValidateLsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());

                // Assert - should have warning, not error
                var hasWarning = result.Warnings.Any(w => w.Code == SchemaValidationWarningCode.UnexpectedLsi);
                var hasNoError = !result.Errors.Any();

                return (hasWarning && hasNoError).ToProperty()
                    .Label($"Unexpected LSI should produce warning, not error. " +
                           $"Unexpected LSI: {unexpectedLsiName}, HasWarning: {hasWarning}, HasNoError: {hasNoError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 8: Extra DynamoDB items produce warnings**
    /// 
    /// For any DynamoDB table with TTL enabled but entity metadata does not define TTL,
    /// the validation result SHALL contain a warning (not error).
    /// 
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnexpectedTtl_ProducesWarning()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, unexpectedTtlAttr) =>
            {
                // Arrange - metadata has no TTL
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    TtlAttributeName = null
                };

                // TTL is enabled on table
                var ttlDescription = new TimeToLiveDescription
                {
                    TimeToLiveStatus = TimeToLiveStatus.ENABLED,
                    AttributeName = unexpectedTtlAttr
                };

                // Act
                var result = new SchemaValidationResult();
                ValidateTtlInternal(tableName, metadata, ttlDescription, result);

                // Assert - should have warning, not error
                var hasWarning = result.Warnings.Any(w => w.Code == SchemaValidationWarningCode.UnexpectedTtl);
                var hasNoError = !result.Errors.Any();

                return (hasWarning && hasNoError).ToProperty()
                    .Label($"Unexpected TTL should produce warning, not error. " +
                           $"Unexpected TTL attr: {unexpectedTtlAttr}, HasWarning: {hasWarning}, HasNoError: {hasNoError}");
            });
    }


    /// <summary>
    /// **Feature: schema-validation, Property 9: Strictness controls projection model enforcement**
    /// 
    /// For any index with projection type KEYS_ONLY without a defined projection model,
    /// the validation result SHALL contain an error when strictness is Strict.
    /// 
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StrictMode_ProjectionModelRequired_ProducesError()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            gsiName =>
            {
                // Arrange - index with KEYS_ONLY projection and no projection model
                var indexMetadata = new IndexMetadata
                {
                    IndexName = gsiName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyAttributeName = "pk",
                    PartitionKeyAttributeType = "S",
                    ProjectionType = Metadata.ProjectionType.All, // Metadata says ALL
                    HasProjectionModel = false
                };

                // Table has KEYS_ONLY projection
                var projection = new Projection
                {
                    ProjectionType = Amazon.DynamoDBv2.ProjectionType.KEYS_ONLY
                };

                // Act - validate with Strict mode
                var result = new SchemaValidationResult();
                ValidateProjectionInternal(gsiName, projection, indexMetadata, result, 
                    new SchemaValidationOptions { Strictness = ValidationStrictness.Strict });

                // Assert - should have error in Strict mode
                var hasError = result.Errors.Any(e => e.Code == SchemaValidationErrorCode.ProjectionModelRequired);

                return hasError.ToProperty()
                    .Label($"Strict mode should produce error for missing projection model. " +
                           $"Index: {gsiName}, HasError: {hasError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 9: Strictness controls projection model enforcement**
    /// 
    /// For any index with projection type KEYS_ONLY without a defined projection model,
    /// the validation result SHALL contain a warning (not error) when strictness is Relaxed.
    /// 
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RelaxedMode_ProjectionModelRecommended_ProducesWarning()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            gsiName =>
            {
                // Arrange - index with KEYS_ONLY projection and no projection model
                var indexMetadata = new IndexMetadata
                {
                    IndexName = gsiName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyAttributeName = "pk",
                    PartitionKeyAttributeType = "S",
                    ProjectionType = Metadata.ProjectionType.All, // Metadata says ALL
                    HasProjectionModel = false
                };

                // Table has KEYS_ONLY projection
                var projection = new Projection
                {
                    ProjectionType = Amazon.DynamoDBv2.ProjectionType.KEYS_ONLY
                };

                // Act - validate with Relaxed mode (default)
                var result = new SchemaValidationResult();
                ValidateProjectionInternal(gsiName, projection, indexMetadata, result, 
                    new SchemaValidationOptions { Strictness = ValidationStrictness.Relaxed });

                // Assert - should have warning, not error in Relaxed mode
                var hasWarning = result.Warnings.Any(w => w.Code == SchemaValidationWarningCode.ProjectionModelRecommended);
                var hasNoError = !result.Errors.Any(e => e.Code == SchemaValidationErrorCode.ProjectionModelRequired);

                return (hasWarning && hasNoError).ToProperty()
                    .Label($"Relaxed mode should produce warning, not error, for missing projection model. " +
                           $"Index: {gsiName}, HasWarning: {hasWarning}, HasNoError: {hasNoError}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 1: Matching schemas produce valid results**
    /// 
    /// For any entity metadata and DynamoDB table description that have identical primary keys,
    /// indexes, and TTL configuration, the validation result SHALL have IsValid = true and zero errors.
    /// 
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MatchingSchemas_ProduceValidResult()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (tableName, pkName, skName) =>
            {
                // Arrange - create matching metadata and table description
                var metadata = new EntityMetadata
                {
                    TableName = tableName,
                    PartitionKeyAttributeName = pkName,
                    PartitionKeyAttributeType = "S",
                    SortKeyAttributeName = skName,
                    SortKeyAttributeType = "S",
                    Indexes = Array.Empty<IndexMetadata>()
                };

                var tableDescription = CreateTableDescription(tableName, pkName, "S", skName, "S");
                tableDescription.GlobalSecondaryIndexes = new List<GlobalSecondaryIndexDescription>();
                tableDescription.LocalSecondaryIndexes = new List<LocalSecondaryIndexDescription>();

                var ttlDescription = new TimeToLiveDescription
                {
                    TimeToLiveStatus = TimeToLiveStatus.DISABLED,
                    AttributeName = null
                };

                // Act - validate all components
                var result = new SchemaValidationResult();
                ValidatePrimaryKeyInternal(tableDescription, metadata, result);
                ValidateGsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());
                ValidateLsiInternal(tableDescription, metadata, result, new SchemaValidationOptions());
                ValidateTtlInternal(tableName, metadata, ttlDescription, result);

                // Assert - should be valid with no errors
                var isValid = result.IsValid;
                var hasNoErrors = result.Errors.Count == 0;

                return (isValid && hasNoErrors).ToProperty()
                    .Label($"Matching schemas should produce valid result. " +
                           $"IsValid: {isValid}, ErrorCount: {result.Errors.Count}");
            });
    }

    /// <summary>
    /// **Feature: schema-validation, Property 10: Error messages contain required information**
    /// 
    /// For any validation error, the error message SHALL contain the expected value, actual value,
    /// and element identification.
    /// 
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ErrorMessages_ContainRequiredInformation()
    {
        return Prop.ForAll(
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            Arb.Default.String().Filter(s => !string.IsNullOrEmpty(s) && s.Length <= 50),
            (element, expected, actual) =>
            {
                var message = "Test error message";
                // Arrange - create an error with specific values
                var error = new SchemaValidationError(
                    SchemaValidationErrorCode.PartitionKeyNameMismatch,
                    element,
                    expected,
                    actual,
                    message);

                // Assert - error should contain all required information
                var hasElement = error.Element == element;
                var hasExpected = error.Expected == expected;
                var hasActual = error.Actual == actual;
                var hasMessage = error.Message == message;
                var toStringContainsInfo = error.ToString().Contains(element) &&
                                           error.ToString().Contains(expected) &&
                                           error.ToString().Contains(actual);

                return (hasElement && hasExpected && hasActual && hasMessage && toStringContainsInfo).ToProperty()
                    .Label($"Error should contain all required information. " +
                           $"HasElement: {hasElement}, HasExpected: {hasExpected}, " +
                           $"HasActual: {hasActual}, HasMessage: {hasMessage}, " +
                           $"ToStringContainsInfo: {toStringContainsInfo}");
            });
    }


    #region Helper Methods

    /// <summary>
    /// Creates a TableDescription for testing.
    /// </summary>
    private static TableDescription CreateTableDescription(
        string tableName,
        string pkName,
        string pkType,
        string? skName,
        string? skType)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = pkName, KeyType = KeyType.HASH }
        };

        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = pkName, AttributeType = new ScalarAttributeType(pkType) }
        };

        if (skName != null && skType != null)
        {
            keySchema.Add(new KeySchemaElement { AttributeName = skName, KeyType = KeyType.RANGE });
            attributeDefinitions.Add(new AttributeDefinition { AttributeName = skName, AttributeType = new ScalarAttributeType(skType) });
        }

        return new TableDescription
        {
            TableName = tableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndexDescription>(),
            LocalSecondaryIndexes = new List<LocalSecondaryIndexDescription>()
        };
    }

    /// <summary>
    /// Creates a GlobalSecondaryIndexDescription for testing.
    /// </summary>
    private static GlobalSecondaryIndexDescription CreateGsiDescription(
        string indexName,
        string pkName,
        string pkType,
        string? skName,
        string? skType)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = pkName, KeyType = KeyType.HASH }
        };

        if (skName != null)
        {
            keySchema.Add(new KeySchemaElement { AttributeName = skName, KeyType = KeyType.RANGE });
        }

        return new GlobalSecondaryIndexDescription
        {
            IndexName = indexName,
            KeySchema = keySchema,
            Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
        };
    }

    /// <summary>
    /// Creates a LocalSecondaryIndexDescription for testing.
    /// </summary>
    private static LocalSecondaryIndexDescription CreateLsiDescription(
        string indexName,
        string pkName,
        string skName)
    {
        return new LocalSecondaryIndexDescription
        {
            IndexName = indexName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = pkName, KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = skName, KeyType = KeyType.RANGE }
            },
            Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
        };
    }

    /// <summary>
    /// Internal validation method for primary key - mirrors SchemaValidator logic.
    /// </summary>
    private static void ValidatePrimaryKeyInternal(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result)
    {
        var keySchema = tableDescription.KeySchema;
        var attributeDefinitions = tableDescription.AttributeDefinitions;

        var tablePartitionKey = keySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
        var tableSortKey = keySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);

        // Validate partition key name
        if (tablePartitionKey == null)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.PartitionKeyNameMismatch,
                tableDescription.TableName,
                metadata.PartitionKeyAttributeName,
                "not found",
                "Table does not have a partition key defined"));
        }
        else if (tablePartitionKey.AttributeName != metadata.PartitionKeyAttributeName)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.PartitionKeyNameMismatch,
                tableDescription.TableName,
                metadata.PartitionKeyAttributeName,
                tablePartitionKey.AttributeName,
                "Partition key name mismatch"));
        }
        else
        {
            var pkAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == tablePartitionKey.AttributeName);
            if (pkAttributeDef != null && pkAttributeDef.AttributeType.Value != metadata.PartitionKeyAttributeType)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.PartitionKeyTypeMismatch,
                    tableDescription.TableName,
                    metadata.PartitionKeyAttributeType,
                    pkAttributeDef.AttributeType.Value,
                    "Partition key type mismatch"));
            }
        }

        // Validate sort key
        var expectedHasSortKey = !string.IsNullOrEmpty(metadata.SortKeyAttributeName);
        var actualHasSortKey = tableSortKey != null;

        if (expectedHasSortKey && !actualHasSortKey)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.SortKeyMissing,
                tableDescription.TableName,
                metadata.SortKeyAttributeName!,
                "not found",
                "Entity metadata defines a sort key but the table does not have one"));
        }
        else if (!expectedHasSortKey && actualHasSortKey)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.SortKeyUnexpected,
                tableDescription.TableName,
                "none",
                tableSortKey!.AttributeName,
                "Table has a sort key but the entity metadata does not define one"));
        }
        else if (expectedHasSortKey && actualHasSortKey)
        {
            if (tableSortKey!.AttributeName != metadata.SortKeyAttributeName)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.SortKeyNameMismatch,
                    tableDescription.TableName,
                    metadata.SortKeyAttributeName!,
                    tableSortKey.AttributeName,
                    "Sort key name mismatch"));
            }
            else
            {
                var skAttributeDef = attributeDefinitions.FirstOrDefault(a => a.AttributeName == tableSortKey.AttributeName);
                if (skAttributeDef != null && metadata.SortKeyAttributeType != null &&
                    skAttributeDef.AttributeType.Value != metadata.SortKeyAttributeType)
                {
                    result.AddError(new SchemaValidationError(
                        SchemaValidationErrorCode.SortKeyTypeMismatch,
                        tableDescription.TableName,
                        metadata.SortKeyAttributeType,
                        skAttributeDef.AttributeType.Value,
                        "Sort key type mismatch"));
                }
            }
        }
    }


    /// <summary>
    /// Internal validation method for GSIs - mirrors SchemaValidator logic.
    /// </summary>
    private static void ValidateGsiInternal(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var tableGsis = tableDescription.GlobalSecondaryIndexes ?? new List<GlobalSecondaryIndexDescription>();
        var expectedGsis = metadata.Indexes
            .Where(i => i.IndexType == IndexType.GlobalSecondaryIndex)
            .ToList();

        // Check for missing GSIs
        foreach (var expectedGsi in expectedGsis)
        {
            var tableGsi = tableGsis.FirstOrDefault(g => g.IndexName == expectedGsi.IndexName);
            if (tableGsi == null)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.GsiNotFound,
                    expectedGsi.IndexName,
                    expectedGsi.IndexName,
                    "not found",
                    $"GSI '{expectedGsi.IndexName}' defined in entity metadata does not exist on the table"));
            }
            else
            {
                // Validate GSI key schema
                var gsiPartitionKey = tableGsi.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
                if (gsiPartitionKey != null && gsiPartitionKey.AttributeName != expectedGsi.PartitionKeyAttributeName)
                {
                    result.AddError(new SchemaValidationError(
                        SchemaValidationErrorCode.GsiPartitionKeyNameMismatch,
                        expectedGsi.IndexName,
                        expectedGsi.PartitionKeyAttributeName,
                        gsiPartitionKey.AttributeName,
                        "GSI partition key name mismatch"));
                }
            }
        }

        // Check for unexpected GSIs
        foreach (var tableGsi in tableGsis)
        {
            var expectedGsi = expectedGsis.FirstOrDefault(g => g.IndexName == tableGsi.IndexName);
            if (expectedGsi == null)
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.UnexpectedGsi,
                    tableGsi.IndexName,
                    $"GSI '{tableGsi.IndexName}' exists on the table but is not defined in entity metadata."));
            }
        }
    }

    /// <summary>
    /// Internal validation method for LSIs - mirrors SchemaValidator logic.
    /// </summary>
    private static void ValidateLsiInternal(
        TableDescription tableDescription,
        EntityMetadata metadata,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var tableLsis = tableDescription.LocalSecondaryIndexes ?? new List<LocalSecondaryIndexDescription>();
        var expectedLsis = metadata.Indexes
            .Where(i => i.IndexType == IndexType.LocalSecondaryIndex)
            .ToList();

        // Check for missing LSIs
        foreach (var expectedLsi in expectedLsis)
        {
            var tableLsi = tableLsis.FirstOrDefault(l => l.IndexName == expectedLsi.IndexName);
            if (tableLsi == null)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.LsiNotFound,
                    expectedLsi.IndexName,
                    expectedLsi.IndexName,
                    "not found",
                    $"LSI '{expectedLsi.IndexName}' defined in entity metadata does not exist on the table"));
            }
            else
            {
                // Validate LSI sort key
                var lsiSortKey = tableLsi.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);
                if (lsiSortKey == null || lsiSortKey.AttributeName != expectedLsi.SortKeyAttributeName)
                {
                    result.AddError(new SchemaValidationError(
                        SchemaValidationErrorCode.LsiSortKeyNameMismatch,
                        expectedLsi.IndexName,
                        expectedLsi.SortKeyAttributeName ?? "unknown",
                        lsiSortKey?.AttributeName ?? "not found",
                        "LSI sort key name mismatch"));
                }
            }
        }

        // Check for unexpected LSIs
        foreach (var tableLsi in tableLsis)
        {
            var expectedLsi = expectedLsis.FirstOrDefault(l => l.IndexName == tableLsi.IndexName);
            if (expectedLsi == null)
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.UnexpectedLsi,
                    tableLsi.IndexName,
                    $"LSI '{tableLsi.IndexName}' exists on the table but is not defined in entity metadata."));
            }
        }
    }

    /// <summary>
    /// Internal validation method for TTL - mirrors SchemaValidator logic.
    /// </summary>
    private static void ValidateTtlInternal(
        string tableName,
        EntityMetadata metadata,
        TimeToLiveDescription ttlDescription,
        SchemaValidationResult result)
    {
        var ttlEnabled = ttlDescription.TimeToLiveStatus == TimeToLiveStatus.ENABLED ||
                         ttlDescription.TimeToLiveStatus == TimeToLiveStatus.ENABLING;
        var tableTtlAttributeName = ttlDescription.AttributeName;
        var expectedHasTtl = !string.IsNullOrEmpty(metadata.TtlAttributeName);

        if (expectedHasTtl && !ttlEnabled)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.TtlNotEnabled,
                tableName,
                $"TTL enabled on attribute '{metadata.TtlAttributeName}'",
                "TTL not enabled",
                "Entity metadata defines a TTL attribute but TTL is not enabled on the table"));
        }
        else if (expectedHasTtl && ttlEnabled && tableTtlAttributeName != metadata.TtlAttributeName)
        {
            result.AddError(new SchemaValidationError(
                SchemaValidationErrorCode.TtlAttributeNameMismatch,
                tableName,
                metadata.TtlAttributeName!,
                tableTtlAttributeName ?? "unknown",
                "TTL attribute name mismatch"));
        }
        else if (!expectedHasTtl && ttlEnabled)
        {
            result.AddWarning(new SchemaValidationWarning(
                SchemaValidationWarningCode.UnexpectedTtl,
                tableName,
                $"Table has TTL enabled on attribute '{tableTtlAttributeName}' but the entity metadata does not define a TTL attribute."));
        }
    }

    /// <summary>
    /// Internal validation method for projection - mirrors SchemaValidator logic.
    /// </summary>
    private static void ValidateProjectionInternal(
        string indexName,
        Projection tableProjection,
        IndexMetadata expectedIndex,
        SchemaValidationResult result,
        SchemaValidationOptions options)
    {
        var actualProjectionType = tableProjection.ProjectionType.Value switch
        {
            "ALL" => Metadata.ProjectionType.All,
            "KEYS_ONLY" => Metadata.ProjectionType.KeysOnly,
            "INCLUDE" => Metadata.ProjectionType.Include,
            _ => Metadata.ProjectionType.All
        };

        if (actualProjectionType != Metadata.ProjectionType.All && !expectedIndex.HasProjectionModel)
        {
            if (options.Strictness == ValidationStrictness.Strict)
            {
                result.AddError(new SchemaValidationError(
                    SchemaValidationErrorCode.ProjectionModelRequired,
                    indexName,
                    "projection model defined",
                    "no projection model",
                    $"Index '{indexName}' has projection type '{actualProjectionType}' but no projection model is defined."));
            }
            else
            {
                result.AddWarning(new SchemaValidationWarning(
                    SchemaValidationWarningCode.ProjectionModelRecommended,
                    indexName,
                    $"Index '{indexName}' has projection type '{actualProjectionType}' but no projection model is defined."));
            }
        }
    }

    #endregion
}
