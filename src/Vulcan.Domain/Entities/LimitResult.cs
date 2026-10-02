namespace Vulcan.Domain.Entities;

/// <summary>
/// Represents the result of validating a value against saved rules.
/// </summary>
public abstract record LimitResult
{
    private LimitResult()
    {
    }

    public sealed record Ok : LimitResult;

    public sealed record Warn(string Message) : LimitResult;

    public sealed record Reject(string Message) : LimitResult;
}
