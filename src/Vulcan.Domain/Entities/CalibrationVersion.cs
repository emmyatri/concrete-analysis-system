using Vulcan.Domain.Calibration;

namespace Vulcan.Domain.Entities;

public class CalibrationVersion : AuditableEntity
{
    public required string Version { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public required CalibrationJsonParameters ParametersJson { get; set; }
}
