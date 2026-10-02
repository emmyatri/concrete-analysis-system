using Vulcan.Domain.Entities;

namespace Vulcan.Domain.Interfaces;

/// <summary>
/// Use to provide ValidationLimits for a given key.
/// </summary>
public interface ILimitProvider
{
    /// <summary>
    /// Get the ValidationLimits associated with the provided key.
    /// </summary>
    /// <param name="key">The key of the limit as stored in the db</param>
    /// <returns></returns>
    public LimitSet Get(string key);
}
