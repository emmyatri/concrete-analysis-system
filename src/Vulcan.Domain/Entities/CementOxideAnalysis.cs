namespace Vulcan.Domain.Entities;

public class CementOxideAnalysis : AuditableEntity
{
    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public decimal AluminaPct { get; set; }
    public decimal IronOxidePct { get; set; }
    public decimal MagnesiumOxidePct { get; set; }
    public decimal SulfurTrioxidePct { get; set; }
}
