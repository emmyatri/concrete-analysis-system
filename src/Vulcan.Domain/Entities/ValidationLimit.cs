using Vulcan.Domain.Enums;

namespace Vulcan.Domain.Entities;

public class ValidationLimit : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
