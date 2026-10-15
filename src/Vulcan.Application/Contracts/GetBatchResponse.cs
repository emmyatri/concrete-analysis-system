using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class GetBatchResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required DateTime CastAt { get; init; }

    [Required]
    public required double FinalWeightLb { get; init; }

    [Required]
    public required CementType CementType { get; init; }

    [Required]
    public required double MixWaterPh { get; init; }

    public decimal? CementSurfacePh { get; init; }

    [Required]
    public required bool IsSynthetic { get; init; }

    [Required]
    public required int ExpectedFinalBreakPsi { get; init; }

    [Required]
    public required DateTime CreatedAtUtc { get; init; }

    [Required]
    public required string CreatedBy { get; init; }

    public DateTime? ModifiedAtUtc { get; init; }

    public string? ModifiedBy { get; init; }

    [Required]
    public required List<SavedWarningDTO> Warnings { get; init; } = [];

    public CompositionDtoResponse? Composition { get; init; }

    public OxidesDto? Oxides { get; init; }

    public List<SpecimenDTO> Specimens { get; init; } = [];

    public static GetBatchResponse FromDomain(
        Batch batch,
        double? waterCementRatio,
        IReadOnlyList<RecordWarning> warnings)
    {
        return new GetBatchResponse
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            CastAt = batch.CastAt,
            FinalWeightLb = (double)batch.FinalWeightLb,
            CementType = batch.CementType,
            MixWaterPh = (double)batch.MixWaterPh,
            CementSurfacePh = batch.CementSurfacePh,
            IsSynthetic = batch.IsSynthetic,
            ExpectedFinalBreakPsi = batch.ExpectedFinalBreakPsi,
            CreatedAtUtc = batch.CreatedAtUtc,
            CreatedBy = batch.CreatedBy,
            ModifiedAtUtc = batch.ModifiedAtUtc,
            ModifiedBy = batch.ModifiedBy,

            Composition = batch.Composition != null
                ? CompositionDtoResponse.FromDomain(batch.Composition, waterCementRatio)
                : null,
            Oxides = batch.CementOxideAnalysis != null
                ? OxidesDto.FromDomain(batch.CementOxideAnalysis)
                : null,
            Specimens = [.. batch.Specimens.Select(SpecimenDTO.FromDomain)],

            Warnings = warnings.Select(w => new SavedWarningDTO
            {
                Id = w.Id,
                EntityType = w.EntityType,
                EntityId = w.EntityId,
                FieldName = w.FieldName,
                LimitKey = w.LimitKey,
                Value = (double)w.Value,
                Message = w.Message,
                CreatedAtUtc = w.CreatedAtUtc
            }).ToList()
        };
    }
}
