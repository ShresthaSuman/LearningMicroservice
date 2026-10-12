namespace BuilfingBlocks.Pagination;

public record PaginationRequest(int pageIndex=1, int pageSize=10, int count=10);
