using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;

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

    public static CuringReadingDTO FromDomain(CuringReading cr)
        => new()
        {
            Id = cr.Id,
            TakenAt = cr.TakenAt,
            InternalTempF = (double)cr.InternalTempF,
            AmbientTempF = (double)cr.AmbientTempF,
            MoisturePct = (double)cr.MoisturePct,
            RelativeHumidityPct = (double)cr.RelativeHumidityPct,
            ResistivityOhmPerFt = (double)cr.ResistivityOhmPerFt,
            Co2Ppm = (double)cr.Co2Ppm,
        };
}
