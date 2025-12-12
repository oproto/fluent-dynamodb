using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Validation;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for schema validation functionality.
/// Tests verify that ValidateSchemaAsync correctly validates DynamoDB table schemas
/// against entity metadata using DynamoDB Local.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "SchemaValidation")]
public class SchemaValidationIntegrationTests : IntegrationTestBase
{
    private readonly List<string> _additionalTablesToCleanup = new();
    
    public SchemaValidationIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task DisposeAsync()
    {
        // Clean up additional tables created during tests
        foreach (var tableName in _additionalTablesToCleanup)
        {
            try
            {
                await DynamoDb.DeleteTableAsync(tableName);
            }
            catch (ResourceNotFoundException)
            {
                // Table already deleted
            }
        }
        
        await base.DisposeAsync();
    }
    
    /// <summary>
    /// Creates a DynamoDB table that matches the SchemaValidationTestEntity metadata exactly.
    /// Includes GSI, LSI, and TTL configuration.
    /// </summary>
    private async Task CreateMatchingTableAsync()
    {
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "status", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "created_at", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "category", AttributeType = ScalarAttributeType.S }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "StatusIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "status", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "created_at", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            },
            LocalSecondaryIndexes = new List<LocalSecondaryIndex>
            {
                new LocalSecondaryIndex
                {
                    IndexName = "CategoryIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "category", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            }
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(TableName);
        
        // Enable TTL
        await DynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
        {
            TableName = TableName,
            TimeToLiveSpecification = new TimeToLiveSpecification
            {
                Enabled = true,
                AttributeName = "ttl"
            }
        });
    }

    /// <summary>
    /// Creates a table with mismatched primary key configuration.
    /// </summary>
    private async Task CreateTableWithMismatchedPrimaryKeyAsync()
    {
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "wrong_pk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "wrong_sk", AttributeType = ScalarAttributeType.S }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "wrong_pk", KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = "wrong_sk", KeyType = KeyType.RANGE }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(TableName);
    }
    
    /// <summary>
    /// Creates a table without the expected GSI.
    /// </summary>
    private async Task CreateTableWithoutGsiAsync()
    {
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "category", AttributeType = ScalarAttributeType.S }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            // Include LSI but not GSI
            LocalSecondaryIndexes = new List<LocalSecondaryIndex>
            {
                new LocalSecondaryIndex
                {
                    IndexName = "CategoryIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "category", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            }
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(TableName);
    }
    
    /// <summary>
    /// Creates a table without the expected LSI.
    /// </summary>
    private async Task CreateTableWithoutLsiAsync()
    {
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "status", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "created_at", AttributeType = ScalarAttributeType.S }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            // Include GSI but not LSI
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "StatusIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "status", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "created_at", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            }
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableAndGsiActiveAsync(TableName, "StatusIndex");
    }

    /// <summary>
    /// Creates a table with extra indexes not defined in entity metadata.
    /// </summary>
    private async Task CreateTableWithExtraIndexesAsync()
    {
        var attributeDefinitions = new List<AttributeDefinition>
        {
            new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "status", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "created_at", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "category", AttributeType = ScalarAttributeType.S },
            new AttributeDefinition { AttributeName = "extra_attr", AttributeType = ScalarAttributeType.S }
        };
        
        var keySchema = new List<KeySchemaElement>
        {
            new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
            new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
        };
        
