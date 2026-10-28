using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class MeasuredBreakDTO
{
    [Required]
    public required int SpecimenId { get; init; }

    [Required]
    public required string SpecimenLabel { get; init; }

    [Required]
    public required int AgeDays { get; init; }

    [Required]
    public required int Psi { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }
}
