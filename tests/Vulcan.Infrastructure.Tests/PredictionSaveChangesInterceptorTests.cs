using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vulcan.Application.Interfaces;
using Vulcan.Application.Services;
using Vulcan.Domain.Calibration;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Domain.Interfaces;
using Vulcan.Infrastructure.Interceptors;
using Vulcan.Infrastructure.Seed;
using Vulcan.Prediction.Adjustments;
using Vulcan.Prediction.Strength;

namespace Vulcan.Infrastructure.Tests;

public sealed class PredictionSaveChangesInterceptorTests()
{
    private static readonly ILogger<PredictionSaveChangesInterceptor> _nullLogger = new NullLogger<PredictionSaveChangesInterceptor>();

    private sealed class MockStrengthEstimator(StrengthEstimateResult result) : IStrengthEstimateService
    {
        public StrengthEstimateResult Estimate(
            DateTime castAt,
            CementType cementType,
            double expectedFinalBreakPsi,
            IReadOnlyList<StrengthSpecimen> specimens,
            StrengthSettings settings,
            IReadOnlyList<StrengthBreakMeasurement>? breakMeasurements = null)
        => result;
    }

    private sealed class MockCalibrationProvider(CalibrationVersion cv) : ICalibrationProvider
    {
        public CalibrationVersion Current() => cv;
    }

