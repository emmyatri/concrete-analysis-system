namespace Vulcan.Domain.Validation;

public sealed record ValidationError(
    string Key,
    string Message);
