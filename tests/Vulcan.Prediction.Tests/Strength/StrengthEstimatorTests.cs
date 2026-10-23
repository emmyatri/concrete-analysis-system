using System.IO;
using System.Linq;
using System.Text.Json;
using Vulcan.Domain.Enums;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;
using Xunit;

namespace Vulcan.Prediction.Tests.Strength;

public sealed class StrengthEstimatorTests
{
    private static readonly DateTime _castAt =
        new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static StrengthSettings CreateSettings() => new()
    {
        CementType = CementType.TypeI,
        A = 4.0,
        B = 0.85,
        CalibrationVersion = "test-v1",
        Maturity = new MaturitySettings()
    };

    private static StrengthSpecimen CreateSpecimen(
        string id,
        params (double Hours, double TemperatureF)[] readings)
    {
        return new StrengthSpecimen
        {
            SpecimenId = id,
            Readings = readings
                .Select(r => new TemperatureReading(
                    _castAt.AddHours(r.Hours),
                    r.TemperatureF))
                .ToList()
        };
    }

    private static string GetFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var path = Path.Combine(
                directory.FullName,
                "data",
                "synthetic",
                "batches.json");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate data/synthetic/batches.json");
    }

    private static (DateTime CastAt,
        CementType CementType,
        double ExpectedFinalPsi,
        StrengthSpecimen[] Specimens) LoadVmSyn006()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(GetFixturePath()));

        var root = document.RootElement;

        // Supports either a root array or an object containing "Batches".
        var batches = root.ValueKind == JsonValueKind.Array
            ? root
            : root.GetProperty("Batches");

        var batch = batches.EnumerateArray().First(
            item => item.GetProperty("BatchNumber").GetString()
                    == "VM-SYN-006");

        var castAt = batch.GetProperty("CastAt").GetDateTime();

        var cementType = Enum.Parse<CementType>(
            batch.GetProperty("CementType").GetString()!);

        var expectedFinalPsi = batch
            .GetProperty("ExpectedFinalBreakPsi")
            .GetDouble();

        var specimens = batch.GetProperty("Specimens")
            .EnumerateArray()
            .Select(specimen => new StrengthSpecimen
            {
                SpecimenId = specimen.GetProperty("Label").GetString()!,
                Readings = specimen.GetProperty("CuringReadings")
                    .EnumerateArray()
                    .Select(reading => new TemperatureReading(
                        reading.GetProperty("TakenAt").GetDateTime(),
                        reading.GetProperty("InternalTempF").GetDouble()))
                    .ToArray()
            })
            .ToArray();

        return (castAt, cementType, expectedFinalPsi, specimens);
    }

    [Fact]
    public void Estimate_NoSpecimens_ReturnsInsufficientData()
    {
        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            Array.Empty<StrengthSpecimen>(),
            CreateSettings());

        Assert.Equal("Not enough curing data yet", result.StatusMessage);
        Assert.Empty(result.DailyCurve);
        Assert.Null(result.Day3);
        Assert.Null(result.Day7);
        Assert.Null(result.Day28);
    }

    [Fact]
    public void Estimate_NoSpecimenReaches24Hours_ReturnsInsufficientData()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 70), (12, 72)),
            CreateSpecimen("S2", (0, 75), (23, 76))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Equal("Not enough curing data yet", result.StatusMessage);
        Assert.Empty(result.DailyCurve);
        Assert.Null(result.Day3);
        Assert.Null(result.Day7);
        Assert.Null(result.Day28);
    }

    [Fact]
    public void Estimate_OneReading_DoesNotQualify()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (48, 75))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Equal("Not enough curing data yet", result.StatusMessage);
        Assert.Empty(result.DailyCurve);
    }

    [Fact]
    public void Estimate_QualifyingSpecimen_Returns28DailyPoints()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Null(result.StatusMessage);
        Assert.Equal(28, result.DailyCurve.Count);

        for (int day = 1; day <= 28; day++)
        {
            var point = result.DailyCurve[day - 1];

            Assert.Equal(day, point.Day);
            Assert.Equal(day, point.EquivalentAgeDays, 8);
            Assert.True(point.EstimatedPsi > 0);
        }

        Assert.Equal(result.DailyCurve[2].EstimatedPsi,
            result.Day3!.EstimatedPsi);

        Assert.Equal(result.DailyCurve[6].EstimatedPsi,
            result.Day7!.EstimatedPsi);

        Assert.Equal(result.DailyCurve[27].EstimatedPsi,
            result.Day28!.EstimatedPsi);
    }

    [Fact]
    public void Estimate_DifferentReadingTimes_ProducesOneAveragedCurve()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1",
                (0, 73.4),
                (24, 73.4),
                (48, 73.4)),

            CreateSpecimen("S2",
                (6, 82.0),
                (30, 82.0),
                (60, 82.0))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Equal(28, result.DailyCurve.Count);

        // S1 cures at the reference temperature: 7 equivalent days.
        // S2 cures above reference temperature:
        // 7 * (82 - 14) / (73.4 - 14).
        double expectedAverageAge =
            (7.0 + 7.0 * (82.0 - 14.0) / (73.4 - 14.0)) / 2.0;

        Assert.Equal(expectedAverageAge,
            result.Day7!.EquivalentAgeDays, 8);

        double expectedPsi =
            expectedAverageAge /
            (4.0 + 0.85 * expectedAverageAge) * 6000;

        Assert.Equal(expectedPsi, result.Day7.EstimatedPsi, 8);
    }

    [Fact]
    public void Estimate_DayEstimates_IncludeBasisAndCalibrationVersion()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Equal("test-v1", result.CalibrationVersion);

        var estimates = new[]
        {
            result.Day3!,
            result.Day7!,
            result.Day28!
        };

        Assert.Equal(new[] { 3, 7, 28 },
            estimates.Select(e => e.Day));

        foreach (var estimate in estimates)
        {
            Assert.Equal("test-v1", estimate.CalibrationVersion);
            Assert.False(string.IsNullOrWhiteSpace(estimate.Basis));
            Assert.Contains("ACI 209R-92", estimate.Basis);
            Assert.Contains("TypeI", estimate.Basis);
            Assert.Contains("24 hours", estimate.Basis);
            Assert.Contains("6000 PSI", estimate.Basis);
            Assert.Contains("averaged", estimate.Basis);
        }
    }

    [Fact]
    public void Estimate_TypeI_At28EquivalentDays_ReturnsExpectedRatio()
    {
        const double expectedFinalPsi = 6000;

        // Approved initial Type I coefficients from ADR-005.
        var settings = CreateSettings() with
        {
            A = 4.0,
            B = 0.85
        };

        var specimens = new[]
        {
            CreateSpecimen("S1",
                (0, 73.4),
                (24, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            expectedFinalPsi,
            specimens,
            settings);

        Assert.NotNull(result.Day28);
        Assert.Equal(28.0, result.Day28.EquivalentAgeDays, 8);

        // Expected result from the approved ACI coefficients.
        double expectedPsi = 28.0 / (4.0 + 0.85 * 28.0)
                             * expectedFinalPsi;

        Assert.Equal(expectedPsi, result.Day28.EstimatedPsi, 6);
    }

    [Fact]
    public void Estimate_TypeIII_At28EquivalentDays_ReturnsExpectedRatio()
    {
        const double expectedFinalPsi = 6000;

        // Approved initial Type III coefficients from ADR-005.
        var settings = CreateSettings() with
        {
            CementType = CementType.TypeIII,
            A = 2.3,
            B = 0.92
        };

        var specimens = new[]
        {
            CreateSpecimen("S1",
                (0, 73.4),
                (24, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeIII,
            expectedFinalPsi,
            specimens,
            settings);

        Assert.NotNull(result.Day28);
        Assert.Equal(28.0, result.Day28.EquivalentAgeDays, 8);

        double expectedPsi = 28.0 / (2.3 + 0.92 * 28.0)
                             * expectedFinalPsi;

        Assert.Equal(expectedPsi, result.Day28.EstimatedPsi, 6);
    }

    [Fact]
    public void Estimate_MismatchedCementType_ThrowsArgumentException()
    {
        var settings = CreateSettings() with
        {
            CementType = CementType.TypeI,
            A = 4.0,
            B = 0.85
        };

        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            StrengthEstimator.Estimate(
                _castAt,
                CementType.TypeIII,
                6000,
                specimens,
                settings));

        Assert.Equal("settings", exception.ParamName);
    }

    [Fact]
    public void Estimate_VmSyn006_LoadsRealFixtureAndProducesCurve()
    {
        var fixture = LoadVmSyn006();

        Assert.Equal(CementType.TypeIII, fixture.CementType);
        Assert.Equal(6200, fixture.ExpectedFinalPsi);
        Assert.Equal(9, fixture.Specimens.Length);

        Assert.All(fixture.Specimens, specimen =>
            Assert.Equal(7, specimen.Readings.Count));

        // Approved initial Type III coefficients from ADR-005.
        // M2-20 will provide calibration settings in production.
        var settings = CreateSettings() with
        {
            CementType = CementType.TypeIII,
            A = 2.3,
            B = 0.92
        };

        var result = StrengthEstimator.Estimate(
            fixture.CastAt,
            fixture.CementType,
            fixture.ExpectedFinalPsi,
            fixture.Specimens,
            settings);

        Assert.Null(result.StatusMessage);
        Assert.Equal(28, result.DailyCurve.Count);

        Assert.NotNull(result.Day3);
        Assert.NotNull(result.Day7);
        Assert.NotNull(result.Day28);

        // M2-15: Updated regression expectations using the 14°F
        // Nurse–Saul datum using the VM-SYN-006 fixture.
        Assert.InRange(result.Day3!.EstimatedPsi, 3847, 3849);
        Assert.InRange(result.Day7!.EstimatedPsi, 5093, 5095);
        Assert.InRange(result.Day28!.EstimatedPsi, 6234, 6236);

        Assert.True(result.Day3.EstimatedPsi > 0);
        Assert.True(result.Day7.EstimatedPsi > result.Day3.EstimatedPsi);
        Assert.True(result.Day28.EstimatedPsi > result.Day7.EstimatedPsi);

        Assert.Equal("test-v1", result.CalibrationVersion);
    }

    [Fact]
    public void Estimate_ExtrapolatedCuringData_DisclosesExtrapolation()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1",
                (0, 73.4),
                (24, 73.4),
                (72, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.Null(result.StatusMessage);

        Assert.Contains(
            "the average of its available curing temperatures was assumed.",
            result.Basis);

        Assert.NotNull(result.Day28);

        Assert.Contains(
            "the average of its available curing temperatures was assumed.",
            result.Day28.Basis);
    }

    [Fact]
    public void Estimate_ACI209R92_TypeI_MoistCuring_MatchesPublishedCoefficients()
    {
        const double expectedFinalPsi = 6000;
        const double equivalentAgeDays = 7.0;

        // Reference:
        // ACI 209R-92, Table 2.2.1
        // Type I cement, moist curing: a = 4.0, b = 0.85
        //
        // WSDOT documentation reproducing the published ACI table:
        // https://www.wsdot.wa.gov/eesc/bridge/software/Documentation/PGSLibrary/8.0/tg_time_dependent_material_models.html

        var settings = CreateSettings() with
        {
            A = 4.0,
            B = 0.85
        };

        // Constant reference temperature means chronological age
        // and equivalent age are equal.
        var specimens = new[]
        {
            CreateSpecimen("S1",
                (0, 73.4),
                (24, 73.4),
                (168, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            expectedFinalPsi,
            specimens,
            settings);

        Assert.NotNull(result.Day7);
        Assert.Equal(
            equivalentAgeDays,
            result.Day7.EquivalentAgeDays,
            8);

        // ACI 209R-92 strength-development equation:
        // PSI = [t / (a + b * t)] * expectedFinalPsi
        //
        // At 7 days:
        // PSI = [7 / (4.0 + 0.85 * 7)] * 6000
        // PSI = 4221.1055...

        const double expectedPsi = 4221.105527638191;

        Assert.Equal(
            expectedPsi,
            result.Day7.EstimatedPsi,
            6);
    }

    [Fact]
    public void Estimate_WithThreeDayBreak_AdjustsCurveAndPreservesOriginal()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var settings = CreateSettings();

        // Calculate the original prediction without recorded breaks.
        var baseline = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings);

        // Simulate a measured 3-day strength 10% above prediction.
        StrengthBreakMeasurement[] breaks =
        [
            new("S1", 3, baseline.Day3!.EstimatedPsi * 1.10, false)
        ];

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings,
            breaks);

        // Preserve the original unadjusted prediction.
        Assert.Equal(
            baseline.Day28!.EstimatedPsi,
            result.Day28!.EstimatedPsi);

        // The adjusted curve must contain 28 points.
        Assert.NotNull(result.AdjustedDailyCurve);
        Assert.Equal(28, result.AdjustedDailyCurve.Count);

        // The adjusted curve should differ from the original.
        Assert.NotEqual(
            baseline.Day28.EstimatedPsi,
            result.AdjustedDailyCurve[27].EstimatedPsi);

        // The adjusted curve must pass through the recorded 3-day break.
        Assert.Equal(
            breaks[0].StrengthPsi,
            result.AdjustedDailyCurve[2].EstimatedPsi,
            precision: 6);

        // Verify the adjustment metadata.
        Assert.NotNull(result.Day28Adjustment);

        Assert.Equal(
            BreakAdjustmentStage.ThreeDay,
            result.Day28Adjustment.Stage);

        Assert.Equal(
            baseline.Day28.EstimatedPsi,
            result.Day28Adjustment.UnadjustedPsi);

        Assert.NotNull(result.Day28Adjustment.Basis);
        Assert.Contains(
            "Re-fit through the average 3-day breaks",
            result.Day28Adjustment.Basis);

        Assert.Contains(
            "using the original expected final strength",
            result.Day28Adjustment.Basis);
    }

    [Fact]
    public void Estimate_WithThreeAndSevenDayBreaks_AdjustsThroughBoth()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var settings = CreateSettings();

        var baseline = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings);

        // Use a valid curve with slightly different coefficients
        // to generate consistent measured break strengths.
        const double adjustedA = 4.5;
        const double adjustedB = 0.85;
        const double referencePsi = 6200;

        double day3Psi = 3.0 /
            (adjustedA + adjustedB * 3.0) * referencePsi;

        double day7Psi = 7.0 /
            (adjustedA + adjustedB * 7.0) * referencePsi;

        StrengthBreakMeasurement[] breaks =
        [
            new("S1", 3, day3Psi, false),
            new("S2", 7, day7Psi, false)
        ];

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings,
            breaks);

        Assert.NotNull(result.AdjustedDailyCurve);
        Assert.Equal(28, result.AdjustedDailyCurve.Count);

        // Both measured breaks must lie on the adjusted curve.
        Assert.Equal(
            day3Psi,
            result.AdjustedDailyCurve[2].EstimatedPsi,
            precision: 6);

        Assert.Equal(
            day7Psi,
            result.AdjustedDailyCurve[6].EstimatedPsi,
            precision: 6);

        Assert.NotNull(result.Day28Adjustment);

        Assert.NotNull(result.Day28Adjustment.Basis);
        Assert.Contains(
            "Re-fit through the average 3- and 7-day breaks",
            result.Day28Adjustment.Basis);

        Assert.Contains(
            $"{result.Day28Adjustment.ReferenceStrengthPsi:N0} PSI",
            result.Day28Adjustment.Basis);

        Assert.Equal(
            BreakAdjustmentStage.ThreeAndSevenDay,
            result.Day28Adjustment.Stage);

        // Preserve the unadjusted estimate for comparison.
        Assert.Equal(
            baseline.Day28!.EstimatedPsi,
            result.Day28Adjustment.UnadjustedPsi);
    }

    [Fact]
    public void Estimate_WithoutBreaks_PreservesOriginalCurve()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            CreateSettings());

        Assert.NotNull(result.AdjustedDailyCurve);
        Assert.Equal(28, result.AdjustedDailyCurve.Count);

        for (int i = 0; i < 28; i++)
        {
            Assert.Equal(
                result.DailyCurve[i].EstimatedPsi,
                result.AdjustedDailyCurve[i].EstimatedPsi);
        }

        Assert.NotNull(result.Day3Adjustment);
        Assert.NotNull(result.Day7Adjustment);
        Assert.NotNull(result.Day28Adjustment);

        Assert.Equal(
            "No break adjustment applied; original ACI 209R-92 prediction.",
            result.Day28Adjustment!.Basis);

        Assert.Equal(
            BreakAdjustmentStage.None,
            result.Day3Adjustment.Stage);

        Assert.Equal(
            BreakAdjustmentStage.None,
            result.Day7Adjustment.Stage);

        Assert.Equal(
            BreakAdjustmentStage.None,
            result.Day28Adjustment.Stage);
    }

    [Fact]
    public void Estimate_ThreeDayBreakFarFromEstimate_IncludesFallbackReasonInBasis()
    {
        var specimens = new[]
        {
            CreateSpecimen("S1", (0, 73.4), (24, 73.4))
        };

        var settings = CreateSettings();

        var baseline = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings);

        StrengthBreakMeasurement[] breaks =
        [
            new(
                "S1",
                3,
                baseline.Day3!.EstimatedPsi * 1.50,
                false)
        ];

        var result = StrengthEstimator.Estimate(
            _castAt,
            CementType.TypeI,
            6000,
            specimens,
            settings,
            breaks);

        Assert.NotNull(result.Day28Adjustment);

        Assert.Equal(
            BreakAdjustmentStage.None,
            result.Day28Adjustment.Stage);

        Assert.Equal(
            baseline.Day28!.EstimatedPsi,
            result.Day28Adjustment.AdjustedPsi);

        Assert.Equal(
            "3-day breaks are far from the estimate",
            result.Day28Adjustment.FallbackReason);

        Assert.Contains(
            "3-day breaks are far from the estimate",
            result.Day28Adjustment.Basis);
    }
}
