using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class SpecimenDTO
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required int BatchId { get; init; }

    [Required]
    public required string Label { get; init; }

    [Required]
    public required double LengthIn { get; init; }

    [Required]
    public required double WidthIn { get; init; }

    [Required]
    public required TestAgeDays TestAgeDays { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; } = false;

    public BreakResultDTO? BreakResult { get; init; }

    [Required]
    public required List<CuringReadingDTO> CuringReadings { get; init; } = [];
}