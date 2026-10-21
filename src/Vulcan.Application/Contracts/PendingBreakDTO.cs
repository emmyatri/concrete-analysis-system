using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

/// <summary>A cylinder that hasn't been broken yet (GET /api/breaks/pending).</summary>
public class PendingBreakDTO
{
    [Required]
    public required int SpecimenId { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required string Label { get; init; }

    [Required]
    public required TestAgeDays TestAgeDays { get; init; }

    [Required]
    public required DateTime CastAt { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }
}
