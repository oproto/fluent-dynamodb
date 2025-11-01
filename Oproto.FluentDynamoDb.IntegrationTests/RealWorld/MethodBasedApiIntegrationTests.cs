using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for the method-based builder API.
/// Tests complete workflows with Query(expression, params), Get/Update/Delete with key parameters,
/// and index queries with various configurations.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "MethodBasedAPI")]
public class MethodBasedApiIntegrationTests : IntegrationTestBase
{
    private TestTableWithSingleKey _singleKeyTable = null!;
    private TestTableWithCompositeKey _compositeKeyTable = null!;
    
    public MethodBasedApiIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Create table for single key tests
        await CreateTableAsync<UserEntity>();
        _singleKeyTable = new TestTableWithSingleKey(DynamoDb, TableName);
        
        // Create table for composite key tests (reuse ComplexEntity)
        var compositeTableName = $"test_composite_{Guid.NewGuid():N}";
        await CreateTableForCompositeKeyAsync(compositeTableName);
        _compositeKeyTable = new TestTableWithCompositeKey(DynamoDb, compositeTableName);
    }
    
    private async Task CreateTableForCompositeKeyAsync(string tableName)
    {
        var request = new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "pk", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "sk", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "sk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "gsi1pk", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "gsi1sk", AttributeType = ScalarAttributeType.S }
            },
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new GlobalSecondaryIndex
                {
                    IndexName = "StatusIndex",
                    KeySchema = new List<KeySchemaElement>
                    {
                        new KeySchemaElement { AttributeName = "gsi1pk", KeyType = KeyType.HASH },
                        new KeySchemaElement { AttributeName = "gsi1sk", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        };
        
        await DynamoDb.CreateTableAsync(request);
        await WaitForTableActiveAsync(tableName);
    }
    
    #region Query with Format String Expression Tests
    
    [Fact]
    public async Task Query_WithFormatStringExpression_SimplePartitionKey_ReturnsMatchingItems()
    {
        // Arrange
        var userId = "USER#123";
        await _singleKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = "METADATA" },
                ["name"] = new AttributeValue { S = "John Doe" },
                ["email"] = new AttributeValue { S = "john@example.com" }
            })
            .PutAsync();
        
        // Act - Use Query with format string expression
        var response = await _singleKeyTable.Query("pk = {0}", userId).PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0]["pk"].S.Should().Be(userId);
        response.Items[0]["name"].S.Should().Be("John Doe");
    }
    
    [Fact]
    public async Task Query_WithFormatStringExpression_CompositeKey_ReturnsMatchingItems()
    {
        // Arrange
        var pk = "PRODUCT#456";
        var sk = "METADATA";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Laptop" },
                ["price"] = new AttributeValue { N = "999.99" }
            })
            .PutAsync();
        
        // Act - Use Query with composite key condition
        var response = await _compositeKeyTable.Query("pk = {0} AND sk = {1}", pk, sk).PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0]["pk"].S.Should().Be(pk);
        response.Items[0]["sk"].S.Should().Be(sk);
        response.Items[0]["name"].S.Should().Be("Laptop");
    }
    
    [Fact]
    public async Task Query_WithFormatStringExpression_BeginsWithOperator_ReturnsMatchingItems()
    {
        // Arrange
        var pk = "USER#789";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "ORDER#001" },
                ["amount"] = new AttributeValue { N = "100" }
            })
            .PutAsync();
        
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "ORDER#002" },
                ["amount"] = new AttributeValue { N = "200" }
            })
            .PutAsync();
        
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "PROFILE" },
                ["name"] = new AttributeValue { S = "Jane" }
            })
            .PutAsync();
        
        // Act - Use begins_with in format string
        var response = await _compositeKeyTable.Query("pk = {0} AND begins_with(sk, {1})", pk, "ORDER#").PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => item["sk"].S.Should().StartWith("ORDER#"));
    }
    
    [Fact]
    public async Task Query_WithFormatStringExpression_ComparisonOperators_ReturnsMatchingItems()
    {
        // Arrange
        var pk = "PRODUCT#SERIES";
        var items = new[]
        {
            ("VERSION#1.0", "100"),
            ("VERSION#2.0", "200"),
            ("VERSION#3.0", "300"),
            ("VERSION#4.0", "400")
        };
        
        foreach (var (sk, price) in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = sk },
                    ["price"] = new AttributeValue { N = price }
                })
                .PutAsync();
        }
        
        // Act - Use >= operator
        var response = await _compositeKeyTable.Query("pk = {0} AND sk >= {1}", pk, "VERSION#2.0").PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(3);
        response.Items.Should().Contain(item => item["sk"].S == "VERSION#2.0");
        response.Items.Should().Contain(item => item["sk"].S == "VERSION#3.0");
        response.Items.Should().Contain(item => item["sk"].S == "VERSION#4.0");
    }
    
    [Fact]
    public async Task Query_WithFormatStringExpression_BetweenOperator_ReturnsMatchingItems()
    {
        // Arrange
        var pk = "EVENTS#2024";
        var dates = new[] { "2024-01-15", "2024-02-20", "2024-03-10", "2024-04-05" };
        
        foreach (var date in dates)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = date },
                    ["event"] = new AttributeValue { S = $"Event on {date}" }
                })
                .PutAsync();
        }
        
        // Act - Use BETWEEN operator
        var response = await _compositeKeyTable.Query("pk = {0} AND sk BETWEEN {1} AND {2}", 
            pk, "2024-02-01", "2024-03-31").PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(item => item["sk"].S == "2024-02-20");
        response.Items.Should().Contain(item => item["sk"].S == "2024-03-10");
    }
    
    #endregion
    
    #region Get/Update/Delete with Key Parameters Tests
    
    [Fact]
    public async Task Get_WithSingleKeyParameter_RetrievesItem()
    {
        // Arrange
        var userId = "USER#GET#001";
        await _singleKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = "METADATA" },
                ["name"] = new AttributeValue { S = "Alice" },
                ["email"] = new AttributeValue { S = "alice@example.com" }
            })
            .PutAsync();
        
        // Act - Use Get with key parameter
        var response = await _singleKeyTable.Get(userId).PutAsync();
        
        // Assert
        response.Item.Should().NotBeNull();
        response.Item["pk"].S.Should().Be(userId);
        response.Item["name"].S.Should().Be("Alice");
    }
    
    [Fact]
    public async Task Get_WithCompositeKeyParameters_RetrievesItem()
    {
        // Arrange
        var pk = "PRODUCT#GET#001";
        var sk = "DETAILS";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Widget" },
                ["price"] = new AttributeValue { N = "49.99" }
            })
            .PutAsync();
        
        // Act - Use Get with composite key parameters
        var response = await _compositeKeyTable.Get(pk, sk).PutAsync();
        
        // Assert
        response.Item.Should().NotBeNull();
        response.Item["pk"].S.Should().Be(pk);
        response.Item["sk"].S.Should().Be(sk);
        response.Item["name"].S.Should().Be("Widget");
    }
    
    [Fact]
    public async Task Update_WithSingleKeyParameter_UpdatesItem()
    {
        // Arrange
        var userId = "USER#UPDATE#001";
        await _singleKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = "METADATA" },
                ["name"] = new AttributeValue { S = "Bob" },
                ["email"] = new AttributeValue { S = "bob@example.com" }
            })
            .PutAsync();
        
        // Act - Use Update with key parameter
        await _singleKeyTable.Update(userId)
            .Set("SET #name = :newName")
            .WithAttribute("#name", "name")
            .WithValue(":newName", "Robert")
            .PutAsync();
        
        // Assert
        var response = await _singleKeyTable.Get(userId).PutAsync();
        response.Item["name"].S.Should().Be("Robert");
    }
    
    [Fact]
    public async Task Update_WithCompositeKeyParameters_UpdatesItem()
    {
        // Arrange
        var pk = "PRODUCT#UPDATE#001";
        var sk = "DETAILS";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Gadget" },
                ["price"] = new AttributeValue { N = "99.99" }
            })
            .PutAsync();
        
        // Act - Use Update with composite key parameters
        await _compositeKeyTable.Update(pk, sk)
            .Set("SET price = :newPrice")
            .WithValue(":newPrice", 79.99m)
            .PutAsync();
        
        // Assert
        var response = await _compositeKeyTable.Get(pk, sk).PutAsync();
        response.Item["price"].N.Should().Be("79.99");
    }
    
    [Fact]
    public async Task Delete_WithSingleKeyParameter_DeletesItem()
    {
        // Arrange
        var userId = "USER#DELETE#001";
        await _singleKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = userId },
                ["sk"] = new AttributeValue { S = "METADATA" },
                ["name"] = new AttributeValue { S = "Charlie" }
            })
            .PutAsync();
        
        // Act - Use Delete with key parameter
        await _singleKeyTable.Delete(userId).PutAsync();
        
        // Assert
        var response = await _singleKeyTable.Get(userId).PutAsync();
        response.Item.Should().BeNull();
    }
    
    [Fact]
    public async Task Delete_WithCompositeKeyParameters_DeletesItem()
    {
        // Arrange
        var pk = "PRODUCT#DELETE#001";
        var sk = "DETAILS";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["name"] = new AttributeValue { S = "Doohickey" }
            })
            .PutAsync();
        
        // Act - Use Delete with composite key parameters
        await _compositeKeyTable.Delete(pk, sk).PutAsync();
        
        // Assert
        var response = await _compositeKeyTable.Get(pk, sk).PutAsync();
        response.Item.Should().BeNull();
    }
    
    #endregion
    
    #region Index Query Tests
    
    [Fact]
    public async Task IndexQuery_WithFormatStringExpression_ReturnsMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            ("ITEM#001", "ACTIVE", "2024-01-15", "Item 1"),
            ("ITEM#002", "ACTIVE", "2024-02-20", "Item 2"),
            ("ITEM#003", "INACTIVE", "2024-03-10", "Item 3")
        };
        
        foreach (var (pk, status, date, name) in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = "METADATA" },
                    ["gsi1pk"] = new AttributeValue { S = $"STATUS#{status}" },
                    ["gsi1sk"] = new AttributeValue { S = date },
                    ["name"] = new AttributeValue { S = name }
                })
                .PutAsync();
        }
        
        // Act - Query index with format string
        var response = await _compositeKeyTable.StatusIndex
            .Query("gsi1pk = {0}", "STATUS#ACTIVE")
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => 
            item["gsi1pk"].S.Should().Be("STATUS#ACTIVE"));
    }
    
    [Fact]
    public async Task IndexQuery_WithCompositeKeyCondition_ReturnsMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            ("ITEM#101", "ACTIVE", "2024-01-15"),
            ("ITEM#102", "ACTIVE", "2024-02-20"),
            ("ITEM#103", "ACTIVE", "2024-03-10")
        };
        
        foreach (var (pk, status, date) in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = "METADATA" },
                    ["gsi1pk"] = new AttributeValue { S = $"STATUS#{status}" },
                    ["gsi1sk"] = new AttributeValue { S = date }
                })
                .PutAsync();
        }
        
        // Act - Query index with composite key condition
        var response = await _compositeKeyTable.StatusIndex
            .Query("gsi1pk = {0} AND gsi1sk >= {1}", "STATUS#ACTIVE", "2024-02-01")
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(item => item["gsi1sk"].S == "2024-02-20");
        response.Items.Should().Contain(item => item["gsi1sk"].S == "2024-03-10");
    }
    
    [Fact]
    public async Task IndexQuery_WithBeginsWithOperator_ReturnsMatchingItems()
    {
        // Arrange
        var items = new[]
        {
            ("ITEM#201", "CATEGORY#ELECTRONICS", "PRODUCT#LAPTOP"),
            ("ITEM#202", "CATEGORY#ELECTRONICS", "PRODUCT#PHONE"),
            ("ITEM#203", "CATEGORY#ELECTRONICS", "SERVICE#WARRANTY")
        };
        
        foreach (var (pk, gsi1pk, gsi1sk) in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = "METADATA" },
                    ["gsi1pk"] = new AttributeValue { S = gsi1pk },
                    ["gsi1sk"] = new AttributeValue { S = gsi1sk }
                })
                .PutAsync();
        }
        
        // Act - Query index with begins_with
        var response = await _compositeKeyTable.StatusIndex
            .Query("gsi1pk = {0} AND begins_with(gsi1sk, {1})", "CATEGORY#ELECTRONICS", "PRODUCT#")
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => 
            item["gsi1sk"].S.Should().StartWith("PRODUCT#"));
    }
    
    #endregion
    
    #region Format String Integration with Fluent Methods Tests
    
    [Fact]
    public async Task Query_FormatStringWithProjection_ReturnsOnlyRequestedAttributes()
    {
        // Arrange
        var pk = "USER#PROJ#001";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = "METADATA" },
                ["name"] = new AttributeValue { S = "David" },
                ["email"] = new AttributeValue { S = "david@example.com" },
                ["phone"] = new AttributeValue { S = "555-1234" },
                ["address"] = new AttributeValue { S = "123 Main St" }
            })
            .PutAsync();
        
        // Act - Combine format string with projection
        var response = await _compositeKeyTable
            .Query("pk = {0}", pk)
            .WithProjection("pk, sk, #name, email")
            .WithAttribute("#name", "name")
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(1);
        var item = response.Items[0];
        item.Should().ContainKey("pk");
        item.Should().ContainKey("sk");
        item.Should().ContainKey("name");
        item.Should().ContainKey("email");
        item.Should().NotContainKey("phone");
        item.Should().NotContainKey("address");
    }
    
    [Fact]
    public async Task Query_FormatStringWithFilter_FiltersResults()
    {
        // Arrange
        var pk = "PRODUCT#FILTER#001";
        var items = new[]
        {
            ("VERSION#1.0", "100", "true"),
            ("VERSION#2.0", "200", "false"),
            ("VERSION#3.0", "300", "true")
        };
        
        foreach (var (sk, price, active) in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = sk },
                    ["price"] = new AttributeValue { N = price },
                    ["active"] = new AttributeValue { BOOL = bool.Parse(active) }
                })
                .PutAsync();
        }
        
        // Act - Combine format string with filter
        var response = await _compositeKeyTable
            .Query("pk = {0}", pk)
            .WithFilter("active = :active")
            .WithValue(":active", true)
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Should().AllSatisfy(item => 
            item["active"].BOOL.Should().BeTrue());
    }
    
    [Fact]
    public async Task Query_FormatStringWithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var pk = "PRODUCT#LIMIT#001";
        for (int i = 1; i <= 10; i++)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = $"ITEM#{i:D3}" },
                    ["name"] = new AttributeValue { S = $"Item {i}" }
                })
                .PutAsync();
        }
        
        // Act - Combine format string with limit
        var response = await _compositeKeyTable
            .Query("pk = {0}", pk)
            .Take(5)
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(5);
        response.LastEvaluatedKey.Should().NotBeNull();
    }
    
    [Fact]
    public async Task Query_FormatStringWithOrderDescending_ReturnsReversedResults()
    {
        // Arrange
        var pk = "PRODUCT#ORDER#001";
        var items = new[] { "A", "B", "C", "D", "E" };
        
        foreach (var letter in items)
        {
            await _compositeKeyTable.Put()
                .WithItem(new Dictionary<string, AttributeValue>
                {
                    ["pk"] = new AttributeValue { S = pk },
                    ["sk"] = new AttributeValue { S = $"ITEM#{letter}" },
                    ["name"] = new AttributeValue { S = $"Item {letter}" }
                })
                .PutAsync();
        }
        
        // Act - Combine format string with descending order
        var response = await _compositeKeyTable
            .Query("pk = {0}", pk)
            .OrderDescending()
            .PutAsync();
        
        // Assert
        response.Items.Should().HaveCount(5);
        response.Items[0]["sk"].S.Should().Be("ITEM#E");
        response.Items[4]["sk"].S.Should().Be("ITEM#A");
    }
    
    [Fact]
    public async Task Update_WithKeyParametersAndCondition_UpdatesConditionally()
    {
        // Arrange
        var pk = "PRODUCT#COND#001";
        var sk = "DETAILS";
        await _compositeKeyTable.Put()
            .WithItem(new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = pk },
                ["sk"] = new AttributeValue { S = sk },
                ["price"] = new AttributeValue { N = "100" },
                ["stock"] = new AttributeValue { N = "50" }
            })
            .PutAsync();
        
        // Act - Update with key parameters and condition
        await _compositeKeyTable.Update(pk, sk)
            .Set("SET price = :newPrice")
            .Where("stock > :minStock")
            .WithValue(":newPrice", 90m)
            .WithValue(":minStock", 10)
            .PutAsync();
        
        // Assert
        var response = await _compositeKeyTable.Get(pk, sk).PutAsync();
        response.Item["price"].N.Should().Be("90");
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Test table with single partition key (no sort key).
    /// Demonstrates manual table implementation with key-specific overloads.
    /// </summary>
    private class TestTableWithSingleKey : DynamoDbTableBase
    {
        public TestTableWithSingleKey(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        // Override Get to provide key-specific overload
        public GetItemRequestBuilder Get(string userId) => 
            base.Get().WithKey("pk", userId).WithKey("sk", "METADATA");
        
        // Override Update to provide key-specific overload
        public UpdateItemRequestBuilder Update(string userId) => 
            base.Update().WithKey("pk", userId).WithKey("sk", "METADATA");
        
        // Override Delete to provide key-specific overload
        public DeleteItemRequestBuilder Delete(string userId) => 
            base.Delete().WithKey("pk", userId).WithKey("sk", "METADATA");
    }
    
    /// <summary>
    /// Test table with composite key (partition key + sort key).
    /// Demonstrates manual table implementation with composite key overloads.
    /// </summary>
    private class TestTableWithCompositeKey : DynamoDbTableBase
    {
        public TestTableWithCompositeKey(IAmazonDynamoDB client, string tableName) 
            : base(client, tableName)
        {
        }
        
        // Override Get to provide composite key overload
        public GetItemRequestBuilder Get(string pk, string sk) => 
            base.Get().WithKey("pk", pk).WithKey("sk", sk);
        
        // Override Update to provide composite key overload
        public UpdateItemRequestBuilder Update(string pk, string sk) => 
            base.Update().WithKey("pk", pk).WithKey("sk", sk);
        
        // Override Delete to provide composite key overload
        public DeleteItemRequestBuilder Delete(string pk, string sk) => 
            base.Delete().WithKey("pk", pk).WithKey("sk", sk);
        
        // Define index with projection
        public DynamoDbIndex StatusIndex => 
            new DynamoDbIndex(this, "StatusIndex", "pk, sk, gsi1pk, gsi1sk, name");
    }
    
    #endregion
}
