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

            double predictedPsi =
                result.AdjustedDailyCurve[27].EstimatedPsi;
            
            double actualPsi = batch.Breaks
                .Where(measurement => measurement.Day == 28)
                .Average(measurement => measurement.StrengthPsi);

            double errorPercent =
                Math.Abs(predictedPsi - actualPsi) / actualPsi * 100.0;

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
