namespace Oproto.FluentDynamoDb.Pagination;

public interface IPaginationRequest
{
    public int PageSize { get; }
    public string PaginationToken { get; }
}