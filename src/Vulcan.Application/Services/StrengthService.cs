using Vulcan.Application.Contracts;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Constants;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;

namespace Vulcan.Application.Services;

public sealed class StrengthService(
    IBatchRepository batchRepository,
    IPredictionRepository predictionRepository,
    IStrengthEstimateService strengthEstimateService,
    ICalibrationProvider calibrationProvider,
    ILimitProvider limitProvider,
    IEarlyWarningEvaluator? earlyWarningEvaluator = null)
{
    public async Task<GetBatchStrengthResponse?> GetByBatchNumberAsync(
        string batchNumber)
    {
        var batch = await batchRepository.GetByNumberAsync(batchNumber);

        if (batch is null)
        {
            return null;
        }

        var warningEvaluation = earlyWarningEvaluator is not null
            ? await earlyWarningEvaluator.EvaluateAsync(
                batch,
                CancellationToken.None)
            : null;

        var predictions = await predictionRepository.GetByBatchIdAsync(batch.Id);

        var strengthSpecimens = batch.Specimens
            .Select(specimen => new StrengthSpecimen
            {
                SpecimenId = specimen.Label,
                Readings = specimen.CuringReadings
                    .OrderBy(reading => reading.TakenAt)
                    .Select(reading => new TemperatureReading(
                        TakenAt: reading.TakenAt,
                        InternalTempF: (double)reading.InternalTempF
                    ))
                    .ToList()
            })
            .ToList();

        var breakMeasurements = batch.Specimens
            .Where(specimen => specimen.BreakResult is not null)
            .Select(specimen => new StrengthBreakMeasurement(
                SpecimenId: specimen.Label,
                Day: specimen.TestAgeDays switch
                {
                    TestAgeDays.Day3 => 3,
                    TestAgeDays.Day7 => 7,
                    TestAgeDays.Day28 => 28,
                    _ => throw new InvalidOperationException(
                        "Specimen has an invalid test age.")
                },
                StrengthPsi: specimen.BreakResult!.StrengthPsi,
                IsSynthetic: specimen.IsSynthetic
            ))
            .ToList();

        var calibration = calibrationProvider.Current();

        var settings = StrengthSettings.FromCalibration(
            calibration,
            batch.CementType
        );

        var maturitySettings = settings.Maturity;

        var estimate = strengthEstimateService.Estimate(
            batch.CastAt,
            batch.CementType,
            batch.ExpectedFinalBreakPsi,
            strengthSpecimens,
            settings,
            breakMeasurements
        );

        var wasAdjusted = new[]
        {
            estimate.Day3Adjustment,
            estimate.Day7Adjustment,
            estimate.Day28Adjustment
        }.Any(adjustment =>
            adjustment is not null &&
            adjustment.Stage != BreakAdjustmentStage.None);

        var selectedCurve = wasAdjusted && estimate.AdjustedDailyCurve is not null
            ? estimate.AdjustedDailyCurve
            : estimate.DailyCurve;

        IReadOnlyList<StrengthCurvePoint> unadjustedCurve = wasAdjusted
            ? estimate.DailyCurve
            : [];

        var curvePoints = selectedCurve
            .Select(point => new StrengthCurvePointDTO
            {
                Day = point.Day,
                Psi = (int)Math.Round(
                    point.EstimatedPsi,
                    0,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        var unadjustedCurvePoints = unadjustedCurve
            .Select(point => new StrengthCurvePointDTO
            {
                Day = point.Day,
                Psi = (int)Math.Round(
                    point.EstimatedPsi,
                    0,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();

        var strengthEstimates = predictions
            .OrderBy(prediction => prediction.HorizonDays)
            .Select(prediction => new StrengthEstimateDTO
            {
                HorizonDays = prediction.HorizonDays,

                EstimatedPsi = prediction.EstimatedPsi.HasValue
                    ? (int)Math.Round(
                        prediction.EstimatedPsi.Value,
                        0,
                        MidpointRounding.AwayFromZero)
                    : null,

                EquivalentAgeDays = (double?)prediction.EquivalentAgeDays,

                AdjustmentStage = prediction.AdjustmentStage,

                UnadjustedPsi = prediction.UnadjustedPsi.HasValue
                    ? (int)Math.Round(
                        prediction.UnadjustedPsi.Value,
                        0,
                        MidpointRounding.AwayFromZero)
                    : null,

                Basis = prediction.Basis,
                CalibratedVersion = prediction.CalibrationVersion,
                IsSynthetic = prediction.IsSynthetic,

                CreatedAtUtc = prediction.CreatedAtUtc,
                CreatedBy = prediction.CreatedBy,
                ModifiedAtUtc = prediction.ModifiedAtUtc,
                ModifiedBy = prediction.ModifiedBy
            })
            .ToList();

        var measuredBreaks = batch.Specimens
            .Where(specimen => specimen.BreakResult is not null)
            .Select(specimen => new MeasuredBreakDTO
            {
                SpecimenId = specimen.Id,
                SpecimenLabel = specimen.Label,

                AgeDays = specimen.TestAgeDays switch
                {
                    TestAgeDays.Day3 => 3,
                    TestAgeDays.Day7 => 7,
                    TestAgeDays.Day28 => 28,
                    _ => throw new InvalidOperationException(
                        "Specimen has an invalid test age.")
                },

                Psi = specimen.BreakResult!.StrengthPsi,
                IsSynthetic = specimen.IsSynthetic
            })
            .OrderBy(result => result.AgeDays)
            .ThenBy(result => result.SpecimenLabel)
            .ToList();

        var curingPoints = batch.Specimens
            .SelectMany(specimen => specimen.CuringReadings
                .Select(reading => new CuringPointDTO
                {
                    SpecimenId = specimen.Id,
                    SpecimenLabel = specimen.Label,
                    Time = reading.TakenAt,

                    InternalTempF = (double)reading.InternalTempF,
                    AmbientTempF = (double)reading.AmbientTempF,

                    MaturityFHours = reading.TakenAt >= batch.CastAt
                        ? MaturityCalculator.Calculate(
                            batch.CastAt,
                            specimen.CuringReadings.Select(r => new TemperatureReading(
                                r.TakenAt,
                                (double)r.InternalTempF
                            )),
                            reading.TakenAt,
                            maturitySettings
                        ).MaturityFHours
                        : null,

                    MoisturePct = (double)reading.MoisturePct,
                    RelativeHumidityPct = (double)reading.RelativeHumidityPct,
                    ResistivityOhmPerFt = (double)reading.ResistivityOhmPerFt,
                    Co2Ppm = (double)reading.Co2Ppm
                }))
            .OrderBy(point => point.SpecimenLabel)
            .ThenBy(point => point.Time)
            .ToList();

        // Fallback calculations until SCRUM-181 provides the evaluator.
        var day28Prediction = predictions
            .FirstOrDefault(prediction => prediction.HorizonDays == 28);

        decimal? projected28DayPsiRaw = day28Prediction?.EstimatedPsi;

        int? projected28DayPsi = projected28DayPsiRaw.HasValue
            ? (int)Math.Round(
                projected28DayPsiRaw.Value,
                0,
                MidpointRounding.AwayFromZero)
            : null;

        int? shortfallPsi = projected28DayPsi.HasValue
            ? batch.ExpectedFinalBreakPsi - projected28DayPsi.Value
            : null;

        double? shortfallPct = projected28DayPsiRaw.HasValue
            ? (double)((batch.ExpectedFinalBreakPsi - projected28DayPsiRaw.Value)
                / batch.ExpectedFinalBreakPsi * 100m)
            : null;

        return new GetBatchStrengthResponse
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            IsSynthetic = batch.IsSynthetic,
            ExpectedFinalBreakPsi = batch.ExpectedFinalBreakPsi,
            CementType = batch.CementType,

            Status = (warningEvaluation?.Projected28DayPsi
                      ?? projected28DayPsi) is not null
                ? "Estimated"
                : "InsufficientCuringData",

            AtRisk = warningEvaluation?.AtRisk ?? false,

            Projected28DayPsi = warningEvaluation is not null
                ? warningEvaluation.Projected28DayPsi is double projected
                    ? (int)Math.Round(
                        projected,
                        0,
                        MidpointRounding.AwayFromZero)
                    : null
                : projected28DayPsi,

            ShortfallPsi = warningEvaluation is not null
                ? warningEvaluation.ShortfallPsi is double shortfall
                    ? (int)Math.Round(
                        shortfall,
                        0,
                        MidpointRounding.AwayFromZero)
                    : null
                : shortfallPsi,

            ShortfallPct = warningEvaluation is not null
                ? warningEvaluation.ShortfallPct
                : shortfallPct,

            AccuracyTargetPct = (double?)limitProvider.Get(LimitKeys.PredictionAccuracyTargetPct).Warn?.Max,

            CurvePoints = curvePoints,
            UnadjustedCurvePoints = unadjustedCurvePoints,
            Estimates = strengthEstimates,
            MeasuredBreaks = measuredBreaks,
            EarlyWarnings = warningEvaluation?.EarlyWarnings.ToList() ?? [],
            CuringPoints = curingPoints
        };
    }
}
