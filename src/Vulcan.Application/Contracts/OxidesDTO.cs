using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Contracts;

public class OxidesDto
{
    [Required]
    public double? AluminaPct { get; init; }

    [Required]
    public double? IronOxidePct { get; init; }

    [Required]
    public double? MagnesiumOxidePct { get; init; }

    [Required]
    public double? SulfurTrioxidePct { get; init; }

    public CementOxideAnalysis ToDomain()
        => new()
        {
            AluminaPct = (decimal)AluminaPct!.Value,
            IronOxidePct = (decimal)IronOxidePct!.Value,
            MagnesiumOxidePct = (decimal)MagnesiumOxidePct!.Value,
            SulfurTrioxidePct = (decimal)SulfurTrioxidePct!.Value,
        };

    public static OxidesDto FromDomain(CementOxideAnalysis c)
        => new()
        {
            AluminaPct = (double)c.AluminaPct,
            IronOxidePct = (double)c.IronOxidePct,
            MagnesiumOxidePct = (double)c.MagnesiumOxidePct,
            SulfurTrioxidePct = (double)c.SulfurTrioxidePct
        };
}
