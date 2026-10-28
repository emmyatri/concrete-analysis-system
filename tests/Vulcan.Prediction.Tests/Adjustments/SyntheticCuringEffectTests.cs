using System.Text.Json;
using Vulcan.Domain.Enums;
using Vulcan.Prediction.Maturity;
using Vulcan.Prediction.Strength;

namespace Vulcan.Prediction.Tests.Adjustments;

public class SyntheticCuringEffectTests
{
    [Theory]
    [InlineData("VM-SYN-001")]
    [InlineData("VM-SYN-002")]
    [InlineData("VM-SYN-003")]
    [InlineData("VM-SYN-004")]
    [InlineData("VM-SYN-005")]
    [InlineData("VM-SYN-006")]
    [InlineData("VM-SYN-007")]
    [InlineData("VM-SYN-008")]
    [InlineData("VM-SYN-009")]
    public void Breaks_follow_actual_maturity_with_fabricated_effects_and_five_percent_scatter(string batchNumber)
    {
        using var document = LoadDataset();
        var batch = FindBatch(document, batchNumber);

        foreach (var specimen in batch.GetProperty("Specimens").EnumerateArray())
        {
            var day = int.Parse(specimen.GetProperty("TestAgeDays").GetString()![3..]);
            var readings = specimen.GetProperty("CuringReadings").EnumerateArray().ToArray();
            var castAt = batch.GetProperty("CastAt").GetDateTime();
            var day1 = readings.Single(reading => reading.GetProperty("TakenAt").GetDateTime() == castAt.AddDays(1));
            var day3 = readings.Single(reading => reading.GetProperty("TakenAt").GetDateTime() == castAt.AddDays(3));
            var loss = Math.Max(0, day1.GetProperty("MoisturePct").GetDouble() - day3.GetProperty("MoisturePct").GetDouble());
            var (ratio, scmShare) = CompositionFactors(batch);
            var waterFactor = Math.Max(0.5, 1 - 2 * Math.Max(0, ratio - 0.5));
            var moistureFactor = Math.Max(0.5, 1 - 0.12 * loss * Math.Min(day / 7.0, 1));
            var scmFactor = 1 - 0.5 * scmShare * Math.Max(0, (28 - day) / 25.0);
            var baseline = Estimate(batch, specimen).DailyCurve[day - 1].EstimatedPsi;
            var expected = baseline * waterFactor * moistureFactor * scmFactor;
            var actual = specimen.GetProperty("BreakResult").GetProperty("StrengthPsi").GetDouble();

            Assert.InRange(actual, expected * 0.95 - 0.5, expected * 1.05 + 0.5);
        }
    }

    [Fact]
    public void On_track_batch_meets_target_while_three_named_cases_have_lower_final_breaks()
    {
        using var document = LoadDataset();
        var control = FindBatch(document, "VM-SYN-001");
        Assert.InRange(FinalStrengthFraction(control), 0.95, 1.05);

        foreach (var number in new[] { "VM-SYN-002", "VM-SYN-003", "VM-SYN-006" })
        {
            Assert.True(FinalStrengthFraction(FindBatch(document, number)) < 0.95, number);
        }

        var ratios = document.RootElement.GetProperty("Batches").EnumerateArray()
            .Where(batch => CompositionFactors(batch).Ratio > 0.6)
            .Select(batch => batch.GetProperty("BatchNumber").GetString()).ToArray();
        Assert.Equal(new[] { "VM-SYN-006" }, ratios);
    }

    [Fact]
    public void Cold_curing_reduces_strength_compared_with_the_same_mix_at_reference_temperature()
    {
        using var document = LoadDataset();
        var cold = FindBatch(document, "VM-SYN-002");
        foreach (var specimen in cold.GetProperty("Specimens").EnumerateArray())
        {
            var day = int.Parse(specimen.GetProperty("TestAgeDays").GetString()![3..]);
            var coldPrediction = Estimate(cold, specimen).DailyCurve[day - 1];
            var warmPrediction = Estimate(cold, specimen, 73.4).DailyCurve[day - 1];
            Assert.True(coldPrediction.EquivalentAgeDays < warmPrediction.EquivalentAgeDays);
            Assert.True(coldPrediction.EstimatedPsi < warmPrediction.EstimatedPsi);
            Assert.All(specimen.GetProperty("CuringReadings").EnumerateArray(), reading =>
                Assert.Equal(45, reading.GetProperty("AmbientTempF").GetDouble()));
        }
    }

