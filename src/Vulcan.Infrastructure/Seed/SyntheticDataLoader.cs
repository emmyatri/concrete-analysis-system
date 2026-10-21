using System.Text.Json;
using System.Text.Json.Serialization;

using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Seed;

public static class SyntheticDataLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    public static List<Batch> Load()
    {
        using var stream = typeof(SyntheticDataLoader).Assembly.GetManifestResourceStream(
                               "Vulcan.Infrastructure.Seed.synthetic-batches.json")
                           ?? throw new InvalidOperationException(
                               "Could not find embedded synthetic batch data.");

        var fixture = JsonSerializer.Deserialize<SyntheticDataFixture>(stream, _jsonOptions)
                      ?? throw new JsonException("Synthetic batch data is malformed.");

        return fixture.Batches;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class SyntheticDataFixture
    {
        public List<Batch> Batches { get; set; } = [];
    }
}
