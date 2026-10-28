using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class StrengthCurvePointDTO
{
    [Required]
    public required int Day { get; init; }

    [Required]
    public required int Psi { get; init; }
}
