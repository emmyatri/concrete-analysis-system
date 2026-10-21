using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class CreateSpecimenRequest
{
    [Required]
    public required string Label { get; init; }

    [Required]
    public required double LengthIn { get; init; }

    [Required]
    public required double WidthIn { get; init; }

    [Required]
    public required TestAgeDays TestAgeDays { get; init; }

    public bool isSynthetic { get; init; } = false;
}
