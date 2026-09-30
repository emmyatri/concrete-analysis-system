using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class SpecimenConfiguration : AuditableEntityConfiguration<Specimen>
{
    public override void Configure(EntityTypeBuilder<Specimen> builder)
    {
        base.Configure(builder);

        // Specimen has many CuringReadings. CuringReading has one Specimen
        builder.HasMany(x => x.CuringReadings)
            .WithOne(x => x.Specimen)
            .HasForeignKey(x => x.SpecimenId);
        builder.Property(x => x.TestAgeDays)
            .HasConversion(
                x => x.ToString(),
                x => Enum.Parse<TestAgeDays>(x));
        builder.Property(x => x.Label)
            .HasMaxLength(20);
    }
}
