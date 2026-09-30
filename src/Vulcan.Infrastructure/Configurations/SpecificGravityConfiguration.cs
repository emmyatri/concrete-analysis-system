using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class SpecificGravityConfiguration : IEntityTypeConfiguration<SpecificGravity>
{
    public void Configure(EntityTypeBuilder<SpecificGravity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
        builder.Property(x => x.Material)
            .HasConversion(
                x => x.ToString(),
                x => Enum.Parse<Material>(x));
    }
}
