using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Wrapper class that provides scan functionality while maintaining access to all core table operations.
/// 
/// This class implements the intentional friction pattern for scan operations by requiring
/// developers to explicitly call AsScannable() to access scan functionality. This design
/// helps prevent accidental use of expensive scan operations while still providing access
/// when legitimately needed.
/// 
/// The wrapper maintains full access to the underlying table instance, ensuring that
/// custom properties and methods defined in your table class remain accessible.
/// </summary>
internal class ScannableDynamoDbTable : IScannableDynamoDbTable
{
    private readonly DynamoDbTableBase _table;

    /// <summary>
    /// Initializes a new instance of the ScannableDynamoDbTable wrapper.
    /// </summary>
    /// <param name="table">The underlying table instance to wrap.</param>
    public ScannableDynamoDbTable(DynamoDbTableBase table)
    {
        _table = table;
    }

    /// <summary>
    /// Gets the DynamoDB client from the underlying table.
    /// </summary>
    public IAmazonDynamoDB DynamoDbClient => _table.DynamoDbClient;

    /// <summary>
    /// Gets the table name from the underlying table.
    /// </summary>
    public string Name => _table.Name;

    /// <summary>
    /// Gets the underlying table instance, providing access to custom properties and methods.
    /// </summary>
    public DynamoDbTableBase UnderlyingTable => _table;
    


    /// <summary>
    /// Creates a new Query operation builder (pass-through to underlying table).
    /// </summary>
    public QueryRequestBuilder Query() => _table.Query();

    /// <summary>
    /// Creates a new Query operation builder with a key condition expression (pass-through to underlying table).
    /// </summary>
    public QueryRequestBuilder Query(string keyConditionExpression, params object[] values) => 
        _table.Query(keyConditionExpression, values);

    /// <summary>
    /// Creates a new GetItem operation builder (pass-through to underlying table).
    /// </summary>
    public GetItemRequestBuilder Get() => _table.Get();

    /// <summary>
    /// Creates a new UpdateItem operation builder (pass-through to underlying table).
    /// </summary>
    public UpdateItemRequestBuilder Update() => _table.Update();

    /// <summary>
    /// Creates a new DeleteItem operation builder (pass-through to underlying table).
    /// </summary>
    public DeleteItemRequestBuilder Delete() => _table.Delete();

    /// <summary>
    /// Creates a new PutItem operation builder (pass-through to underlying table).
    /// </summary>
    public PutItemRequestBuilder Put() => _table.Put();

    /// <summary>
    /// Creates a new Scan operation builder - only available through this scannable interface.
    /// 
    /// WARNING: Scan operations can be expensive. Use Query operations instead whenever possible.
    /// </summary>
    public ScanRequestBuilder Scan() => new ScanRequestBuilder(DynamoDbClient, _table.Logger).ForTable(Name);
}