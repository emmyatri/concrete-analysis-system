namespace Vulcan.Domain.Entities;

public class CuringReading : AuditableEntity
{
    public int SpecimenId { get; set; }
    public Specimen Specimen { get; set; } = null!;

    public DateTime TakenAt { get; set; }
    public decimal InternalTempF { get; set; }
    public decimal AmbientTempF { get; set; }
    public decimal MoisturePct { get; set; }
    public decimal RelativeHumidityPct { get; set; }
    public decimal ResistivityOhmPerFt { get; set; }
    public decimal Co2Ppm { get; set; }
}
