using System.ComponentModel.DataAnnotations;
using Vulcan.Domain.Enums;

namespace Vulcan.Application.Contracts;

public class SpecificGravityDTO
{
    [Required]
    public required Material Material { get; set; }

    [Required]
    public required double Value { get; set; }
}
