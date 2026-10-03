using Vulcan.Domain.Technicians;

namespace Vulcan.Domain.Tests.Technicians;

public class TechnicianNameTests
{
    [Theory]
    [InlineData("Jo")]
    [InlineData("J. Rivera")]
    [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJ")] // 50
    public void Validate_accepts_2_to_50_characters(string name)
    {
        TechnicianName.Validate(name).ShouldBeNull();
    }

    [Theory]
    [InlineData(null, "Enter your name")]
    [InlineData("", "Enter your name")]
    [InlineData("   ", "Enter your name")]
    [InlineData("J", "At least 2 characters")]
    [InlineData("  J  ", "At least 2 characters")]
    [InlineData("ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJK", "50 characters max")] // 51
    public void Validate_rejects_names_outside_2_to_50_characters(string? name, string message)
    {
        TechnicianName.Validate(name).ShouldBe(message);
    }

    [Fact]
    public void Validate_counts_the_name_after_trimming()
    {
        // 50 letters plus spaces on both ends is still 50.
        var padded = "  " + new string('A', TechnicianName.MaxLength) + "  ";

        TechnicianName.Validate(padded).ShouldBeNull();
    }

    [Fact]
    public void Normalize_trims_spaces()
    {
        TechnicianName.Normalize(" Jordan ").ShouldBe("Jordan");
    }
}
