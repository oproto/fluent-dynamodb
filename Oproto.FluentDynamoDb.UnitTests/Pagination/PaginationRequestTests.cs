using FluentAssertions;
using Oproto.FluentDynamoDb.Pagination;

namespace Oproto.FluentDynamoDb.UnitTests.Pagination;

public class PaginationRequestTests
{
    [Fact]
    public void PaginationRequest_Constructor_Succcess()
    {
        var paginationRequest = new PaginationRequest(10, "TOKEN");
        paginationRequest.PageSize.Should().Be(10);
        paginationRequest.PaginationToken.Should().Be("TOKEN");
    }
}