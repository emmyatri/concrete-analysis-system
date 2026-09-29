namespace Vulcan.Domain.Entities;

public class RecordWarning : AuditableEntity
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LimitKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Message { get; set; } = string.Empty;
}
