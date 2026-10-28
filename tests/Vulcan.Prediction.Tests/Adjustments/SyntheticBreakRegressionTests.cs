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
    public void ThreeDayBreaks_ProduceExpectedSevenDayPredictionsForAllBatches()
    {
        var batches = LoadSyntheticBatches();

        Assert.Equal(9, batches.Count);

        // Baseline predictions captured from the current 3-day adjustment model.
        // These verify regression consistency, not real-world prediction accuracy.
        var expected7DayPsi = new Dictionary<string, double>
        {
            ["VM-SYN-001"] = 4339,
            ["VM-SYN-002"] = 2139,
            ["VM-SYN-003"] = 4221,
            ["VM-SYN-004"] = 4058,
            ["VM-SYN-005"] = 4021,
            ["VM-SYN-006"] = 4279,
            ["VM-SYN-007"] = 3880,
            ["VM-SYN-008"] = 3945,
            ["VM-SYN-009"] = 4168
        };

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

            Assert.Equal(
                BreakAdjustmentStage.ThreeDay,
                result.Day7Adjustment.Stage);

            double predictedPsi =
                result.AdjustedDailyCurve[6].EstimatedPsi;

            Assert.True(
                expected7DayPsi.TryGetValue(batch.BatchNumber, out double expectedPsi),
                $"No pinned 7-day prediction for {batch.BatchNumber}");

            Assert.InRange(
                predictedPsi,
                expectedPsi - 1.0,
                expectedPsi + 1.0);

            // The adjusted curve must pass through the recorded 3-day break.
            double measuredDay3Psi = knownBreaks
                .Average(measurement => measurement.StrengthPsi);

            double fittedDay3Psi =
                result.AdjustedDailyCurve[2].EstimatedPsi;

            Assert.Equal(measuredDay3Psi, fittedDay3Psi, precision: 6);
        }
    }

    [Fact]
    public void ThreeAndSevenDayBreaks_ProduceExpected28DayPredictionsForAllBatches()
    {
        var batches = LoadSyntheticBatches();

        Assert.Equal(9, batches.Count);

        // These verify calculation consistency, not real-world prediction accuracy.
        var expected28DayPsi = new Dictionary<string, double>
        {
            ["VM-SYN-001"] = 6226,
            ["VM-SYN-002"] = 4402,
            ["VM-SYN-003"] = 3749,
            ["VM-SYN-004"] = 5661,
            ["VM-SYN-005"] = 5455,
            ["VM-SYN-006"] = 4781,
            ["VM-SYN-007"] = 5840,
            ["VM-SYN-008"] = 5679,
            ["VM-SYN-009"] = 4906
        };

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

            Assert.True(
                expected28DayPsi.TryGetValue(batch.BatchNumber, out double expectedPsi),
                $"No pinned 28-day prediction for {batch.BatchNumber}");

            Assert.InRange(
                predictedPsi,
                expectedPsi - 1.0,
                expectedPsi + 1.0);

            double measuredDay3Psi = knownBreaks
                .Where(measurement => measurement.Day == 3)
                .Average(measurement => measurement.StrengthPsi);

            double measuredDay7Psi = knownBreaks
                .Where(measurement => measurement.Day == 7)
                .Average(measurement => measurement.StrengthPsi);

            double fittedDay3Psi =
                result.AdjustedDailyCurve[2].EstimatedPsi;

            double fittedDay7Psi =
                result.AdjustedDailyCurve[6].EstimatedPsi;

            Assert.Equal(measuredDay3Psi, fittedDay3Psi, precision: 6);
            Assert.Equal(measuredDay7Psi, fittedDay7Psi, precision: 6);
        }
    }
}
