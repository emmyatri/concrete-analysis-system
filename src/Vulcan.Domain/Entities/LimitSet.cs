namespace Vulcan.Domain.Entities;

/// <summary>
/// Holds validation limits for a given key.
/// </summary>
public class LimitSet
{
    public ValidationLimit? Warn { get; init; }
    public ValidationLimit? Reject { get; init; }
}
