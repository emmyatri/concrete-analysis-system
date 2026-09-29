using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

public class Specimen : AuditableEntity
{
    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public decimal LengthIn { get; set; }
    public decimal WidthIn { get; set; }
    public TestAgeDays TestAgeDays { get; set; }
    public bool IsSynthetic { get; set; }

    public BreakResult? BreakResult { get; set; }
    public ICollection<CuringReading> CuringReadings { get; set; } = [];
}
