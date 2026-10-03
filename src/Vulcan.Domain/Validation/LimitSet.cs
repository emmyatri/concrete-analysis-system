using Vulcan.Domain.Entities;

namespace Vulcan.Domain.Validation;

/// <summary>
/// Holds validation limits for a given key.
/// </summary>
public class LimitSet
{
    public ValidationLimit? Warn { get; init; }
    public ValidationLimit? Reject { get; init; }
}
