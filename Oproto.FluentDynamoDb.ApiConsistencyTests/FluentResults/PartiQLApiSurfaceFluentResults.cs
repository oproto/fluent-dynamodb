using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults PartiQL operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 10.1, 10.2
/// </summary>
public class PartiQLApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task ToListAsyncResult_PartiQL_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ToListAsyncResult for SELECT queries ===
        var result = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = ?", "1234")
            .ToListAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entities = result.Value;
            foreach (var entity in entities)
            {
                var name = entity.Name;
            }
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }

        // === Multiple parameters ===
        result = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = ? AND age > ?", "1234", 21)
            .ToListAsyncResult();

        // === With projection ===
        result = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT pk, name FROM basicPk WHERE pk = ?", "1234")
            .ToListAsyncResult();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task ExecuteAsyncResult_PartiQL_NonSelect_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsyncResult for INSERT ===
        var result = await table.ExecutePartiQL<BasicPkEntity>(
            "INSERT INTO basicPk VALUE {'pk': ?, 'name': ?, 'age': ?}", "1234", "John", 25)
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Insert succeeded
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }

        // === ExecuteAsyncResult for UPDATE ===
        result = await table.ExecutePartiQL<BasicPkEntity>(
            "UPDATE basicPk SET name = ? WHERE pk = ?", "Jane", "1234")
            .ExecuteAsyncResult();

        // === ExecuteAsyncResult for DELETE ===
        result = await table.ExecutePartiQL<BasicPkEntity>(
            "DELETE FROM basicPk WHERE pk = ?", "1234")
            .ExecuteAsyncResult();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task PartiQL_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var cancellationToken = new CancellationToken();

        // === ToListAsyncResult with cancellation token ===
        var listResult = await table.ExecutePartiQL<BasicPkEntity>(
            "SELECT * FROM basicPk WHERE pk = ?", "1234")
            .ToListAsyncResult(cancellationToken);

        // === ExecuteAsyncResult with cancellation token ===
        var executeResult = await table.ExecutePartiQL<BasicPkEntity>(
            "UPDATE basicPk SET name = ? WHERE pk = ?", "Jane", "1234")
            .ExecuteAsyncResult(cancellationToken);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task PartiQL_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ToListAsyncResult with PK+SK entity ===
        var result = await table.ExecutePartiQL<BasicPkSkEntity>(
            "SELECT * FROM basicPkSk WHERE pk = ? AND sk = ?", "pk1", "sk1")
            .ToListAsyncResult();

        // === ExecuteAsyncResult with PK+SK entity ===
        var executeResult = await table.ExecutePartiQL<BasicPkSkEntity>(
            "UPDATE basicPkSk SET totalCount = ? WHERE pk = ? AND sk = ?", 100, "pk1", "sk1")
            .ExecuteAsyncResult();
    }
}
