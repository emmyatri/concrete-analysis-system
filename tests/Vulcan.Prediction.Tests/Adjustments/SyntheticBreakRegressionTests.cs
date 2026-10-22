using System.Text.Json;
using Vulcan.Domain.Enums;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;
using Xunit;

namespace Vulcan.Prediction.Tests.Adjustments;

public class SyntheticBreakRegressionTests
{
    [Fact]
    public void SyntheticDataset_HasNineCompleteBatches()
    {
        string path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "../../../../../data/synthetic/batches.json"));

        using JsonDocument document =
            JsonDocument.Parse(File.ReadAllText(path));

        JsonElement batches =
            document.RootElement.GetProperty("Batches");

        Assert.Equal(9, batches.GetArrayLength());

        foreach (JsonElement batch in batches.EnumerateArray())
        {
            JsonElement specimens = batch.GetProperty("Specimens");

            Assert.Equal(9, specimens.GetArrayLength());

            foreach (string age in new[] { "Day3", "Day7", "Day28" })
            {
                JsonElement[] matching = specimens
                    .EnumerateArray()
                    .Where(s =>
                        s.GetProperty("TestAgeDays").GetString() == age)
                    .ToArray();

                Assert.Equal(3, matching.Length);

                Assert.All(matching, specimen =>
                {
                    Assert.True(
                        specimen.GetProperty("BreakResult")
                            .GetProperty("StrengthPsi")
                            .GetDouble() > 0);

                    Assert.Equal(
                        7,
                        specimen.GetProperty("CuringReadings")
                            .GetArrayLength());
                });
            }
        }
    }
    
    private sealed record SyntheticBatchFixture(
    string BatchNumber,
    DateTime CastAt,
    CementType CementType,
    double ExpectedFinalPsi,
    StrengthSpecimen[] Specimens,
    StrengthBreakMeasurement[] Breaks);

    private static IReadOnlyList<SyntheticBatchFixture> LoadSyntheticBatches()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
    
        while (directory != null)
        {
            string path = Path.Combine(
                directory.FullName,
                "data",
                "synthetic",
                "batches.json");
    
            if (File.Exists(path))
            {
                using var document =
                    JsonDocument.Parse(File.ReadAllText(path));
    
                return document.RootElement
                    .GetProperty("Batches")
                    .EnumerateArray()
                    .Select(batch =>
                    {
                        string batchNumber = batch
                            .GetProperty("BatchNumber")
                            .GetString()!;
    
                        DateTime castAt = batch
                            .GetProperty("CastAt")
                            .GetDateTime();
    
                        CementType cementType =
                            Enum.Parse<CementType>(
                                batch.GetProperty("CementType")
                                    .GetString()!);
    
                        double expectedFinalPsi = batch
                            .GetProperty("ExpectedFinalBreakPsi")
                            .GetDouble();
    
                        var specimens = batch
                            .GetProperty("Specimens")
                            .EnumerateArray()
                            .ToArray();
    
                        StrengthSpecimen[] strengthSpecimens =
                            specimens.Select(specimen =>
                                new StrengthSpecimen
                                {
                                    SpecimenId = specimen
                                        .GetProperty("Label")
                                        .GetString()!,
    
                                    Readings = specimen
                                        .GetProperty("CuringReadings")
                                        .EnumerateArray()
                                        .Select(reading =>
                                            new TemperatureReading(
                                                reading.GetProperty("TakenAt")
                                                    .GetDateTime(),
                                                reading.GetProperty("InternalTempF")
                                                    .GetDouble()))
                                        .ToArray()
                                }).ToArray();
    
                        StrengthBreakMeasurement[] breaks =
                            specimens.Select(specimen =>
                                new StrengthBreakMeasurement(
                                    specimen.GetProperty("Label")
                                        .GetString()!,
                                    int.Parse(
                                        specimen.GetProperty("TestAgeDays")
                                            .GetString()![3..]),
                                    specimen.GetProperty("BreakResult")
                                        .GetProperty("StrengthPsi")
                                        .GetDouble(),
                                    specimen.GetProperty("IsSynthetic")
                                        .GetBoolean()))
                                .ToArray();
    
                        return new SyntheticBatchFixture(
                            batchNumber,
                            castAt,
                            cementType,
                            expectedFinalPsi,
                            strengthSpecimens,
                            breaks);
                    })
                    .ToArray();
            }
    
            directory = directory.Parent;
        }
    
        throw new FileNotFoundException(
            "Could not locate data/synthetic/batches.json");
    }
    
    [Fact]
    public void ThreeDayBreaks_PredictSevenDayStrength_WithinFivePercentForEightBatches()
    {
        var batches = LoadSyntheticBatches();
    
        Assert.Equal(9, batches.Count);
    
        int withinTolerance = 0;
        var failures = new List<string>();
    
        foreach (var batch in batches)
        {
            var (a, b) = batch.CementType switch
            {
                CementType.TypeI => (4.0, 0.85),
                CementType.TypeII => (4.0, 0.85),
                CementType.TypeIII => (2.3, 0.92),
                _ => throw new InvalidOperationException(
                    $"Unsupported cement type: {batch.CementType}")
            };
    
            var settings = new StrengthSettings
            {
                CementType = batch.CementType,
                A = a,
                B = b,
                CalibrationVersion = "synthetic-regression-v1",
                Maturity = new MaturitySettings()
            };
    
            // Only 3-day breaks are available for this prediction.
            StrengthBreakMeasurement[] knownBreaks = batch.Breaks
                .Where(measurement => measurement.Day == 3)
                .ToArray();
    
            var result = StrengthEstimator.Estimate(
                batch.CastAt,
                batch.CementType,
                batch.ExpectedFinalPsi,
                batch.Specimens,
                settings,
                knownBreaks);
    
            Assert.NotNull(result.Day7Adjustment);
            Assert.NotNull(result.AdjustedDailyCurve);
    
            double predictedPsi =
                result.AdjustedDailyCurve[6].EstimatedPsi;
    
            double actualPsi = batch.Breaks
                .Where(measurement => measurement.Day == 7)
                .Average(measurement => measurement.StrengthPsi);
    
            double errorPercent =
                Math.Abs(predictedPsi - actualPsi) / actualPsi * 100.0;
    
            if (errorPercent <= 5.0)
            {
                withinTolerance++;
            }
            else
            {
                failures.Add(
                    $"{batch.BatchNumber}: {errorPercent:F2}% error");
            }
        }
    
        Assert.Single(failures);
        Assert.StartsWith("VM-SYN-006:", failures[0]);

        Assert.True(
            withinTolerance >= 8,
            $"Expected at least 8 of 9 batches within ±5%, " +
            $"but only {withinTolerance} passed. " +
            $"Outside tolerance: {string.Join("; ", failures)}");
    }
    

    [Fact]
    public void ThreeAndSevenDayBreaks_Predict28DayStrength_WithinFivePercentForAllBatches()
    {
        var batches = LoadSyntheticBatches();

        Assert.Equal(9, batches.Count);

        var failures = new List<string>();

        foreach (var batch in batches)
        {
            var (a, b) = batch.CementType switch
            {
                CementType.TypeI => (4.0, 0.85),
                CementType.TypeII => (4.0, 0.85),
                CementType.TypeIII => (2.3, 0.92),
                _ => throw new InvalidOperationException(
                    $"Unsupported cement type: {batch.CementType}")
            };

            var settings = new StrengthSettings
            {
                CementType = batch.CementType,
                A = a,
                B = b,
                CalibrationVersion = "synthetic-regression-v1",
                Maturity = new MaturitySettings()
            };

            // At the 28-day prediction point, only earlier breaks are known.
            StrengthBreakMeasurement[] knownBreaks = batch.Breaks
                .Where(measurement => measurement.Day is 3 or 7)
                .ToArray();

            var result = StrengthEstimator.Estimate(
                batch.CastAt,
                batch.CementType,
                batch.ExpectedFinalPsi,
                batch.Specimens,
                settings,
                knownBreaks);

            Assert.NotNull(result.Day28Adjustment);
            Assert.NotNull(result.AdjustedDailyCurve);
            
            Assert.True(
                result.Day28Adjustment.Stage ==
                BreakAdjustmentStage.ThreeAndSevenDay,
                $"{batch.BatchNumber}: Expected 3-and-7-day adjustment, " +
                $"but got {result.Day28Adjustment.Stage}. " +
                $"Reason: {result.Day28Adjustment.FallbackReason}");
            
            /* DIAG FOR THIS SUCKER
            if (batch.BatchNumber == "VM-SYN-005")
            {
                var adjustment = result.Day28Adjustment;

                Console.WriteLine(
                    $"VM-SYN-005 diagnostics:\n" +
                    $"3-day equivalent age: {result.Day3!.EquivalentAgeDays:F6}\n" +
                    $"7-day equivalent age: {result.Day7!.EquivalentAgeDays:F6}\n" +
                    $"3-day measured PSI: {knownBreaks.Where(x => x.Day == 3).Average(x => x.StrengthPsi):F3}\n" +
                    $"7-day measured PSI: {knownBreaks.Where(x => x.Day == 7).Average(x => x.StrengthPsi):F3}\n" +
                    $"Original 28-day PSI: {adjustment.UnadjustedPsi:F3}\n" +
                    $"28-day equivalent age: {result.Day28!.EquivalentAgeDays:F6}\n" +
                    $"Adjusted A: {adjustment.AdjustedA:F6}\n" +
                    $"Reference PSI: {adjustment.ReferenceStrengthPsi:F3}\n" +
                    $"Adjusted 28-day PSI: {adjustment.AdjustedPsi:F3}");
                
                var oldDatumSettings = new StrengthSettings
                {
                    CementType = batch.CementType,
                    A = a,
                    B = b,
                    CalibrationVersion = "synthetic-regression-old-datum",
                    Maturity = new MaturitySettings
                    {
                        DatumTempF = 32.0
                    }
                };

                var oldDatumResult = StrengthEstimator.Estimate(
                    batch.CastAt,
                    batch.CementType,
                    batch.ExpectedFinalPsi,
                    batch.Specimens,
                    oldDatumSettings,
                    knownBreaks);

                double actual28Psi = batch.Breaks
                    .Where(x => x.Day == 28)
                    .Average(x => x.StrengthPsi);

                double oldDatumPrediction =
                    oldDatumResult.AdjustedDailyCurve![27].EstimatedPsi;

                double oldDatumError =
                    Math.Abs(oldDatumPrediction - actual28Psi) / actual28Psi * 100.0;

                Console.WriteLine(
                    $"32°F datum prediction: {oldDatumPrediction:F3} PSI\n" +
                    $"32°F datum error: {oldDatumError:F3}%");
                
                double fittedDay3Psi =
                    result.AdjustedDailyCurve![2].EstimatedPsi;

                double fittedDay7Psi =
                    result.AdjustedDailyCurve[6].EstimatedPsi;

                double measuredDay3Psi = knownBreaks
                    .Where(x => x.Day == 3)
                    .Average(x => x.StrengthPsi);

                double measuredDay7Psi = knownBreaks
                    .Where(x => x.Day == 7)
                    .Average(x => x.StrengthPsi);

                Assert.Equal(measuredDay3Psi, fittedDay3Psi, precision: 6);
                Assert.Equal(measuredDay7Psi, fittedDay7Psi, precision: 6);
            }
            */

            // EXPERIMENT: Compare the original M2-14 break-adjusted prediction
            // against the unadjusted estimate and the actual 28-day break.
            //
            // The specified two-break fitting equation improves some batches
            // substantially, but the original estimate is more accurate for others.
            // VM-SYN-006 improves from 39.75% error to 0.28%, while VM-SYN-005
            // worsens from 1.57% to 5.07%.
            //
            // All experiments below are diagnostic only. The production
            // BreakAdjustmentCalculator and the original test assertion remain unchanged.

            double predictedPsi =
                result.AdjustedDailyCurve[27].EstimatedPsi;

            double actualPsi = batch.Breaks
                .Where(measurement => measurement.Day == 28)
                .Average(measurement => measurement.StrengthPsi);

            double errorPercent =
                Math.Abs(predictedPsi - actualPsi) / actualPsi * 100.0;
            
            double originalPsi =
                result.DailyCurve[27].EstimatedPsi;

            double originalErrorPercent =
                Math.Abs(originalPsi - actualPsi) / actualPsi * 100.0;

            double adjustedErrorPercent =
                Math.Abs(predictedPsi - actualPsi) / actualPsi * 100.0;

            Console.WriteLine(
                $"{batch.BatchNumber} | " +
                $"Original: {originalPsi:F0} PSI ({originalErrorPercent:F2}%) | " +
                $"Adjusted: {predictedPsi:F0} PSI ({adjustedErrorPercent:F2}%) | " +
                $"Actual: {actualPsi:F0} PSI");
            
            // EXPERIMENT 1: Fixed-weight blending.
            //
            // Theory: The original estimate may still contain useful information
            // after measured breaks are available. Retaining 10% of that estimate
            // could reduce overcorrection by the fitted curve.
            //
            // A 90% adjusted / 10% original blend placed all 9 synthetic batches
            // within +/-5%, but increased average absolute percentage error
            // from approximately 2.82% to 3.03%.
            //
            // This also worsened VM-SYN-006 from 0.28% to 3.72%, suggesting
            // that a fixed blend is not necessarily appropriate for every batch.

            double blendedPsi =
                0.90 * predictedPsi + 0.10 * originalPsi;

            double blendedErrorPercent =
                Math.Abs(blendedPsi - actualPsi) / actualPsi * 100.0;

            Console.WriteLine(
                $"  Blended: {blendedPsi:F2} PSI " +
                $"({blendedErrorPercent:F3}% error)");
            
            // EXPERIMENT 2: Measure disagreement between the fitted reference
            // strength and the expected-final strength.
            //
            // Theory: The magnitude of this disagreement may help identify
            // when the measured-break model should receive more weight.
            //
            // VM-SYN-005 has approximately 8.96% disagreement, while
            // VM-SYN-006 has approximately 30.75% disagreement.
            // The latter requires a much larger correction, which the
            // break-adjusted model handles particularly well.
            //
            // Disagreement is not itself a validated confidence measure.
            // Additional independent data would be needed to establish that.

            double fittedReferencePsi =
                result.Day28Adjustment!.ReferenceStrengthPsi;

            double expectedFinalPsi =
                batch.ExpectedFinalPsi;

            double referenceDifferencePercent =
                (fittedReferencePsi - expectedFinalPsi)
                / expectedFinalPsi * 100.0;

            double predictionDifferencePercent =
                (predictedPsi - originalPsi)
                / originalPsi * 100.0;

            Console.WriteLine(
                $"  Reference: {fittedReferencePsi:F0} PSI | " +
                $"Expected: {expectedFinalPsi:F0} PSI | " +
                $"Reference difference: {referenceDifferencePercent:+0.00;-0.00;0.00}% | " +
                $"Prediction difference: {predictionDifferencePercent:+0.00;-0.00;0.00}%");
            
            // EXPERIMENT 3: Adaptive blending based on reference disagreement.
            //
            // Theory: When the fitted reference strength is relatively close
            // to Expected final, retain a small contribution from the original
            // prediction. When disagreement is substantial, rely entirely
            // on the break-adjusted prediction.
            //
            // Current experimental rule:
            //   Difference < 10%  -> 90% adjusted + 10% original.
            //   Difference >= 10% -> 100% adjusted.
            //
            // Results on the 9 synthetic batches:
            //   - All 9 predictions fall within +/-5%.
            //   - Mean absolute percentage error decreases from
            //     approximately 2.82% to 2.65%.
            //   - VM-SYN-005 improves from 5.07% to 4.72%.
            //   - VM-SYN-006 retains approximately 0.28% error.
            //
            // LIMITATIONS:
            // The 10% threshold and blending weight were chosen after
            // examining these same synthetic batches, so the improvement
            // has not been independently validated.
            //
            // A hard threshold also creates a discontinuity in predictions.
            // A continuous weighting function may be worth investigating.
            //
            // This is NOT part of the specified M2-14 equation. If pursued,
            // it should be a separate enhancement that preserves the exact
            // 3-day and 7-day break fits and is tested on independent data.

            double referenceDifferenceMagnitude =
                Math.Abs(referenceDifferencePercent);

            double originalWeight =
                referenceDifferenceMagnitude >= 10.0 ? 0.0 : 0.10;

            double adaptivePsi =
                (1.0 - originalWeight) * predictedPsi +
                originalWeight * originalPsi;

            double adaptiveErrorPercent =
                Math.Abs(adaptivePsi - actualPsi) / actualPsi * 100.0;

            Console.WriteLine(
                $"  Adaptive: {adaptivePsi:F2} PSI " +
                $"({adaptiveErrorPercent:F3}% error) | " +
                $"Original weight: {originalWeight:P0}");
            
            // Additional diagnostics for the single batch that currently
            // fails the original M2-14 28-day accuracy assertion.
            // These values help investigate the fitted equation's inputs
            // without modifying the required calculation.

            if (batch.BatchNumber == "VM-SYN-005")
            {
                Console.WriteLine(
                    $"  B3: {batch.Breaks.Where(x => x.Day == 3).Average(x => x.StrengthPsi):F2} PSI | " +
                    $"B7: {batch.Breaks.Where(x => x.Day == 7).Average(x => x.StrengthPsi):F2} PSI");

                Console.WriteLine(
                    $"  Adjusted a: {result.Day28Adjustment!.AdjustedA:F6} | " +
                    $"Reference F: {result.Day28Adjustment.ReferenceStrengthPsi:F2}");
            }

            // Keep the original M2-14 acceptance assertion unchanged.
            // This intentionally checks the required two-break adjustment,
            // not either experimental blended prediction.
            // VM-SYN-005 currently fails at approximately 5.07%.

            if (errorPercent > 5.0)
            {
                failures.Add(
                    $"{batch.BatchNumber}: predicted {predictedPsi:F0} PSI, " +
                    $"actual {actualPsi:F0} PSI, error {errorPercent:F2}%");
            }
        }
        
        Assert.True(
            failures.Count == 0,
            $"Expected all 9 batches within ±5%. " +
            $"Failures: {string.Join("; ", failures)}");
    }
}
