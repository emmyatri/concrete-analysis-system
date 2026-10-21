namespace Vulcan.Application.Contracts;

public class GetValidationListResponse : List<ValidationLimitDTO>
{
    public GetValidationListResponse() { }

    public GetValidationListResponse(IEnumerable<ValidationLimitDTO> items) : base(items)
    { }
}
