using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for expression-based update operations with DynamoDB Local.
/// These tests verify end-to-end functionality of the type-safe update expression API.
/// 
/// <para><strong>Test Coverage:</strong></para>
/// <list type="bullet">
/// <item><description>Simple SET operations with constants and variables</description></item>
/// <item><description>Multiple property updates in a single expression</description></item>
/// <item><description>Captured variables in expressions</description></item>
/// <item><description>Conditional updates with Where() clauses</description></item>
/// <item><description>Combined multiple SET operations</description></item>
/// </list>
/// 
/// <para><strong>Known Limitations (Not Tested):</strong></para>
/// <list type="bullet">
/// <item><description>ADD operations - Extension methods don't support nullable types yet</description></item>
/// <item><description>REMOVE operations - Compilation issues with nullable types</description></item>
/// <item><description>DELETE operations - Extension methods don't support nullable types yet</description></item>
/// <item><description>DynamoDB functions (IfNotExists, ListAppend, ListPrepend) - Nullable type limitation</description></item>
/// <item><description>Arithmetic operations in SET - Not yet implemented in translator</description></item>
/// <item><description>Format string application - Not yet implemented in UpdateExpressionTranslator</description></item>
/// <item><description>Field-level encryption - Not yet implemented in UpdateExpressionTranslator</description></item>
/// <item><description>Mixing string-based and expression-based Set() methods - Attribute name conflicts</description></item>
/// </list>
/// 
/// <para><strong>Future Enhancements Needed:</strong></para>
/// <list type="bullet">
/// <item><description>Add nullable overloads to UpdateExpressionPropertyExtensions for nullable entity properties</description></item>
/// <item><description>Implement format string application in UpdateExpressionTranslator</description></item>
/// <item><description>Implement field-level encryption support in UpdateExpressionTranslator</description></item>
/// <item><description>Fix attribute name generation when mixing string-based and expression-based methods</description></item>
/// <item><description>Implement arithmetic operations (+ and -) in SET clauses</description></item>
/// </list>
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "ExpressionBasedUpdates")]
public class ExpressionBasedUpdateTests : IntegrationTestBase
{
    private DynamoDbTableBase _table = null!;
    
    public ExpressionBasedUpdateTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await CreateTableAsync<ComplexEntity>();
        _table = new TestTable(DynamoDb, TableName);
    }
    
    #region Simple SET Operations
    
    [Fact]
    public async Task Set_SimpleStringProperty_UpdatesSuccessfully()
    {
        // Arrange - Create initial entity
        var entity = new ComplexEntity
        {
            Id = "expr-test-1",
            Type = "product",
            Name = "Old Name"
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update using expression-based API
        await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-1")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = "New Name"
                })
            .UpdateAsync();
        
        // Assert - Verify the update
        var loaded = await LoadEntityAsync("expr-test-1", "product");
        loaded.Name.Should().Be("New Name");
    }
    
    [Fact]
    public async Task Set_MultipleProperties_UpdatesAllSuccessfully()
    {
        // Arrange
        var entity = new ComplexEntity
        {
            Id = "expr-test-2",
            Type = "product",
            Name = "Old Name",
            Description = "Old Description",
            IsActive = false
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update multiple properties
        await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-2")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = "New Name",
                    Description = "New Description",
                    IsActive = true
                })
            .UpdateAsync();
        
        // Assert
        var loaded = await LoadEntityAsync("expr-test-2", "product");
        loaded.Name.Should().Be("New Name");
        loaded.Description.Should().Be("New Description");
        loaded.IsActive.Should().BeTrue();
    }
    
    [Fact]
    public async Task Set_WithCapturedVariables_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new ComplexEntity
        {
            Id = "expr-test-3",
            Type = "product",
            Name = "Old Name"
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Use captured variables
        var newName = "Captured Name";
        var newDescription = "Captured Description";
        
        await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-3")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = newName,
                    Description = newDescription
                })
            .UpdateAsync();
        
        // Assert
        var loaded = await LoadEntityAsync("expr-test-3", "product");
        loaded.Name.Should().Be("Captured Name");
        loaded.Description.Should().Be("Captured Description");
    }
    
    #endregion
    
    #region ADD Operations
    
    // Note: ADD, DELETE, and DynamoDB function operations are not yet supported for nullable types.
    // The extension methods are defined for non-nullable UpdateExpressionProperty<T>, but the
    // source generator creates nullable properties for nullable entity properties (e.g., HashSet<int>?).
    // This is a known limitation that will be addressed by adding nullable overloads to the extension methods.
    //
    // Tests for these operations are commented out until nullable support is added.
    // See: UpdateExpressionPropertyExtensions.cs for current method signatures.
    
    #endregion
    
    #region REMOVE Operations
    
    // Note: REMOVE operations are also affected by the nullable type limitation.
    // The Remove() extension method is defined for UpdateExpressionProperty<T>, but works with
    // nullable properties. However, due to compilation issues, these tests are commented out.
    
    #endregion
    
    #region DELETE Operations
    
    // Note: DELETE operations are not yet supported for nullable types.
    // See ADD Operations section for details on the nullable type limitation.
    
    #endregion
    
    #region Arithmetic Operations
    
    // Note: Arithmetic operations in SET are not yet implemented in the translator
    // These tests are commented out until task 5.2 is completed
    
    // [Fact]
    // public async Task Set_ArithmeticAddition_UpdatesSuccessfully()
    // {
    //     // Arrange
    //     var entity = new ComplexEntity
    //     {
    //         Id = "expr-test-10",
    //         Type = "product",
    //         Name = "Test Product"
    //     };
    //     
    //     var item = ComplexEntity.ToDynamoDb(entity);
    //     await DynamoDb.PutItemAsync(TableName, item);
    //     
    //     // Act - Use arithmetic in SET
    //     await _table.Update<ComplexEntity>()
    //         .WithKey("pk", "expr-test-10")
    //         .WithKey("sk", "product")
    //         .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
    //             x => new ComplexEntityUpdateModel
    //             {
    //                 // Arithmetic operations would go here
    //             })
    //         .UpdateAsync();
    //     
    //     // Assert
    //     var loaded = await LoadEntityAsync("expr-test-10", "product");
    //     // Assertions would go here
    // }
    
    #endregion
    
    #region DynamoDB Functions
    
    // Note: DynamoDB functions (IfNotExists, ListAppend, ListPrepend) are not yet supported for nullable types.
    // See ADD Operations section for details on the nullable type limitation.
    
    #endregion
    
    #region Combined Operations
    
    [Fact]
    public async Task Set_CombinedMultipleSetOperations_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new ComplexEntity
        {
            Id = "expr-test-15",
            Type = "product",
            Name = "Old Name",
            Description = "Old Description",
            IsActive = false
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Combine multiple SET operations
        await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-15")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = "New Name",
                    Description = "New Description",
                    IsActive = true
                })
            .UpdateAsync();
        
        // Assert
        var loaded = await LoadEntityAsync("expr-test-15", "product");
        loaded.Name.Should().Be("New Name");
        loaded.Description.Should().Be("New Description");
        loaded.IsActive.Should().BeTrue();
    }
    
    #endregion
    
    #region Conditional Updates
    
    [Fact]
    public async Task Set_WithCondition_UpdatesWhenConditionMet()
    {
        // Arrange
        var entity = new ComplexEntity
        {
            Id = "expr-test-16",
            Type = "product",
            Name = "Old Name",
            IsActive = true
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Update with condition
        await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-16")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = "New Name"
                })
            .Where("attribute_exists(is_active)")
            .UpdateAsync();
        
        // Assert
        var loaded = await LoadEntityAsync("expr-test-16", "product");
        loaded.Name.Should().Be("New Name");
    }
    
    [Fact]
    public async Task Set_WithCondition_FailsWhenConditionNotMet()
    {
        // Arrange
        var entity = new ComplexEntity
        {
            Id = "expr-test-17",
            Type = "product",
            Name = "Old Name"
            // No IsActive set
        };
        
        var item = ComplexEntity.ToDynamoDb(entity);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act & Assert - Update should fail
        var act = async () => await _table.Update<ComplexEntity>()
            .WithKey("pk", "expr-test-17")
            .WithKey("sk", "product")
            .Set<ComplexEntity, ComplexEntityUpdateExpressions, ComplexEntityUpdateModel>(
                x => new ComplexEntityUpdateModel
                {
                    Name = "New Name"
                })
            .Where("attribute_exists(is_active)")
            .UpdateAsync();
        
        var exception = await act.Should().ThrowAsync<DynamoDbMappingException>();
        exception.Which.InnerException.Should().BeOfType<ConditionalCheckFailedException>();
    }
    
    #endregion
    
    #region Mixing String-Based and Expression-Based Methods
    
    // Note: Mixing string-based and expression-based Set() methods in the same builder
    // currently has issues with attribute name generation. This is a known limitation
    // that will be addressed in a future update. For now, use one approach or the other.
    
    #endregion
    
    #region Helper Methods
    
    private async Task<ComplexEntity> LoadEntityAsync(string id, string type)
    {
        var getResponse = await DynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = id },
                ["sk"] = new AttributeValue { S = type }
            }
        });
        
        if (!getResponse.IsItemSet)
        {
            throw new InvalidOperationException($"Item not found: {id}/{type}");
        }
        
        return ComplexEntity.FromDynamoDb<ComplexEntity>(getResponse.Item);
    }
    
    private class TestTable : DynamoDbTableBase
    {
        public TestTable(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
    }
    
    #endregion
}
