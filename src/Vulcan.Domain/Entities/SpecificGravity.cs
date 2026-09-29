using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

public class SpecificGravity
{
    public int Id { get; set; }
    public Material Material { get; set; }
    public decimal Value { get; set; }
}
