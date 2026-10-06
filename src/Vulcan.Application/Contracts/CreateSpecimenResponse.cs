using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateSpecimenResponse
{
    [Required]
    public required double Id { get; init; }

    [Required]
    public required string Label { get; init; }

    [Required]
    public required string batchNumber { get; init; }

    public List<WarningDTO>? Warnings { get; init; }
}
