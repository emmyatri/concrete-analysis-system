using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateBatchResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string BatchNumber { get; init; }

    [Required]
    public required List<WarningDTO> Warnings { get; init; } = [];
}
