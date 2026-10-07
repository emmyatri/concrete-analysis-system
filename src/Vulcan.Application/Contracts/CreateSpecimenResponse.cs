using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateSpecimenResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required string Label { get; init; }

    [Required]
    public required string batchNumber { get; init; }

    [Required]
    public required List<WarningDTO> Warnings { get; init; } = [];
}
