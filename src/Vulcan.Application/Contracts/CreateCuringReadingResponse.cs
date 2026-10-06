using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class CreateCuringReadingResponse
{
    [Required]
    public required int Id { get; init; }

    [Required]
    public required int SpecimenId { get; init; }

    public List<WarningDTO>? Warnings { get; init; }
}
