using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateBreakResultRequest
{
    [Required]
    public required DateTime BrokenAt { get; init; }

    [Required]
    public required int StrengthPsi { get; init; }
}
