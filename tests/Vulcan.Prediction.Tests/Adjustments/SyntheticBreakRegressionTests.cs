using System.Text.Json;
using Xunit;

namespace Vulcan.Prediction.Tests.Adjustments;

public class SyntheticBreakRegressionTests
{
    [Fact]
    public void SyntheticDataset_HasNineCompleteBatches()
    {
        string path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "../../../../../data/synthetic/batches.json"));

        using JsonDocument document =
            JsonDocument.Parse(File.ReadAllText(path));

        JsonElement batches =
            document.RootElement.GetProperty("Batches");

        Assert.Equal(9, batches.GetArrayLength());

        foreach (JsonElement batch in batches.EnumerateArray())
        {
            JsonElement specimens = batch.GetProperty("Specimens");

            Assert.Equal(9, specimens.GetArrayLength());

            foreach (string age in new[] { "Day3", "Day7", "Day28" })
            {
                JsonElement[] matching = specimens
                    .EnumerateArray()
                    .Where(s =>
                        s.GetProperty("TestAgeDays").GetString() == age)
                    .ToArray();

                Assert.Equal(3, matching.Length);

                Assert.All(matching, specimen =>
                {
                    Assert.True(
                        specimen.GetProperty("BreakResult")
                            .GetProperty("StrengthPsi")
                            .GetDouble() > 0);

                    Assert.Equal(
                        7,
                        specimen.GetProperty("CuringReadings")
                            .GetArrayLength());
                });
            }
        }
    }
}
