using Vulcan.Domain.Entities;

namespace Vulcan.Domain.Interfaces;

public interface ICalibrationProvider
{
    /// <summary>
    /// Returns the latest calibration version
    /// </summary>
    /// <returns></returns>
    public CalibrationVersion Current();
}
