using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Wrapper class that provides scan functionality while maintaining access to all core table operations.
/// This class implements the intentional friction pattern for scan operations.
/// </summary>
internal class ScannableDynamoDbTable : IScannableDynamoDbTable
{
    private readonly DynamoDbTableBase _table;

    public ScannableDynamoDbTable(DynamoDbTableBase table)
    {
        _table = table;
    }

    // Pass-through properties from the underlying table
    public IAmazonDynamoDB DynamoDbClient => _table.DynamoDbClient;
    public string Name => _table.Name;
    public DynamoDbTableBase UnderlyingTable => _table;

    // Pass-through operations from IDynamoDbTable
    public GetItemRequestBuilder Get => _table.Get;
    public PutItemRequestBuilder Put => _table.Put;
    public UpdateItemRequestBuilder Update => _table.Update;
    public QueryRequestBuilder Query => _table.Query;
    public DeleteItemRequestBuilder Delete => _table.Delete;

    // Scan operation - only available through this scannable interface
    public ScanRequestBuilder Scan => new ScanRequestBuilder(DynamoDbClient).ForTable(Name);
}