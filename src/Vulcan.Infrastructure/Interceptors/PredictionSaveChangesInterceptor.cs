using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;

namespace Vulcan.Infrastructure.Interceptors;

public sealed class PredictionSaveChangesInterceptor(
    ICalibrationProvider calibrationProvider,
    IStrengthEstimateService strengthEstimateService
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

        // nothing to change; exit
        if (changedCuringReadings.Count == 0 && changedBreakResults.Count == 0)
        {
            return;
        }

        CalibrationVersion calibration = null!;
        try
        {
            calibration = calibrationProvider.Current();
        }
        catch (InvalidOperationException)
        {
            // there is no current calibration version, thus a prediction cannot be made. exit.
            return;
        }

        // don't use navigational properties to be safe

        // get batch ids of changed resources
        var changedSpecimenIds = changedCuringReadings
            .Select(cr => cr?.Entity?.SpecimenId)
            .Concat(changedBreakResults
                .Select(br => br?.Entity?.SpecimenId))
            .Where(id => id != null)
            .Distinct()
            .ToList();
        var changedBatchIds = context.Set<Specimen>()
            .Where(s => changedSpecimenIds.Contains(s.Id))
            .Select(s => s.BatchId)
            .ToList();

        if (changedBatchIds is null)
        {
            return;
        }

        var changedBatches = context.Set<Batch>()
            .Where(b => changedBatchIds.Contains(b.Id))
            .Include(b => b.Specimens)
            .ThenInclude(s => s.CuringReadings)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.CuringReadings);

        if (changedBatches is null)
        {
            return;
        }

        // build lookup table for below queries
        var specimenBatchIds = context.Set<Specimen>()
            .Where(s => changedSpecimenIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.BatchId);

        foreach (var batch in changedBatches)
        {
            var batchChangedReadings = changedCuringReadings
                .Where(cr => cr?.Entity != null
                    && specimenBatchIds.TryGetValue(
                        cr.Entity.SpecimenId, out var batchId)
                    && batchId == batch.Id)
                .ToList();
            var batchChangedBreaks = changedBreakResults
                .Where(br => br?.Entity != null
                    && specimenBatchIds.TryGetValue(
                        br.Entity.SpecimenId, out var batchId)
                    && batchId == batch.Id)
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
        var allReadings = existingCuringReadings.ToDictionary(cr => cr.Id);
        var allBreakResults = existingBreakResults.ToDictionary(br => br.Id);

        // overwrite / add pending changes
        foreach (var curingReading in changedCuringReadings)
        {
            if (curingReading is null)
            {
                continue;
            }

            allReadings[curingReading.Entity.Id] = curingReading.Entity;
        }
        foreach (var breakResult in changedBreakResults)
        {
            if (breakResult is null)
            {
                continue;
            }

            allBreakResults[breakResult.Entity.Id] = breakResult.Entity;
        }

        var strengthSpecimens = allReadings
            .Values
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
            .Values
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

        var estimate = strengthEstimateService.Estimate(
            batch.CastAt,
            batch.CementType,
            batch.ExpectedFinalBreakPsi,
            [.. strengthSpecimens],
            StrengthSettings.FromCalibration(calibration, batch.CementType),
            [.. strengthBreakMeasurements]
        );

        var predictions = new List<PredictionEntity>();

        void AddEstimate(StrengthDayEstimate day, BreakAdjustmentResult? adjustment)
            => predictions.Add(new()
            {
                BatchId = batch.Id,
                CalibrationVersion = calibration.Version,
                IsSynthetic = batch.IsSynthetic,

                Basis = estimate.Basis,
                HorizonDays = day.Day,
                EstimatedPsi = (decimal?)day?.EstimatedPsi,
                EquivalentAgeDays = (decimal?)day?.EquivalentAgeDays,
                AdjustmentStage = adjustment?.Stage switch
                {
                    BreakAdjustmentStage.None => AdjustmentStage.None,
                    BreakAdjustmentStage.ThreeDay => AdjustmentStage.ThreeDay,
                    BreakAdjustmentStage.ThreeAndSevenDay => AdjustmentStage.ThreeAndSevenDay,
                    _ => null,
                },
                UnadjustedPsi = (decimal?)adjustment?.UnadjustedPsi,
            });

        if (estimate.Day3 is not null)
        {
            AddEstimate(estimate.Day3, estimate.Day3Adjustment);
        }
        if (estimate.Day7 is not null)
        {
            AddEstimate(estimate.Day7, estimate.Day7Adjustment);
        }
        if (estimate.Day28 is not null)
        {
            AddEstimate(estimate.Day28, estimate.Day28Adjustment);
        }

        var existingPredictions = context.Set<PredictionEntity>()
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
        // not updated, and not in predictions, so delete
        var toDelete = existingPredictions
            .Where(e => !predictions.Any(p => e.HorizonDays == p.HorizonDays))
            .ToList();

        // tracking is not ignored here so that changes fall through to the audit interceptor
        context.AddRange(toAdd);
        context.RemoveRange(toDelete);

        // existingPredictions should be updated automatically since it was queried with tracking
    }
}
