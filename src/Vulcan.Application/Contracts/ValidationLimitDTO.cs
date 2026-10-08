using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class ValidationLimitDTO
{
    [Required]
    public required string Key { get; set; }

    [Required]
    public required string Severity { get; set; }

    [Required]
    public required double? Min { get; set; }

    [Required]
    public required double? Max { get; set; }

    [Required]
    public required string Unit { get; set; }

    [Required]
    public required string Message { get; set; }

    [Required]
    public required string Source { get; set; }
}
