namespace Vulcan.Application.Contracts;

public sealed record EarlyWarningDTO(
    string Warning,
    string Field,
    double Value,
    string LimitKey,
    double Limit,
    string? Recommendation = null);
