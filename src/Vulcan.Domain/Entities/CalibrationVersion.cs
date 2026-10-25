using Vulcan.Domain.Calibration;

namespace Vulcan.Domain.Entities;

public class CalibrationVersion : AuditableEntity
{
    public required string Version { get; set; }

    public string Notes = string.Empty;

    public string Method = string.Empty;

    public required CalibrationJsonParameters ParametersJson { get; set; }
}
