namespace Vulcan.Application.Contracts;

public class GetBatchListResponse : List<GetBatchResponse>
{
    public GetBatchListResponse()
    {
    }

    public GetBatchListResponse(IEnumerable<GetBatchResponse> items)
        : base(items)
    {
    }
}
