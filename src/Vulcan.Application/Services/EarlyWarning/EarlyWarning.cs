using Vulcan.Domain.Entities;

namespace Vulcan.Application.Services.EarlyWarning;

public class EarlyWarning
{
    public required string Warning { get; set; }
    public required string Field { get; set; }
    public required double Value { get; set; }
    public required string LimitKey { get; set; }
    public required double Limit { get; set; }

    public static EarlyWarning FromValidationLimit(
        ValidationLimit limit,
        string field,
        double value,
        double limitValue)
        => new()
        {
            Warning = limit.Message,
            Field = field,
            Value = value,
            LimitKey = limit.Key,
            Limit = limitValue
        };

    public static EarlyWarning FromRecordWarning(RecordWarning rw)
        => new()
        {
            Warning = rw.Message,
            Field = rw.FieldName,
            Value = (double)rw.Value,
            LimitKey = rw.LimitKey,
            Limit = 0.0
        };
}
