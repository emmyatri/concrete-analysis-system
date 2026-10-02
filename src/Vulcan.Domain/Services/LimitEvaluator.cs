using System.Numerics;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;

namespace Vulcan.Domain.Services;

/// <summary>
/// Use to evaluate a value against its validation limit key
/// </summary>
/// <param name="limitProvider"></param>
public class LimitEvaluator(ILimitProvider limitProvider)
{
    /// <summary>
    /// Returns the result of checking a value against rules in the database.
    /// </summary>
    /// <typeparam name="T">The numeric kind of value</typeparam>
    /// <param name="key">The LimitKey to check rules under</param>
    /// <param name="value">A number to check against limits</param>
    /// <returns></returns>
    public LimitResult Check<T>(string key, T value) where T : INumber<T>
    {
        var limits = limitProvider.Get(key);

        // first priority: reject
        if (limits.Reject != null)
        {
            if (limits.Reject.Min.HasValue && value < T.CreateChecked(limits.Reject.Min.Value) ||
                limits.Reject.Max.HasValue && value > T.CreateChecked(limits.Reject.Max.Value))
            {
                return new LimitResult.Reject(limits.Reject.Message);
            }
        }

        // then warn
        if (limits.Warn != null)
        {
            if (limits.Warn.Min.HasValue && value < T.CreateChecked(limits.Warn.Min.Value) ||
                limits.Warn.Max.HasValue && value > T.CreateChecked(limits.Warn.Max.Value))
            {
                return new LimitResult.Warn(limits.Warn.Message);
            }
        }

        return new LimitResult.Ok();
    }
}
