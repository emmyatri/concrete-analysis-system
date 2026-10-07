using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateCuringReadingRequest
{
    [Required]
    public required DateTime TakenAt { get; set; }

    [Required]
    public required double InternalTempF { get; set; }

    [Required]
    public required double AmbientTempF { get; set; }

    [Required]
    public required double MoisturePct { get; set; }

    [Required]
    public required double RelativeHumidityPct { get; set; }

    [Required]
    public required double ResistivityOhmPerFt { get; set; }

    [Required]
    public required double Co2Ppm { get; set; }
}
