using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

public class Composition : AuditableEntity
{
    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public decimal CementPct { get; set; }
    public decimal ScmPct { get; set; }
    public ScmType ScmType { get; set; }
    public decimal CoarseAggregatePct { get; set; }
    public decimal FineAggregatePct { get; set; }
    public decimal WaterPct { get; set; }
}
