using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;

namespace Vulcan.Infrastructure.Interceptors;

public sealed class PredictionSaveChangesInterceptor(
    Func<VulcanDbContext, ICalibrationProvider> calibrationProviderFactory,
    IStrengthEstimateService strengthEstimateService,
    ILogger<PredictionSaveChangesInterceptor> logger
) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RecomputePredictions(eventData.Context);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecomputePredictions(eventData.Context);
        return result;
    }

    // Assumptions:
    // 1. handling delete state on CuringReading and BreakResult is not in scope
    private void RecomputePredictions(DbContext? context)
    {
        if (context is null || context is not VulcanDbContext)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var changedCuringReadings = context.ChangeTracker.Entries<CuringReading>()
            .Where(entry => entry.State is EntityState.Added
                or EntityState.Modified)
            .ToList();

        var changedBreakResults = context.ChangeTracker.Entries<BreakResult>()
            .Where(entry => entry.State is EntityState.Added
                or EntityState.Modified)
            .ToList();

        var changedBatchEntries = context.ChangeTracker.Entries<Batch>()
            .Where(entry => entry.State is EntityState.Added
                            || (entry.State == EntityState.Modified &&
                                (entry.Property(nameof(Batch.CementType)).IsModified ||
                                 entry.Property(nameof(Batch.ExpectedFinalBreakPsi)).IsModified ||
                                 entry.Property(nameof(Batch.CastAt)).IsModified)))
            .ToList();

        var addedSpecimens = context.ChangeTracker.Entries<Specimen>()
            .Where(entry => entry.State is EntityState.Added)
            .ToList();

        // nothing to change; exit
        if (changedCuringReadings.Count == 0
            && changedBreakResults.Count == 0
            && changedBatchEntries.Count == 0
            && addedSpecimens.Count == 0)
        {
            return;
        }

        CalibrationVersion calibration = null!;
        try
        {
            calibration = calibrationProviderFactory((VulcanDbContext)context).Current();
        }
        catch (InvalidOperationException)
        {
            // there is no current calibration version, thus a prediction cannot be made. exit.
            return;
        }

        // don't use navigational properties to be safe

        // get batch ids of changed resources
        var changedSpecimenIds = changedCuringReadings
            .Select(cr => cr.Entity.SpecimenId)
            .Concat(changedBreakResults
                .Select(br => br.Entity.SpecimenId))
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        var changedSpecimens = context.ChangeTracker.Entries<Specimen>()
            .Select(entry => entry.Entity)
            .Where(s => changedSpecimenIds.Contains(s.Id)
                || addedSpecimens.Any(a => ReferenceEquals(a.Entity, s)))
            .ToList();

        var changedBatchIds = context.Set<Specimen>()
            .Where(s => changedSpecimenIds.Contains(s.Id))
            .Select(s => s.BatchId)
            .Distinct()
            .ToList();

        var changedBatches = context.Set<Batch>()
            .Where(b => changedBatchIds.Contains(b.Id))
            .Include(b => b.Specimens)
            .ThenInclude(s => s.CuringReadings)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.BreakResult)
            .ToList();

        foreach (var entry in changedBatchEntries)
        {
            var batch = entry.Entity;

            if (entry.State == EntityState.Modified && batch.Id != 0)
            {
                context.Entry(batch)
                    .Collection(b => b.Specimens)
                    .Query()
                    .Include(s => s.CuringReadings)
                    .Include(s => s.BreakResult)
                    .Load();
            }

            if (!changedBatches.Any(b =>
                    ReferenceEquals(b, batch) ||
                    (batch.Id != 0 && b.Id == batch.Id)))
            {
                changedBatches.Add(batch);
            }
        }

        foreach (var specimen in changedSpecimens)
        {
            var batch = specimen.Batch;

            if (batch is null && specimen.BatchId != 0)
            {
                batch = changedBatches.FirstOrDefault(b =>
                    b.Id == specimen.BatchId);

                batch ??= context.ChangeTracker.Entries<Batch>()
                    .Select(entry => entry.Entity)
                    .FirstOrDefault(b => b.Id == specimen.BatchId);
            }

            if (batch is not null
                && !changedBatches.Any(b => ReferenceEquals(b, batch)
                    || (batch.Id != 0 && b.Id == batch.Id)))
            {
                changedBatches.Add(batch);
            }
        }

        // build lookup table for below queries
        var specimenBatchIds = context.Set<Specimen>()
            .Where(s => changedSpecimenIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.BatchId);

        foreach (var specimen in changedSpecimens)
        {
            if (specimen.Id != 0 && !specimenBatchIds.ContainsKey(specimen.Id))
            {
                specimenBatchIds[specimen.Id] = specimen.BatchId;
            }
        }

        foreach (var batch in changedBatches)
        {
            var batchSpecimens = batch.Specimens
                .Concat(changedSpecimens.Where(s =>
                    ReferenceEquals(s.Batch, batch)
                    || (batch.Id != 0 && s.BatchId == batch.Id)))
                .Distinct()
                .ToList();

            var batchChangedReadings = changedCuringReadings
                .Where(cr => batchSpecimens.Any(s =>
                    ReferenceEquals(cr.Entity.Specimen, s)
                    || (s.Id != 0 && cr.Entity.SpecimenId == s.Id)))
                .ToList();

            var batchChangedBreaks = changedBreakResults
                .Where(br => batchSpecimens.Any(s =>
                    ReferenceEquals(br.Entity.Specimen, s)
                    || (s.Id != 0 && br.Entity.SpecimenId == s.Id)))
                .ToList();

            UpdateBatchPredictions(
                context,
                calibration,
                batch,
                batchChangedReadings,
                batchChangedBreaks);
        }
    }

    /// <summary>
    /// Updates predictions for a given batch using the changed inputs.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="calibration"></param>
    /// <param name="batch"></param>
    /// <param name="changedCuringReadings"></param>
    /// <param name="changedBreakResults"></param>
    /// <exception cref="InvalidOperationException"></exception>
    private void UpdateBatchPredictions(
        DbContext context,
        CalibrationVersion calibration,
        Batch batch,
        IReadOnlyCollection<EntityEntry<CuringReading>> changedCuringReadings,
        IReadOnlyCollection<EntityEntry<BreakResult>> changedBreakResults)
    {
        var existingCuringReadings = batch.Specimens
            .SelectMany(x => x.CuringReadings)
            .ToList();

        var existingBreakResults = batch.Specimens
            .Select(s => s.BreakResult)
            .OfType<BreakResult>()
            .ToList();

        // merge (pending + existing). Baseline is current store
        var allReadings = existingCuringReadings
            .Concat(changedCuringReadings.Select(entry => entry.Entity))
            .Distinct()
            .ToList();

        var allBreakResults = existingBreakResults
            .Concat(changedBreakResults.Select(entry => entry.Entity))
            .Distinct()
            .ToList();

        var strengthSpecimens = allReadings
            // strength specimen contains many readings
            .GroupBy(cr => cr.Specimen.Label)
            .Select(crs =>
                // specimen + curing reading -> strength specimen
                new StrengthSpecimen
                {
                    SpecimenId = crs.Key,
                    Readings = [.. crs.Select(cr =>
                        new TemperatureReading(
                            TakenAt: cr.TakenAt,
                            InternalTempF: (double)cr.InternalTempF
                        ))]
                });

        var strengthBreakMeasurements = allBreakResults
            // strength break measurement contains one break result
            .Select(br =>
                // specimen + break result -> strength break measurement
                new StrengthBreakMeasurement(
                    SpecimenId: br.Specimen.Label,
                    Day: br.Specimen.TestAgeDays switch
                    {
                        TestAgeDays.Day3 => 3,
                        TestAgeDays.Day7 => 7,
                        TestAgeDays.Day28 => 28,
                        _ => throw new InvalidOperationException("Specimen has an incorrect test age"),
                    },
                    StrengthPsi: br.StrengthPsi,
                    IsSynthetic: batch.IsSynthetic
                ));

        StrengthEstimateResult estimate = null!;
        try
        {
            estimate = strengthEstimateService.Estimate(
                batch.CastAt,
                batch.CementType,
                batch.ExpectedFinalBreakPsi,
                [.. strengthSpecimens],
                StrengthSettings.FromCalibration(calibration, batch.CementType),
                [.. strengthBreakMeasurements]
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Strength estimate failed for batch {BatchNumber}; predictions not updated.", batch.BatchNumber);
            return;
        }

        var predictions = new List<PredictionEntity>();

        void AddEstimate(int dayNum, StrengthDayEstimate? day, BreakAdjustmentResult? adjustment)
            => predictions.Add(new()
            {
                Batch = batch, // required if batch is new (won't have an id yet)
                BatchId = batch.Id,
                CalibrationVersion = calibration.Version,
                IsSynthetic = batch.IsSynthetic,

                Basis = $"{estimate.Basis} {adjustment?.Basis}".Trim(),
                HorizonDays = dayNum,
                EstimatedPsi = (decimal?)(adjustment?.AdjustedPsi ?? day?.EstimatedPsi),
                EquivalentAgeDays = (decimal?)day?.EquivalentAgeDays,
                AdjustmentStage = adjustment?.Stage switch
                {
                    BreakAdjustmentStage.None => AdjustmentStage.None,
                    BreakAdjustmentStage.ThreeDay => AdjustmentStage.ThreeDay,
                    BreakAdjustmentStage.ThreeAndSevenDay => AdjustmentStage.ThreeAndSevenDay,
                    _ => null,
                },
                UnadjustedPsi = (decimal?)(adjustment?.UnadjustedPsi ?? day?.EstimatedPsi),
            });

        // add three predictions to represent it was attempted
        AddEstimate(3, estimate.Day3, estimate.Day3Adjustment);
        AddEstimate(7, estimate.Day7, estimate.Day7Adjustment);
        AddEstimate(28, estimate.Day28, estimate.Day28Adjustment);

        var existingPredictions = batch.Id == 0
            ? [] // new batch doesn't have predictions
            : context.Set<PredictionEntity>()
                .Where(p => p.BatchId == batch.Id)
                .ToList();

        existingPredictions.ForEach(old =>
        {
            // update
            if (predictions.FirstOrDefault(p => p.HorizonDays == old.HorizonDays) is { } match)
            {
                old.CalibrationVersion = match.CalibrationVersion;
                old.Basis = match.Basis;
                old.EstimatedPsi = match.EstimatedPsi;
                old.EquivalentAgeDays = match.EquivalentAgeDays;
                old.AdjustmentStage = match.AdjustmentStage;
                old.UnadjustedPsi = match.UnadjustedPsi;
                old.IsSynthetic = match.IsSynthetic;
            }
        });
        // not updated, but in predictions, so add
        var toAdd = predictions
            .Where(p => !existingPredictions.Any(e => e.HorizonDays == p.HorizonDays))
            .ToList();
        // tracking is not ignored here so that changes fall through to the audit interceptor
        context.AddRange(toAdd);

        // existingPredictions should be updated automatically since it was queried with tracking
    }
}
