using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Contracts;

public class BreakResultDTO
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required DateTime BrokenAt { get; init; }

    [Required]
    public required int StrengthPsi { get; init; }

    public static BreakResultDTO FromDomain(BreakResult br)
        => new()
        {
            Id = br.Id,
            BrokenAt = br.BrokenAt,
            StrengthPsi = br.StrengthPsi,
        };
}
