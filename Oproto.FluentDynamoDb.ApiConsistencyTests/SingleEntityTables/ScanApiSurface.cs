using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class ScanApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllQueryPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        ScannableTable table = new ScannableTable(client, null);

        // Scan with filter-expression string based with explicit names
        var results = await table.Scan("#age >= :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", "21")
            .ToListAsync();

        // Scan with filter expression using string based with string formatting
        results = await table.Scan("age >= {0}", 21).ToListAsync();
        
        // Scan with filter-expression using c# Lambda Expression translation
        results = await table.Scan(x => x.Age >= 21).ToListAsync();
    }
}