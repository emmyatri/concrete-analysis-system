using Vulcan.Domain.Constants;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Services;
using Vulcan.Domain.Validation;
using static Vulcan.Infrastructure.SetupUtils;

namespace Vulcan.Domain.Tests;

public class CuringReadingValidatorTests
{
    private const string _takenAtKey = "Curing.TakenAt";

    private static readonly DateTime _castAt = new(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly CuringReadingValidator _validator;

    public CuringReadingValidatorTests()
    {
        // The real M1-10 seed limits, so no ranges are repeated in the validator or the tests.
        _validator = new CuringReadingValidator(
            new LimitEvaluator(
                new FakeLimitProvider([.. LoadValidationLimits()])));
    }

    // -------------------------------------------------------------------------
    // A normal reading (VM-SYN-006, cylinder D3-1, first reading)
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithNormalReading_HasNoErrorsOrWarnings()
    {
        var outcome = _validator.Validate(CreateReading(), _castAt, _now);

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.Errors);
        Assert.Empty(outcome.Warnings);
    }

    // -------------------------------------------------------------------------
    // Time taken: on or after the cast, not in the future
    // -------------------------------------------------------------------------

    [Fact]
    public void TakenAt_AtCastTime_IsValid()
    {
        var outcome = _validator.Validate(CreateReading(takenAt: _castAt), _castAt, _now);

        Assert.DoesNotContain(outcome.Errors, error => error.Key == _takenAtKey);
    }

    [Fact]
    public void TakenAt_BeforeCast_IsRejected()
    {
        var outcome = _validator.Validate(
            CreateReading(takenAt: _castAt.AddMinutes(-1)), _castAt, _now);

        Assert.Contains(outcome.Errors, error => error.Key == _takenAtKey);
    }

    [Fact]
    public void TakenAt_InFuture_IsRejected()
    {
        var outcome = _validator.Validate(
            CreateReading(takenAt: _now.AddMinutes(1)), _castAt, _now);

        Assert.Contains(outcome.Errors, error => error.Key == _takenAtKey);
    }

    [Fact]
    public void TakenAt_Missing_IsRejected()
    {
        var reading = CreateReading();
        reading.TakenAt = default;

        var outcome = _validator.Validate(reading, _castAt, _now);

        Assert.Contains(outcome.Errors, error => error.Key == _takenAtKey);
    }

    // -------------------------------------------------------------------------
    // Ambient (room) temperature: Reject outside 0–150 °F, Warn outside 60–80 °F
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-0.1", "Reject")]
    [InlineData("0", "Warn")]
    [InlineData("59.9", "Warn")]
    [InlineData("60", "Ok")]
    [InlineData("80", "Ok")]
    [InlineData("80.1", "Warn")]
    [InlineData("150", "Warn")]
    [InlineData("150.1", "Reject")]
    public void AmbientTemp_IsCheckedAgainstBothRanges(string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(ambientTempF: decimal.Parse(value)), _castAt, _now);

