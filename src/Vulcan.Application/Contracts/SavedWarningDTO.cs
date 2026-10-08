using System.ComponentModel.DataAnnotations;

namespace Vulcan.Application.Contracts;

public class SavedWarningDTO
{
    [Required]
    public required int Id { get; set; }

    [Required]
    public required string EntityType { get; set; }

    [Required]
    public required int EntityId { get; set; }

    [Required]
    public required string FieldName { get; set; }

    [Required]
    public required string LimitKey { get; set; }

    [Required]
    public required double Value { get; set; }

    [Required]
    public required DateTime CreatedAtUtc { get; set; }
    
    [Required]
    public required string Message { get; set; }
}