    private sealed class TestTechnician() : ICurrentTechnician
    {
        public string? Name { get; set; } = "Joe";
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private static DbContextOptions<VulcanDbContext> GetDbOptions(
        SqliteConnection conn,
        StrengthEstimateResult estimate,
        CalibrationVersion calibration,
        IStrengthEstimateService? strengthEstimate = null,
        bool enableIntercept = true)
    {
        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(conn);

        if (enableIntercept)
        {
            return opts.AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(calibration),
                    strengthEstimate ?? new MockStrengthEstimator(estimate),
                    _nullLogger)
            ).Options;
        }
        return opts.Options;
    }

    private StrengthEstimateResult MakeEstimate(
        bool setDay3 = false,
        bool setDay3Adjust = false,
        bool setDay7 = false,
        bool setDay7Adjust = false,
        bool setDay28 = false,
        bool setDay28Adjust = false
    )
    => new()
    {
        DailyCurve = [],
        Day3 = setDay3 ? new StrengthDayEstimate(3, 3, 4500, "", "v1") : null,
        Day3Adjustment = setDay3Adjust ? new BreakAdjustmentResult(BreakAdjustmentStage.None, 2500, 2600, 0.431, 3500, "nil") : null,
        Day7 = setDay7 ? new StrengthDayEstimate(7, 7, 4500, "", "v1") : null,
        Day7Adjustment = setDay7Adjust ? new BreakAdjustmentResult(BreakAdjustmentStage.ThreeDay, 2500, 2600, 0.431, 3500, "nil") : null,
        Day28 = setDay28 ? new StrengthDayEstimate(28, 28, 4500, "", "v1") : null,
        Day28Adjustment = setDay28Adjust ? new BreakAdjustmentResult(BreakAdjustmentStage.ThreeAndSevenDay, 2500, 2600, 0.431, 3500, "nil") : null,
        Basis = "Once upon a time",
        CalibrationVersion = "v1",
        StatusMessage = null
    };

    // ------------------------------------------------------------------------
    // tests with the mock StrengthEstimateService
    // ------------------------------------------------------------------------

    private CalibrationVersion _calibrationVersion = new()
    {
        Version = "v1",
        ParametersJson = new CalibrationJsonParameters(
            Base: new CalibrationBaseParameters(
                DatumTemperatureF: 14.0m,
                ReferenceTemperatureF: 73.4m,
                CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>
                {
                    { CementType.TypeI, new CementTypeCalibration(0.4m, 0.85m) },
                    { CementType.TypeII, new CementTypeCalibration(0.4m, 0.85m) },
                    { CementType.TypeIII, new CementTypeCalibration(2.3m, 0.92m) },
                }
            ),
            Factors: []
        )
    };

    [Fact]
    public async Task Updating_a_break_result_triggers_prediction()
    {
        var estimate = MakeEstimate(
            setDay3: true,
            setDay3Adjust: true
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);

            var batch = new Batch
            {
                BatchNumber = "B",
                IsSynthetic = true,
                CementType = CementType.TypeII,
                Specimens =
                {
                    new Specimen
                    {
                        Label = "LBL",
                        TestAgeDays = TestAgeDays.Day3,
                        BreakResult = new BreakResult
                        {
                            StrengthPsi = 3100
                        }
                    }
                }
            };

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var br = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.BreakResult)
                .First()
                .Specimens.First().BreakResult;
            br.ShouldNotBeNull().StrengthPsi = 3200;

            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            // one added for day 3
            db.Prediction.Count().ShouldBe(3);
            var prediction = db.Prediction.FirstOrDefault(p => p.HorizonDays == 3).ShouldNotBeNull();
            prediction.CalibrationVersion.ShouldBe("v1");
            prediction.HorizonDays.ShouldBe(3);
            prediction.EstimatedPsi.ShouldBe(2600);
            prediction.UnadjustedPsi.ShouldBe(2500);
            prediction.AdjustmentStage.ShouldBe(AdjustmentStage.None);
        }
    }

    [Fact]
    public async Task Updating_a_curing_reading_triggers_prediction()
    {
        var estimate = MakeEstimate(
            setDay7: true,
            setDay7Adjust: true
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);

            var batch = new Batch
            {
                BatchNumber = "B",
                IsSynthetic = true,
                CementType = CementType.TypeII,
                Specimens =
                {
                    new Specimen
                    {
                        Label = "LBL",
                        TestAgeDays = TestAgeDays.Day3,
                        CuringReadings =
                        [
                            new CuringReading
                            {
                                InternalTempF = 12
                            }
                        ]
                    }
                }
            };

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            cr.ShouldNotBeNull().InternalTempF = 2_500_000_000;

            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            // one added for day 7
            db.Prediction.Count().ShouldBe(3);
            var prediction = db.Prediction.FirstOrDefault(p => p.HorizonDays == 7).ShouldNotBeNull();
            prediction.CalibrationVersion.ShouldBe("v1");
            prediction.EstimatedPsi.ShouldBe(2600);
            prediction.UnadjustedPsi.ShouldBe(2500);
            prediction.AdjustmentStage.ShouldBe(AdjustmentStage.ThreeDay);
        }
    }

    [Fact]
    public async Task Prediction_keeps_successful_predictions()
    {
        var estimate = MakeEstimate(); // no predictions made
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 12
                        }
                    ]
                }
            }
        };

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                _calibrationVersion,
                new CalibrationVersion
                {
                    Version = "v0",
                    ParametersJson = new CalibrationJsonParameters(
                        Base: new CalibrationBaseParameters(
                            DatumTemperatureF: 13.0m,
                            ReferenceTemperatureF: 103m,
                            CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>()
                            {
                                { CementType.TypeII, new CementTypeCalibration(0.0m, 0.0m) }
                            }
                        ),
                        Factors: []
                    )
                });

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        // add predictions
        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 3,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 7,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 28,
                });
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            // trigger refresh
            cr.ShouldNotBeNull().InternalTempF = 2_500_000_000;
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            db.Prediction.Count().ShouldBe(3);
            db.Prediction.Select(p => p.EstimatedPsi).ShouldAllBe(e => e == null);
        }
    }

    [Fact]
    public async Task Prediction_overwrites_updated_predictions()
    {
        var estimate = MakeEstimate(
            setDay3: true,
            setDay7: true,
            setDay28: true
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 12
                        }
                    ]
                }
            }
        };

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                _calibrationVersion,
                new CalibrationVersion
                {
                    Version = "v0",
                    ParametersJson = new CalibrationJsonParameters(
                        Base: new CalibrationBaseParameters(
                            DatumTemperatureF: 13.0m,
                            ReferenceTemperatureF: 103m,
                            CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>()
                            {
                                { CementType.TypeII, new CementTypeCalibration(0.0m, 0.0m) }
                            }
                        ),
                        Factors: []
                    )
                });

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        // add predictions
        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v1",
                    HorizonDays = 3,
                    EstimatedPsi = 0,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v1",
                    HorizonDays = 7,
                    EstimatedPsi = 10,
                    UnadjustedPsi = 25.03m,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 28,
                    EstimatedPsi = 50,
                });
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            // trigger refresh
            cr.ShouldNotBeNull().InternalTempF = 2_500_000_000;
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.ShouldAllBe(p => p.CalibrationVersion == "v1");
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Select(p => p.EstimatedPsi).ShouldBeSubsetOf([4500, 4500, 4500]);
        }
    }

    [Fact]
    public async Task Prediction_overwrites_and_adds_and_keeps_updated_predictions()
    {
        var estimate = MakeEstimate(
            setDay3: true, // to update
                           // day 7 to set null
            setDay28: true // to be added
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 12
                        }
                    ]
                }
            }
        };

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                _calibrationVersion,
                new CalibrationVersion
                {
                    Version = "v0",
                    ParametersJson = new CalibrationJsonParameters(
                        Base: new CalibrationBaseParameters(
                            DatumTemperatureF: 13.0m,
                            ReferenceTemperatureF: 103m,
                            CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>()
                            {
                                { CementType.TypeII, new CementTypeCalibration(0.0m, 0.0m) }
                            }
                        ),
                        Factors: []
                    )
                });

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        // add predictions
        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v1",
                    HorizonDays = 3,
                    EstimatedPsi = 0,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 7,
                    EstimatedPsi = 50,
                });
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            // trigger refresh
            cr.ShouldNotBeNull().InternalTempF = 2_500_000_000;
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.ShouldAllBe(p => p.CalibrationVersion == "v1");
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Count(p => p.EstimatedPsi == 4500).ShouldBe(2);
        }
    }

    [Fact]
    public async Task Prediction_falls_through_to_audit()
    {
        var estimate = MakeEstimate(
            setDay3: true,
            setDay7: true
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var now = DateTime.UtcNow;
        var technician = new TestTechnician();
        var clock = new TestClock(now);
        var options = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(_calibrationVersion),
                    new MockStrengthEstimator(estimate),
                    _nullLogger),
                new AuditSaveChangesInterceptor(
                    technician, clock)
            )
            .Options;

        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 12
                        }
                    ]
                }
            }
        };

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                _calibrationVersion,
                new CalibrationVersion
                {
                    Version = "v0",
                    ParametersJson = new CalibrationJsonParameters(
                        Base: new CalibrationBaseParameters(
                            DatumTemperatureF: 13.0m,
                            ReferenceTemperatureF: 103m,
                            CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>()
                            {
                                { CementType.TypeII, new CementTypeCalibration(0.0m, 0.0m) }
                            }
                        ),
                        Factors: []
                    )
                });

            db.Add(batch);

            await db.SaveChangesAsync();
        }

        // use interceptors here
        await using (var db = new VulcanDbContext(options))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            // trigger refresh
            cr.ShouldNotBeNull().InternalTempF = 2_500_000_000;
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.ShouldAllBe(p => p.CalibrationVersion == "v1");
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Count(p => p.EstimatedPsi == 4500).ShouldBe(2);

            predictions.ForEach(it =>
            {
                it.CreatedBy.ShouldBe("Joe");
                it.CreatedAtUtc.ShouldBe(now);
                it.ModifiedAtUtc.ShouldBeNull();
                it.ModifiedBy.ShouldBeNull();
            });
        }

        technician.Name = "Bob";
        var modifiedAt = DateTime.UtcNow.AddHours(1);
        clock.UtcNow = modifiedAt;

        // and here
        await using (var db = new VulcanDbContext(options))
        {
            var cr = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .First()
                .Specimens
                .First()
                .CuringReadings
                .First();
            // trigger refresh
            cr.ShouldNotBeNull().InternalTempF = 400.3120951093m;
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            // should be the same
            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.ShouldAllBe(p => p.CalibrationVersion == "v1");
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Count(p => p.EstimatedPsi == 4500).ShouldBe(2);

            predictions.ForEach(it =>
            {
                it.CreatedBy.ShouldBe("Joe");
                it.CreatedAtUtc.ShouldBe(now);
                // produced predictions did not change, so they should not be modified
                it.ModifiedBy.ShouldBeNull();
                it.ModifiedAtUtc.ShouldBeNull();
            });
            // was not created with interceptor enabled
            db.CuringReading.Single().ShouldSatisfyAllConditions(
                it => it.ModifiedAtUtc.ShouldBe(modifiedAt),
                it => it.ModifiedBy.ShouldBe("Bob"));
        }
    }

    [Fact]
    public async Task Prediction_overwrites_and_adds_and_removes_updated_predictions_in_different_batches()
    {
        var estimate = MakeEstimate(
            setDay3: true, // to update
            setDay28: true // to be added
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 12
                        }
                    ]
                }
            }
        };
        var batch2 = new Batch
        {
            BatchNumber = "B2",
            IsSynthetic = true,
            CementType = CementType.TypeII,
            Specimens =
            {
                new Specimen
                {
                    Label = "LBL",
                    TestAgeDays = TestAgeDays.Day3,
                    CuringReadings =
                    [
                        new CuringReading
                        {
                            InternalTempF = 20
                        }
                    ]
                }
            }
        };

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                _calibrationVersion,
                new CalibrationVersion
                {
                    Version = "v0",
                    ParametersJson = new CalibrationJsonParameters(
                        Base: new CalibrationBaseParameters(
                            DatumTemperatureF: 13.0m,
                            ReferenceTemperatureF: 103m,
                            CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>()
                            {
                                { CementType.TypeII, new CementTypeCalibration(0.0m, 0.0m) }
                            }
                        ),
                        Factors: []
                    )
                });

            db.AddRange(batch, batch2);

            await db.SaveChangesAsync();
        }

        // add predictions
        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: false)))
        {
            await db.Database.EnsureCreatedAsync();
            db.AddRange(
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v1",
                    HorizonDays = 3,
                    EstimatedPsi = 0,
                },
                new PredictionEntity
                {
                    BatchId = batch.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 7,
                    EstimatedPsi = 50,
                },
                new PredictionEntity
                {
                    BatchId = batch2.Id,
                    CalibrationVersion = "v1",
                    HorizonDays = 3,
                    EstimatedPsi = 0,
                },
                new PredictionEntity
                {
                    BatchId = batch2.Id,
                    CalibrationVersion = "v0",
                    HorizonDays = 7,
                    EstimatedPsi = 50,
                }
                );
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion)))
        {
            var crs = db.Batch
                .Include(x => x.Specimens)
                .ThenInclude(s => s.CuringReadings)
                .SelectMany(s => s.Specimens)
                .SelectMany(s => s.CuringReadings)
                .ToList();
            crs.ShouldNotBeNull().Count.ShouldBe(2);
            // trigger refresh
            crs.ForEach(cr => cr.ShouldNotBeNull().InternalTempF = 2_500_000_000);
            await Should.NotThrowAsync(() => db.SaveChangesAsync());

            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(6);
            predictions.ShouldAllBe(p => p.CalibrationVersion == "v1");
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28, 3, 7, 28]);
            predictions.Where(p => p.EstimatedPsi == 4500).ToList().Count.ShouldBe(4);
            predictions.ShouldAllBe(p => p.IsSynthetic);
        }
    }

    [Fact]
    public async Task Seed_data_has_no_conflicts_with_prediction_logic()
    {
        var estimate = MakeEstimate(
            setDay3: true, // to update
                           // day 7 to remove
            setDay28: true // to be added
        );
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        using var db = new VulcanDbContext(GetDbOptions(connection, estimate, _calibrationVersion, enableIntercept: true));
        await db.Database.EnsureCreatedAsync();
        db.Add(_calibrationVersion);
        SyntheticDataSeeder.Seed(db);

        await Should.NotThrowAsync(() => db.SaveChangesAsync());
    }

    // ------------------------------------------------------------------------
    // tests with the real StrengthEstimateService
    // ------------------------------------------------------------------------
    private readonly IStrengthEstimateService _estimator = new StrengthEstimateService();

    [Fact]
    public async Task Saved_curing_reading_creates_three_predictions()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(_calibrationVersion),
                    _estimator,
                    _nullLogger))
            .Options;
        var noInterceptor = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .Options;

        var specimen = new Specimen
        {
            Label = "LBL",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = []
        };
        var specimen2 = new Specimen
        {
            Label = "LBL1",
            TestAgeDays = TestAgeDays.Day7,
            CuringReadings = []
        };
        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            ExpectedFinalBreakPsi = 3600,
            CementType = CementType.TypeII,
            CastAt = DateTime.UtcNow,
            Specimens = [
                specimen,
                specimen2
            ]
        };

        // wait for the update trigger
        await using (var db = new VulcanDbContext(noInterceptor))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);
            db.Add(batch);
            await db.SaveChangesAsync();
        }

        // update
        await using (var db = new VulcanDbContext(opts))
        {
            db.AddRange(
                new CuringReading
                {
                    SpecimenId = specimen.Id,
                    InternalTempF = 78.8m,
                    TakenAt = DateTime.UtcNow.AddDays(4)
                },
                new CuringReading
                {
                    SpecimenId = specimen2.Id,
                    InternalTempF = 79.8m,
                    TakenAt = DateTime.UtcNow.AddDays(8)
                });
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(opts))
        {
            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Select(p => p.IsSynthetic).ShouldAllBe(it => it);
            predictions.Select(p => p.CalibrationVersion).ShouldAllBe(v => v == "v1");
        }
    }

    [Fact]
    public async Task Three_day_break_maps_adjustment_stage_correctly()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(_calibrationVersion),
                    _estimator,
                    _nullLogger))
            .Options;
        var castAt = new DateTime(
            2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var specimen = new Specimen
        {
            Label = "LBL",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = [
                new CuringReading
                {
                    InternalTempF = 73.4m,
                    TakenAt = castAt
                },
                new CuringReading
                {
                    InternalTempF = 73.4m,
                    TakenAt = castAt.AddDays(1)
                },
                ],
        };
        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            ExpectedFinalBreakPsi = 6000,
            CementType = CementType.TypeI,
            CastAt = castAt,
            Specimens = [
                specimen,
            ]
        };

        await using (var db = new VulcanDbContext(opts))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);
            db.Add(batch);
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(opts))
        {
            var prediction = db.Prediction
                .FirstOrDefault(p => p.HorizonDays == 3)
                .ShouldNotBeNull()
                .EstimatedPsi
                // bump beyond prediction
                .ShouldNotBeNull() * 1.10m;
            var brSpecimen = db.Specimen
                .Where(s => s.Label == "LBL")
                .First()
                .ShouldNotBeNull();
            brSpecimen.BreakResult = new()
            {
                StrengthPsi = (int)prediction,
                BrokenAt = DateTime.UtcNow.AddDays(4).AddHours(1.3)
            };

            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(opts))
        {
            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Select(p => p.IsSynthetic).ShouldAllBe(it => it);
            predictions.Select(p => p.CalibrationVersion).ShouldAllBe(v => v == "v1");
            predictions
                .FirstOrDefault(p => p.HorizonDays == 28)
                .ShouldNotBeNull()
                .ShouldSatisfyAllConditions(
                    it => it.AdjustmentStage.ShouldBe(AdjustmentStage.ThreeDay),
                    it => it.EstimatedPsi.ShouldNotBe(it.UnadjustedPsi),
                    it => it.Basis.ShouldContain("Re-fit through the average 3-day breaks"));
        }
    }

    [Fact]
    public async Task New_curing_reading_on_batch_with_breaks_keeps_adjustment_stage()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var castAt = new DateTime(
            2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(_calibrationVersion),
                    _estimator,
                    _nullLogger))
            .Options;
        var noInterceptor = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .Options;

        var specimen = new Specimen
        {
            Label = "LBL",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = [],
            BreakResult = new BreakResult
            {
                BrokenAt = castAt.AddDays(2.5),
                StrengthPsi = 2400
            }
        };
        var specimen2 = new Specimen
        {
            Label = "LBL1",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = [],
            BreakResult = new BreakResult
            {
                BrokenAt = castAt.AddDays(3),
                StrengthPsi = 3300
            }
        };
        var specimen3 = new Specimen
        {
            Label = "LBL2",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = [],
            BreakResult = new BreakResult
            {
                BrokenAt = castAt.AddDays(2),
                StrengthPsi = 3331
            }
        };
        var specimen4 = new Specimen
        {
            Label = "LBL3",
            TestAgeDays = TestAgeDays.Day3,
            CuringReadings = [],
            BreakResult = new BreakResult
            {
                BrokenAt = castAt.AddDays(2.3),
                StrengthPsi = 3333
            }
        };
        var batch = new Batch
        {
            BatchNumber = "B",
            IsSynthetic = true,
            ExpectedFinalBreakPsi = 6200,
            CementType = CementType.TypeI,
            CastAt = castAt,
            Specimens = [
                specimen,
                specimen2,
                specimen3,
                specimen4
            ]
        };

        await using (var db = new VulcanDbContext(opts))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);
            db.Add(batch);
            await db.SaveChangesAsync();
        }

        AdjustmentStage? adjustmentStage = null;
        // update
        await using (var db = new VulcanDbContext(opts))
        {
            db.AddRange(
                new CuringReading
                {
                    SpecimenId = specimen.Id,
                    InternalTempF = 78.8m,
                    TakenAt = castAt.AddDays(4)
                },
                new CuringReading
                {
                    SpecimenId = specimen.Id,
                    InternalTempF = 78.8m,
                    TakenAt = castAt.AddDays(4.1)
                },
                new CuringReading
                {
                    SpecimenId = specimen2.Id,
                    InternalTempF = 79.8m,
                    TakenAt = castAt.AddDays(5)
                },
                new CuringReading
                {
                    SpecimenId = specimen2.Id,
                    InternalTempF = 79.8m,
                    TakenAt = castAt.AddDays(5.1)
                },
                new CuringReading
                {
                    SpecimenId = specimen3.Id,
                    InternalTempF = 79.8m,
                    TakenAt = castAt.AddDays(6)
                },
                new CuringReading
                {
                    SpecimenId = specimen3.Id,
                    InternalTempF = 79.8m,
                    TakenAt = castAt.AddDays(6.1)
                },
                new CuringReading
                {
                    SpecimenId = specimen4.Id,
                    InternalTempF = 79.8m,
                    TakenAt = DateTime.UtcNow.AddDays(7)
                });
            await db.SaveChangesAsync();

            var first = db.Prediction.First().ShouldNotBeNull();
            adjustmentStage = first.AdjustmentStage.ShouldNotBeNull();
        }

        await using (var db = new VulcanDbContext(opts))
        {
            var predictions = db.Prediction.ToList();
            predictions.Count.ShouldBe(3);
            predictions.Select(p => p.HorizonDays).ShouldBeSubsetOf([3, 7, 28]);
            predictions.Select(p => p.IsSynthetic).ShouldAllBe(it => it);
            predictions.Select(p => p.CalibrationVersion).ShouldAllBe(v => v == "v1");
            predictions
                .Any(p => p.AdjustmentStage == adjustmentStage)
                .ShouldBeTrue();
        }
    }

    // spot check on VM-SYN-006 following StrengthEstimator tests
    [Fact]
    public async Task Seeding_batch_data_has_correct_prediction()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var opts = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new PredictionSaveChangesInterceptor(
                    _ => new MockCalibrationProvider(_calibrationVersion),
                    _estimator,
                    _nullLogger))
            .Options;
        var noInterceptor = new DbContextOptionsBuilder<VulcanDbContext>()
            .UseSqlite(connection)
            .Options;

        // wait for the update trigger
        await using (var db = new VulcanDbContext(opts))
        {
            await db.Database.EnsureCreatedAsync();
            db.Add(_calibrationVersion);
            SyntheticDataSeeder.Seed(db);
            await db.SaveChangesAsync();
        }

        await using (var db = new VulcanDbContext(opts))
        {
            var batch = db.Batch
                .Where(b => b.BatchNumber == "VM-SYN-006")
                .First();
            batch.ShouldNotBeNull();

            db.Prediction
                .Where(p => p.BatchId == batch.Id
                    && p.HorizonDays == 28)
                .First()
                .ShouldNotBeNull()
                .ShouldSatisfyAllConditions(
                    it => it.EstimatedPsi.ShouldNotBeNull().ShouldBeInRange(4732m, 4734m),
                    it => it.UnadjustedPsi.ShouldNotBeNull().ShouldBeInRange(6211m, 6213m),
                    it => it.AdjustmentStage.ShouldBe(AdjustmentStage.ThreeAndSevenDay),
                    it => it.IsSynthetic.ShouldBeTrue());
        }
    }
}