        var request = new CreateTableRequest
        {
            TableName = TableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "StatusIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "status", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "created_at", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                },
                // Extra GSI not in entity metadata
                new GlobalSecondaryIndex
                {
                    IndexName = "ExtraGsiIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "extra_attr", KeyType = KeyType.HASH }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            },
            LocalSecondaryIndexes = new List<LocalSecondaryIndex>
            {
                new LocalSecondaryIndex
                {
                    IndexName = "CategoryIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "category", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                },
                // Extra LSI not in entity metadata
                new LocalSecondaryIndex
                {
                    IndexName = "ExtraLsiIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "extra_attr", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = Amazon.DynamoDBv2.ProjectionType.ALL }
                }
            }
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableAndGsiActiveAsync(TableName, "StatusIndex");
        await WaitForTableAndGsiActiveAsync(TableName, "ExtraGsiIndex");
        
        // Enable TTL
        await DynamoDb.UpdateTimeToLiveAsync(new UpdateTimeToLiveRequest
        {
            TableName = TableName,
            TimeToLiveSpecification = new TimeToLiveSpecification
            {
                Enabled = true,
                AttributeName = "ttl"
            }
        });
    }
    
    #region Test Methods
    
    /// <summary>
    /// Tests that ValidateSchemaAsync returns IsValid=true when the table schema matches entity metadata.
    /// Requirements: 1.1, 1.3
    /// </summary>
    [Fact]
    public async Task ValidateSchemaAsync_WithMatchingSchema_ReturnsIsValidTrue()
    {
        // Arrange
        await CreateMatchingTableAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Assert
        result.IsValid.Should().BeTrue("schema matches entity metadata");
        result.Errors.Should().BeEmpty("no mismatches should be detected");
    }

    /// <summary>
    /// Tests that ValidateSchemaAsync detects missing GSI.
    /// Requirements: 3.1
    /// </summary>
    [Fact]
    public async Task ValidateSchemaAsync_WithMissingGsi_ReturnsError()
    {
        // Arrange
        await CreateTableWithoutGsiAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Assert
        result.IsValid.Should().BeFalse("GSI is missing from table");
        result.Errors.Should().Contain(e => 
            e.Code == SchemaValidationErrorCode.GsiNotFound &&
            e.Element == "StatusIndex");
    }
    
    /// <summary>
    /// Tests that ValidateSchemaAsync detects missing LSI.
    /// Requirements: 4.2
    /// </summary>
    [Fact]
    public async Task ValidateSchemaAsync_WithMissingLsi_ReturnsError()
    {
        // Arrange
        await CreateTableWithoutLsiAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Assert
        result.IsValid.Should().BeFalse("LSI is missing from table");
        result.Errors.Should().Contain(e => 
            e.Code == SchemaValidationErrorCode.LsiNotFound &&
            e.Element == "CategoryIndex");
    }
    
    /// <summary>
    /// Tests that ValidateSchemaAsync detects primary key mismatches.
    /// Requirements: 2.1
    /// </summary>
    [Fact]
    public async Task ValidateSchemaAsync_WithPrimaryKeyMismatch_ReturnsError()
    {
        // Arrange
        await CreateTableWithMismatchedPrimaryKeyAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Assert
        result.IsValid.Should().BeFalse("primary key does not match");
        result.Errors.Should().Contain(e => 
            e.Code == SchemaValidationErrorCode.PartitionKeyNameMismatch);
    }
    
    /// <summary>
    /// Tests that ValidateSchemaAsync produces warnings for unexpected indexes.
    /// Requirements: 1.4
    /// </summary>
    [Fact]
    public async Task ValidateSchemaAsync_WithUnexpectedIndexes_ReturnsWarnings()
    {
        // Arrange
        await CreateTableWithExtraIndexesAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        
        // Act
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Assert
        result.IsValid.Should().BeTrue("extra indexes are warnings, not errors");
        result.Warnings.Should().Contain(w => 
            w.Code == SchemaValidationWarningCode.UnexpectedGsi &&
            w.Element == "ExtraGsiIndex");
        result.Warnings.Should().Contain(w => 
            w.Code == SchemaValidationWarningCode.UnexpectedLsi &&
            w.Element == "ExtraLsiIndex");
    }
    
    /// <summary>
    /// Tests that ThrowOnError throws SchemaValidationException when errors exist.
    /// Requirements: 9.2, 9.3
    /// </summary>
    [Fact]
    public async Task ThrowOnError_WithErrors_ThrowsSchemaValidationException()
    {
        // Arrange
        await CreateTableWithMismatchedPrimaryKeyAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Act & Assert
        var act = () => result.ThrowOnError();
        act.Should().Throw<SchemaValidationException>()
            .Which.ValidationResult.Should().BeSameAs(result);
    }
    
    /// <summary>
    /// Tests that ThrowOnError does not throw when validation passes.
    /// Requirements: 9.2
    /// </summary>
    [Fact]
    public async Task ThrowOnError_WithNoErrors_DoesNotThrow()
    {
        // Arrange
        await CreateMatchingTableAsync();
        var validator = new SchemaValidator();
        var metadata = SchemaValidationTestEntity.GetEntityMetadata();
        var result = await validator.ValidateAsync(DynamoDb, TableName, metadata);
        
        // Act & Assert
        var act = () => result.ThrowOnError();
        act.Should().NotThrow();
    }
    
    #endregion
}