        Assert.Equal(expected, ResultFor(outcome, LimitKeys.AmbientTempF));
    }

    [Fact]
    public void AmbientTemp_OutsideNormalRange_StillSavesWithWarning()
    {
        // Mockup "Curing readings": 84.2 °F is saved and flagged, not rejected.
        var outcome = _validator.Validate(CreateReading(ambientTempF: 84.2m), _castAt, _now);

        Assert.True(outcome.IsValid);
        var warning = Assert.Single(outcome.Warnings);
        Assert.Equal(LimitKeys.AmbientTempF, warning.Key);
        Assert.Contains("[60-80]", warning.Message);
    }

    // -------------------------------------------------------------------------
    // Internal temperature: Reject outside 0–200 °F (typo guard, e.g. 720 for 72)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-0.1", "Reject")]
    [InlineData("0", "Ok")]
    [InlineData("200", "Ok")]
    [InlineData("200.1", "Reject")]
    public void InternalTemp_IsChecked(string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(internalTempF: decimal.Parse(value)), _castAt, _now);

        Assert.Equal(expected, ResultFor(outcome, LimitKeys.InternalTempF));
    }

    [Theory]
    [InlineData("72", true)]
    [InlineData("72.0", true)]
    [InlineData("72.1", true)]
    [InlineData("72.12", false)]
    [InlineData("72.123", false)]
    public void InternalTemp_EnforcesOneDecimalPlace(
        string value, bool expectedValid)
    {
        var outcome = _validator.Validate(
            CreateReading(internalTempF: decimal.Parse(value)),
            _castAt,
            _now);

        var precisionErrors = outcome.Errors
            .Where(error => error.Key == LimitKeys.InternalTempF)
            .ToList();

        if (expectedValid)
        {
            Assert.Empty(precisionErrors);
        }
        else
        {
            var error = Assert.Single(precisionErrors);
            Assert.Equal("Max 1 decimal place", error.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Moisture: % of sample weight
    // Reject outside 0–15%, warn outside 2–4%.
    // The accepted range is provisional pending client confirmation.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-0.1", "Reject")]
    [InlineData("0", "Warn")]
    [InlineData("1.5", "Warn")]
    [InlineData("1.9", "Warn")]
    [InlineData("2", "Ok")]
    [InlineData("3", "Ok")]
    [InlineData("4", "Ok")]
    [InlineData("4.1", "Warn")]
    [InlineData("4.5", "Warn")]
    [InlineData("15", "Warn")]
    [InlineData("15.1", "Reject")]
    [InlineData("92", "Reject")]
    public void Moisture_IsCheckedAgainstBothRanges(
        string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(moisturePct: decimal.Parse(value)),
            _castAt,
            _now);

        Assert.Equal(expected, ResultFor(outcome, LimitKeys.MoisturePct));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("4.5")]
    public void Moisture_OutsideNormalRange_SavesWithWarning(
        string value)
    {
        var outcome = _validator.Validate(
            CreateReading(moisturePct: decimal.Parse(value)),
            _castAt,
            _now);

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.Errors);

        var warning = Assert.Single(outcome.Warnings);
        Assert.Equal(LimitKeys.MoisturePct, warning.Key);
        Assert.Contains("2-4% of sample weight", warning.Message);
    }

    // -------------------------------------------------------------------------
    // Relative humidity: 0–100% 
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("-0.1", "Reject")]
    [InlineData("0", "Ok")]
    [InlineData("100", "Ok")]
    [InlineData("100.1", "Reject")]
    public void RelativeHumidity_IsChecked(
        string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(relativeHumidityPct: decimal.Parse(value)),
            _castAt,
            _now);

        Assert.Equal(
            expected,
            ResultFor(outcome, LimitKeys.RelativeHumidityPct));
    }

    // -------------------------------------------------------------------------
    // Resistivity: more than 0. CO2: at least 0.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("0", "Reject")]
    [InlineData("0.01", "Ok")]
    [InlineData("1000", "Ok")]
    public void Resistivity_IsChecked(string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(resistivityOhmPerFt: decimal.Parse(value)), _castAt, _now);

        Assert.Equal(expected, ResultFor(outcome, LimitKeys.ResistivityOhmPerFt));
    }

    [Theory]
    [InlineData("-0.1", "Reject")]
    [InlineData("0", "Ok")]
    [InlineData("5000", "Ok")]
    public void Co2_IsChecked(string value, string expected)
    {
        var outcome = _validator.Validate(
            CreateReading(co2Ppm: decimal.Parse(value)), _castAt, _now);

        Assert.Equal(expected, ResultFor(outcome, LimitKeys.Co2Ppm));
    }

    // -------------------------------------------------------------------------
    // Every problem is reported at once, each with its key
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_WithSeveralProblems_ReturnsThemAll()
    {
        var reading = CreateReading(
            takenAt: _castAt.AddHours(-1),
            ambientTempF: 720m,
            moisturePct: 101m,
            resistivityOhmPerFt: 0m);

        var outcome = _validator.Validate(reading, _castAt, _now);

        Assert.Equal(
            [_takenAtKey, LimitKeys.AmbientTempF, LimitKeys.MoisturePct, LimitKeys.ResistivityOhmPerFt],
            outcome.Errors.Select(error => error.Key));
        Assert.Empty(outcome.Warnings);
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    /// <summary>"Reject", "Warn" or "Ok" for one key.</summary>
    private static string ResultFor(ValidationOutcome outcome, string key)
    {
        if (outcome.Errors.Any(error => error.Key == key))
        {
            return "Reject";
        }

        return outcome.Warnings.Any(warning => warning.Key == key) ? "Warn" : "Ok";
    }

    private static CuringReading CreateReading(
        DateTime? takenAt = null,
        decimal internalTempF = 78.4m,
        decimal ambientTempF = 69.5m,
        decimal moisturePct = 3.0m,
        decimal relativeHumidityPct = 96.1m,
        decimal resistivityOhmPerFt = 15.98m,
        decimal co2Ppm = 416.8m)
    {
        return new CuringReading
        {
            TakenAt = takenAt ?? _castAt.AddHours(12),
            InternalTempF = internalTempF,
            AmbientTempF = ambientTempF,
            MoisturePct = moisturePct,
            RelativeHumidityPct = relativeHumidityPct,
            ResistivityOhmPerFt = resistivityOhmPerFt,
            Co2Ppm = co2Ppm,
        };
    }

    // Supplies the real M1-10 seed limits to LimitEvaluator without a database.
    private sealed class FakeLimitProvider(params ValidationLimit[] limits)
        : ILimitProvider
    {
        public LimitSet Get(string key)
        {
            var matches = limits.Where(limit => limit.Key == key).ToList();

            if (matches.Count == 0)
            {
                throw new KeyNotFoundException(key);
            }

            return new LimitSet
            {
                Warn = matches.FirstOrDefault(limit => limit.Severity == Severity.Warn),
                Reject = matches.FirstOrDefault(limit => limit.Severity == Severity.Reject),
            };
        }
    }
}
