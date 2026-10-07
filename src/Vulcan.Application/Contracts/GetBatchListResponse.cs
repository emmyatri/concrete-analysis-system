namespace Vulcan.Application.Contracts;

public class GetBatchListResponse : List<BatchSummaryDTO>
{
    public GetBatchListResponse()
    {
    }

    public GetBatchListResponse(IEnumerable<BatchSummaryDTO> items)
        : base(items)
    {
    }
}
