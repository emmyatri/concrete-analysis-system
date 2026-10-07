using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CuringReadingDTO
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required DateTime TakenAt { get; init; }

    [Required]
    public required double InternalTempF { get; init; }

    [Required]
    public required double AmbientTempF { get; init; }

    [Required]
    public required double MoisturePct { get; init; }

    [Required]
    public required double RelativeHumidityPct { get; init; }

    [Required]
    public required double ResistivityOhmPerFt { get; init; }

    [Required]
    public required double Co2Ppm { get; init; }
}