    [Fact]
    public void Moisture_loss_case_has_two_percentage_points_of_loss_and_lower_later_strength()
    {
        using var document = LoadDataset();
        var batch = FindBatch(document, "VM-SYN-003");
        foreach (var specimen in batch.GetProperty("Specimens").EnumerateArray())
        {
            var readings = specimen.GetProperty("CuringReadings").EnumerateArray().ToArray();
            Assert.Equal(4.5, readings[2].GetProperty("MoisturePct").GetDouble());
            Assert.Equal(2.5, readings[6].GetProperty("MoisturePct").GetDouble());
            if (specimen.GetProperty("TestAgeDays").GetString() == "Day28")
            {
                var baseline = Estimate(batch, specimen).DailyCurve[27].EstimatedPsi;
                var measured = specimen.GetProperty("BreakResult").GetProperty("StrengthPsi").GetDouble();
                Assert.InRange(measured / baseline, 0.76 * 0.95 - 0.001, 0.76 * 1.05 + 0.001);
            }
        }
    }

    [Fact]
    public void Scm_effect_delays_early_strength_but_leaves_day_28_baseline_unchanged()
    {
        using var document = LoadDataset();
        var batch = FindBatch(document, "VM-SYN-007");
        var (_, share) = CompositionFactors(batch);
        Assert.True(share > 0);
        foreach (var specimen in batch.GetProperty("Specimens").EnumerateArray())
        {
            var day = int.Parse(specimen.GetProperty("TestAgeDays").GetString()![3..]);
            var baseline = Estimate(batch, specimen).DailyCurve[day - 1].EstimatedPsi;
            var actual = specimen.GetProperty("BreakResult").GetProperty("StrengthPsi").GetDouble();
            var factor = 1 - 0.5 * share * Math.Max(0, (28 - day) / 25.0);
            Assert.InRange(actual / baseline, factor * 0.95 - 0.001, factor * 1.05 + 0.001);
            Assert.Equal(day == 28, factor == 1);
        }
    }

    private static JsonDocument LoadDataset()
    {
        return JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../data/synthetic/batches.json"))));
    }

    private static JsonElement FindBatch(JsonDocument document, string number)
    {
        return document.RootElement.GetProperty("Batches").EnumerateArray()
            .Single(batch => batch.GetProperty("BatchNumber").GetString() == number);
    }

    private static double FinalStrengthFraction(JsonElement batch)
    {
        return batch.GetProperty("Specimens").EnumerateArray()
            .Where(specimen => specimen.GetProperty("TestAgeDays").GetString() == "Day28")
            .Average(specimen => specimen.GetProperty("BreakResult").GetProperty("StrengthPsi").GetDouble())
            / batch.GetProperty("ExpectedFinalBreakPsi").GetDouble();
    }

    private static (double Ratio, double ScmShare) CompositionFactors(JsonElement batch)
    {
        var composition = batch.GetProperty("Composition");
        var gravity = composition.GetProperty("ScmType").GetString() switch
        {
            "None" => 0.0,
            "FlyAsh" or "SilicaFume" => 2.35,
            "Slag" => 2.90,
            _ => throw new InvalidOperationException("Unsupported synthetic SCM type.")
        };
        var scm = composition.GetProperty("ScmPct").GetDouble() * gravity;
        var binder = composition.GetProperty("CementPct").GetDouble() * 3.15 + scm;
        return (composition.GetProperty("WaterPct").GetDouble() / binder, scm / binder);
    }

    private static StrengthEstimateResult Estimate(JsonElement batch, JsonElement specimen, double? temperature = null)
    {
        var cementType = Enum.Parse<CementType>(batch.GetProperty("CementType").GetString()!);
        var strengthSpecimen = new StrengthSpecimen
        {
            SpecimenId = specimen.GetProperty("Label").GetString()!,
            Readings = specimen.GetProperty("CuringReadings").EnumerateArray()
                .Select(reading => new TemperatureReading(reading.GetProperty("TakenAt").GetDateTime(),
                    temperature ?? reading.GetProperty("InternalTempF").GetDouble())).ToArray()
        };
        return StrengthEstimator.Estimate(batch.GetProperty("CastAt").GetDateTime(), cementType,
            batch.GetProperty("ExpectedFinalBreakPsi").GetDouble(), [strengthSpecimen],
            new StrengthSettings
            {
                CementType = cementType,
                A = cementType == CementType.TypeIII ? 2.3 : 4.0,
                B = cementType == CementType.TypeIII ? 0.92 : 0.85,
                CalibrationVersion = "v1",
                Maturity = new MaturitySettings()
            });
    }
}
