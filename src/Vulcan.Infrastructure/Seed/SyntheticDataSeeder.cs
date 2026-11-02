using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure.Seed;

public static class SyntheticDataSeeder
{
    private const string _syntheticUser = "Synthetic data";

    public static void Seed(VulcanDbContext db)
    {
        // Load the demo set once, next to any real batches (kept after the M1 demo, see
        // client-decisions.md). Skip it if synthetic batches are already there.
        if (db.Batch.Any(b => b.IsSynthetic))
        {
            return;
        }

        // Skip any demo batch whose number is already taken, so startup can't hit the unique index.
        var existingNumbers = db.Batch
            .Select(b => b.BatchNumber.ToUpper())
            .ToHashSet();

        var batches = SyntheticDataLoader.Load()
            .Where(b => !existingNumbers.Contains(b.BatchNumber.ToUpperInvariant()))
            .ToList();

        foreach (var batch in batches)
        {
            batch.IsSynthetic = true;

            foreach (var specimen in batch.Specimens)
            {
                specimen.IsSynthetic = true;
            }

            SetSyntheticAuditFields(batch);
        }

        db.Batch.AddRange(batches);
        db.SaveChanges();

        RecordWarnings(db, batches);
    }

    /// <summary>
    /// Saves the same warnings the app records when these values are entered by hand (batch entry,
    /// curing readings and 28-day breaks), so the demo batches show their flags and early warnings.
    /// Skipped when the validation limits or specific gravities haven't been seeded (some tests).
    /// </summary>
    private static void RecordWarnings(VulcanDbContext db, IReadOnlyList<Batch> batches)
    {
        if (!db.ValidationLimit.Any() || !db.SpecificGravity.Any())
        {
            return;
        }

        var limitEvaluator = new LimitEvaluator(new LimitProvider(db));
        var batchValidator = new BatchValidator(limitEvaluator);
        var oxideValidator = new CementOxideValidator(limitEvaluator);
        var curingValidator = new CuringReadingValidator(limitEvaluator);
        var breakValidator = new BreakResultValidator(limitEvaluator);
        var ratioCalculator = new WaterCementRatioCalculator(new SpecificGravityProvider(db), limitEvaluator);
        var now = DateTime.UtcNow;

        var warnings = new List<RecordWarning>();

        void Add(string entityType, int entityId, string key, decimal value, string message, DateTime at)
        {
            var warning = new RecordWarning
            {
                EntityType = entityType,
                EntityId = entityId,
                FieldName = key,
                LimitKey = key,
                Value = value,
                Message = message
            };
            SetCreated(warning, at, _syntheticUser);
            warnings.Add(warning);
        }

        void AddIfWarn(string entityType, int entityId, string key, decimal value, LimitResult? result, DateTime at)
        {
            if (result is LimitResult.Warn warn)
            {
                Add(entityType, entityId, key, value, warn.Message, at);
            }
        }

        foreach (var batch in batches)
        {
            AddIfWarn(nameof(Batch), batch.Id, LimitKeys.MixWaterPh, batch.MixWaterPh,
                batchValidator.CheckMixWaterPh(batch.MixWaterPh), batch.CastAt);

            if (batch.CementSurfacePh is { } surfacePh)
            {
                AddIfWarn(nameof(Batch), batch.Id, LimitKeys.CementSurfacePh, surfacePh,
                    batchValidator.CheckCementSurfacePh(surfacePh), batch.CastAt);
            }

            AddIfWarn(nameof(Batch), batch.Id, LimitKeys.FinalWeightLb, batch.FinalWeightLb,
                batchValidator.CheckFinalWeightLb(batch.FinalWeightLb), batch.CastAt);

            if (batch.Composition is not null && ratioCalculator.Calculate(batch.Composition) is { } ratio)
            {
                AddIfWarn(nameof(Batch), batch.Id, LimitKeys.WaterCementRatio, ratio,
                    ratioCalculator.Evaluate(ratio), batch.CastAt);
            }

            if (batch.CementOxideAnalysis is { } oxides)
            {
                var oxideOutcome = oxideValidator.Validate(
                    oxides.AluminaPct, oxides.IronOxidePct, oxides.MagnesiumOxidePct, oxides.SulfurTrioxidePct);

                foreach (var warning in oxideOutcome.Warnings)
                {
                    var value = warning.Key switch
                    {
                        LimitKeys.AluminaPct => oxides.AluminaPct,
                        LimitKeys.IronOxidePct => oxides.IronOxidePct,
                        LimitKeys.MagnesiumOxidePct => oxides.MagnesiumOxidePct,
                        _ => oxides.SulfurTrioxidePct
                    };
                    Add(nameof(Batch), batch.Id, warning.Key, value, warning.Message, batch.CastAt);
                }
            }

            foreach (var specimen in batch.Specimens)
            {
                foreach (var reading in specimen.CuringReadings)
                {
                    foreach (var warning in curingValidator.Validate(reading, batch.CastAt, now).Warnings)
                    {
                        var value = warning.Key switch
                        {
                            LimitKeys.InternalTempF => reading.InternalTempF,
                            LimitKeys.AmbientTempF => reading.AmbientTempF,
                            LimitKeys.MoisturePct => reading.MoisturePct,
                            LimitKeys.RelativeHumidityPct => reading.RelativeHumidityPct,
                            LimitKeys.ResistivityOhmPerFt => reading.ResistivityOhmPerFt,
                            _ => reading.Co2Ppm
                        };
                        Add(nameof(CuringReading), reading.Id, warning.Key, value, warning.Message, reading.TakenAt);
                    }
                }

                if (specimen.BreakResult is { } breakResult)
                {
                    var breakOutcome = breakValidator.Validate(
                        breakResult, batch.CastAt, false, now, specimen.TestAgeDays);

                    foreach (var warning in breakOutcome.Warnings)
                    {
                        Add(nameof(BreakResult), breakResult.Id, warning.Key, breakResult.StrengthPsi,
                            warning.Message, breakResult.BrokenAt);
                    }
                }
            }
        }

        if (warnings.Count > 0)
        {
            db.RecordWarning.AddRange(warnings);
            db.SaveChanges();
        }
    }

    private static void SetSyntheticAuditFields(Batch batch)
    {
        SetCreated(batch, batch.CastAt, _syntheticUser);

        if (batch.Composition is not null)
        {
            SetCreated(batch.Composition, batch.CastAt, _syntheticUser);
        }

        if (batch.CementOxideAnalysis is not null)
        {
            SetCreated(batch.CementOxideAnalysis, batch.CastAt, _syntheticUser);
        }

        foreach (var specimen in batch.Specimens)
        {
            SetCreated(specimen, batch.CastAt, _syntheticUser);

            if (specimen.BreakResult is not null)
            {
                SetCreated(
                    specimen.BreakResult,
                    specimen.BreakResult.BrokenAt,
                    _syntheticUser);
            }

            foreach (var reading in specimen.CuringReadings)
            {
                SetCreated(reading, reading.TakenAt, _syntheticUser);
            }
        }
    }

    private static void SetCreated(
        AuditableEntity entity,
        DateTime createdAtUtc,
        string createdBy)
    {
        entity.CreatedAtUtc = createdAtUtc;
        entity.CreatedBy = createdBy;
    }
}
