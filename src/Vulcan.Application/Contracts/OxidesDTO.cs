using System.ComponentModel.DataAnnotations;

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
}
