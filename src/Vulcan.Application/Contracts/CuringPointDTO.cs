using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CuringPointDTO
{
    [Required]
    public required int SpecimenId { get; init; }

    [Required]
    public required string SpecimenLabel { get; init; }

    [Required]
    public required DateTime Time { get; init; }

    [Required]
    public required double InternalTempF { get; init; }

    [Required]
    public required double AmbientTempF { get; init; }

    public double? MaturityFHours { get; init; }

    public double? MoisturePct { get; init; }

    public double? RelativeHumidityPct { get; init; }

    public double? ResistivityOhmPerFt { get; init; }

    public double? Co2Ppm { get; init; }
}
