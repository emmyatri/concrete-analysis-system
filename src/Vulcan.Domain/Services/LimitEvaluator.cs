using System.Numerics;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Validation;

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
        var v = decimal.CreateChecked(value);

        if (limits.Reject is { } reject && IsOutside(v, reject))
        {
            return new LimitResult.Reject(reject.Message);
        }

        if (limits.Warn is { } warn && IsOutside(v, warn))
        {
            return new LimitResult.Warn(warn.Message);
        }

        return new LimitResult.Ok();
    }

    private static bool IsOutside(decimal value, ValidationLimit limit) =>
        (limit.Min is { } min && value < min) || (limit.Max is { } max && value > max);
}
