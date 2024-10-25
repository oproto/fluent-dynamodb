namespace Oproto.FluentDynamoDb.Pagination;

public class PaginationRequest : IPaginationRequest
{
    public PaginationRequest(int pageSize, string paginationToken)
    {
        PageSize = pageSize;
        PaginationToken = paginationToken;
    }
    
    public int PageSize { get; }
    public string PaginationToken { get; }
}