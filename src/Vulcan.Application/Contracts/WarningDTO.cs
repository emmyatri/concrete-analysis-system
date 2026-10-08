using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class WarningDTO
{
    [Required]
    public required string Key { get; init; }

    [Required]
    public required string Message { get; init; }
}
