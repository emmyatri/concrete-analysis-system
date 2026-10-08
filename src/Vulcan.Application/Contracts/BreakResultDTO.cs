using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class BreakResultDTO
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required DateTime BrokenAt { get; init; }

    [Required]
    public required int StrengthPsi { get; init; }
}
