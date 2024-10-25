using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.Storage;

public class DynamoDbIndex
{
    public DynamoDbIndex(DynamoDbTableBase table, string indexName)
    {
        _table = table;
        Name = indexName;
    }

    private readonly DynamoDbTableBase _table;
    public string Name { get; private init; }
    
    public QueryRequestBuilder Query => new QueryRequestBuilder(_table.DynamoDbClient).ForTable(_table.Name).UsingIndex(Name);
}