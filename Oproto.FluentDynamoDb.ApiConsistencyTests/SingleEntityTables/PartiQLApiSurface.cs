using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class PartiQLApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task ExecutePartiQL_TypedEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === SELECT with typed entity ===
        // ToListAsync terminal
        var results = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = ?", "1234")
            .ToListAsync();
        
        // With format string placeholders
        results = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = {0}", "1234")
            .ToListAsync();
        
        // Multiple parameters
        results = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = {0} AND age > {1}", "1234", 21)
            .ToListAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ExecutePartiQL_DynamicEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === SELECT with DynamicEntity (no type parameter) ===
        var results = await table.ExecutePartiQL(
            "SELECT * FROM basicPk WHERE pk = {0}", "1234")
            .ToListAsync();
        
        // Access DynamicEntity fields using typed accessors
        foreach (var item in results)
        {
            var pk = item.DynamicFields.GetString("pk");
            var name = item.DynamicFields.GetString("name");
            var age = item.DynamicFields.GetInt("age");
        }
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task ExecutePartiQL_NonSelectStatements_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === INSERT statement ===
        await table.ExecutePartiQL<BasicPkEntity>(
            "INSERT INTO basicPk VALUE {'pk': {0}, 'name': {1}, 'age': {2}}", 
            "1234", "Test", 25)
            .ExecuteAsync();
        
        // === UPDATE statement ===
        await table.ExecutePartiQL<BasicPkEntity>(
            "UPDATE basicPk SET name = {0} WHERE pk = {1}", 
            "NewName", "1234")
            .ExecuteAsync();
        
        // === DELETE statement ===
        await table.ExecutePartiQL<BasicPkEntity>(
            "DELETE FROM basicPk WHERE pk = {0}", "1234")
            .ExecuteAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ExecutePartiQL_CompoundEntity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ToCompoundEntityAsync for multi-entity tables ===
        var result = await table.ExecutePartiQL<BasicPkSkEntity>(
            "SELECT * FROM basicPkSk WHERE pk = {0}", "1234")
            .ToCompoundEntityAsync();
        
        // Access entities by type from compound result
        var entities = result.GetEntities<BasicPkSkEntity>();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task ExecutePartiQL_ResponseMetadata_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Access response metadata after execution ===
        var builder = table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = {0}", "1234");
        
        var results = await builder.ToListAsync();
        
        // Access metadata from builder after execution
        var responseMetadata = builder.ResponseMetadata;
        var consumedCapacity = builder.ConsumedCapacity;
        var nextToken = builder.NextToken;
    }
}
