using Vulcan.Domain.Validation;

namespace Vulcan.Application.Services;

public sealed record ServiceResult<T>(
    T? Value,
    IReadOnlyList<ValidationError> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static ServiceResult<T> Success(T value) =>
        new(value, []);

    public static ServiceResult<T> Failure(IReadOnlyList<ValidationError> errors) =>
        new(default, errors);
}
