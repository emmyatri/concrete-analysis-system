namespace Vulcan.Application.Contracts;

public class GetSpecificGravityListResponse : List<SpecificGravityDTO>
{
    public GetSpecificGravityListResponse() { }

    public GetSpecificGravityListResponse(IEnumerable<SpecificGravityDTO> items) : base(items)
    { }
}
