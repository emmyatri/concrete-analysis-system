using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class EarlyWarningDTO
{
    [Required]
    public required string Warning { get; init; }

    [Required]
    public required string Field { get; init; }

    public double? Value { get; init; }

    [Required]
    public required string LimitKey { get; init; }

    public double? Limit { get; init; }
}
