using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed record ServiceResult<T>(
    T? Value,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationError> Warnings,
    ServiceResultKind Kind)
{
    public bool IsSuccess => Kind == ServiceResultKind.Success;

    public static ServiceResult<T> Success(
        T value,
        IReadOnlyList<ValidationError>? warnings = null) =>
        new(
            value,
            [],
            warnings ?? [],
            ServiceResultKind.Success);

    public static ServiceResult<T> Invalid(
        IReadOnlyList<ValidationError> errors) =>
        new(
            default,
            errors,
            [],
            ServiceResultKind.Invalid);

    public static ServiceResult<T> NotFound(
        IReadOnlyList<ValidationError> errors) =>
        new(
            default,
            errors,
            [],
            ServiceResultKind.NotFound);

    public static ServiceResult<T> Conflict(
        IReadOnlyList<ValidationError> errors) =>
        new(
            default,
            errors,
            [],
            ServiceResultKind.Conflict);
}
