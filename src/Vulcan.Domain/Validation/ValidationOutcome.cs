namespace Vulcan.Domain.Validation;

/// <summary>
/// What a validator found. Errors block the save; warnings save and are flagged (RecordWarning).
/// </summary>
public sealed record ValidationOutcome(
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationError> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
