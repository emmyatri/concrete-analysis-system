using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class UpdateBatchResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    public List<WarningDTO>? Warnings { get; init; }
}
