using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vulcan.Domain.Calibration;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

public class CalibrationVersionConfiguration : AuditableEntityConfiguration<CalibrationVersion>
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public override void Configure(EntityTypeBuilder<CalibrationVersion> builder)
    {
        base.Configure(builder);

        builder
            .HasIndex(x => x.Version)
            .IsUnique();

        // calibration versions are de-normalized, so parameters are stored as json TEXT
        var convertor =
            new ValueConverter<CalibrationJsonParameters, string>(
                value => JsonSerializer.Serialize(
                    value, _options),
                json => JsonSerializer.Deserialize<CalibrationJsonParameters>(
                    json, _options)!
            );

        builder
            .Property(x => x.ParametersJson)
            .HasConversion(convertor)
            .HasColumnType("TEXT");
    }
}
