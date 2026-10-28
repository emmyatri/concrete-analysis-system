using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

// named differently to avoid conflicts with the "Prediction" namespace
public class PredictionEntity : AuditableEntity
{
    public int BatchId { get; set; }

    public Batch Batch { get; set; } = null!;

    public string CalibrationVersion { get; set; } = string.Empty;

    public CalibrationVersion CalibrationVersionEntity { get; set; } = null!;

    public string Basis { get; set; } = string.Empty;

    public int HorizonDays { get; set; }

    public decimal? EstimatedPsi { get; set; }

    public decimal? EquivalentAgeDays { get; set; }

    public AdjustmentStage? AdjustmentStage { get; set; }

    public decimal? UnadjustedPsi { get; set; }

    public bool IsSynthetic { get; set; }
}
