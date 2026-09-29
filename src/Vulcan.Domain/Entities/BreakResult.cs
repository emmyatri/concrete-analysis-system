namespace Vulcan.Domain.Entities;

public class BreakResult : AuditableEntity
{
    public int SpecimenId { get; set; }
    public Specimen Specimen { get; set; } = null!;

    public DateTime BrokenAt { get; set; }
    public int StrengthPsi { get; set; }
}
