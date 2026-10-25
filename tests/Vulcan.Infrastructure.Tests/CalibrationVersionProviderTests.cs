using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Calibration;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;
using Vulcan.Infrastructure.Services;

namespace Vulcan.Infrastructure.Tests;

public class CalibrationVersionProviderTests(DatabaseFixture dbFixture)
    : IClassFixture<DatabaseFixture>
{
    [Theory]
    [MemberData(nameof(CalibrationVersionTestCases))]
    public async Task Calibration_version_provider_returns_latest(Func<List<CalibrationVersion>> calibrationVersionsFactory, Func<CalibrationVersion> expectedFactory)
    {
        await using (var db = dbFixture.CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
            db.AddRange(calibrationVersionsFactory());
            await db.SaveChangesAsync();
        }

        await using (var db = dbFixture.CreateDbContext())
        {
            var provider = new CalibrationProvider(db);
            var latest = provider.Current();
            latest.Version.ShouldBe(expectedFactory().Version);
        }
    }

    public static TheoryData<Func<List<CalibrationVersion>>, Func<CalibrationVersion>> CalibrationVersionTestCases => new()
    {
        {
            () => new()
            {
                CreateCalibrationVersion("v1", DateTime.UtcNow),
                CreateCalibrationVersion("v2", DateTime.UtcNow.AddDays(2))
            },
            () => CreateCalibrationVersion("v2", DateTime.UtcNow.AddDays(2))
        },
        {
            () => new()
            {
                CreateCalibrationVersion("v1", DateTime.UtcNow),
                CreateCalibrationVersion("v2", DateTime.UtcNow.AddDays(5)),
                CreateCalibrationVersion("v3", DateTime.UtcNow.AddDays(2)),
                CreateCalibrationVersion("v4", DateTime.UtcNow.AddDays(3)),
                CreateCalibrationVersion("v5", DateTime.UtcNow.AddDays(4)),
            },
            () => CreateCalibrationVersion("v2", DateTime.UtcNow.AddDays(5))
        },
        {
            () => new()
            {
                CreateCalibrationVersion("v1", DateTime.UtcNow),
            },
            () => CreateCalibrationVersion("v1", DateTime.UtcNow)
        },
    };

    private static CalibrationVersion CreateCalibrationVersion(string version, DateTime created)
    => new()
    {
        Version = version,
        ParametersJson = new CalibrationJsonParameters(
            Base: new CalibrationBaseParameters(
                DatumTemperatureF: 24.4m,
                ReferenceTemperatureF: 74.52m,
                CementTypeCalibrations: new Dictionary<CementType, CementTypeCalibration>
                {
                    { CementType.TypeI, new CementTypeCalibration(0.4m, 0.5m) },
                    { CementType.TypeII, new CementTypeCalibration(0.4m, 0.5m) },
                    { CementType.TypeIII, new CementTypeCalibration(0.4m, 0.5m) },
                }
            ),
            Factors: []
        ),
        CreatedAtUtc = created
    };
}
