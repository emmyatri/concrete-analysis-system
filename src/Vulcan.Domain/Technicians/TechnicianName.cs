namespace Vulcan.Domain.Technicians;

/// <summary>
/// The rules for the technician name typed on the Start session screen (M1-42, FR-21).
/// The UI checks the same rules, but the API always checks again (NFR-6).
/// </summary>
public static class TechnicianName
{
    public const int MinLength = 2;
    public const int MaxLength = 50;

    /// <summary>
    /// Trims spaces from both ends. Null becomes an empty string.
    /// </summary>
    public static string Normalize(string? name) => (name ?? string.Empty).Trim();

    /// <summary>
    /// Returns the message to show the operator, or null when the name is fine.
    /// Checks the trimmed name, so "  J  " is too short.
    /// </summary>
    public static string? Validate(string? name)
    {
        var trimmed = Normalize(name);

        if (trimmed.Length == 0)
        {
            return "Enter your name";
        }

        if (trimmed.Length < MinLength)
        {
            return $"At least {MinLength} characters";
        }

        if (trimmed.Length > MaxLength)
        {
            return $"{MaxLength} characters max";
        }

        return null;
    }
}
