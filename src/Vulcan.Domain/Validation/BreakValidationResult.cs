namespace Vulcan.Domain.Validation;

public sealed class BreakValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];
    public IReadOnlyList<ValidationError> Warnings { get; init; } = [];
}
