using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;

namespace Vulcan.Infrastructure.Services;

public class CalibrationProvider(VulcanDbContext db) : ICalibrationProvider
{
    public CalibrationVersion Current()
    {
        // latest by create date
        return db
            .CalibrationVersion
            .OrderByDescending(x => x.CreatedAtUtc)
            .First();
    }
}
