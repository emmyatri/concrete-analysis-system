using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

public class Batch : AuditableEntity
{
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime CastAt { get; set; }
    public decimal FinalWeightLb { get; set; }
    public int ExpectedFinalBreakPsi { get; set; }
    public CementType CementType { get; set; }
    public decimal MixWaterPh { get; set; }
    public bool IsSynthetic { get; set; }

    public Composition? Composition { get; set; }
    public CementOxideAnalysis? CementOxideAnalysis { get; set; }
    public ICollection<Specimen> Specimens { get; set; } = [];
}
