using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

public class CuringReadingConfiguration : AuditableEntityConfiguration<CuringReading>
{
    public override void Configure(EntityTypeBuilder<CuringReading> builder)
    {
        base.Configure(builder);

        // foreign key relationship configured in SpecimenConfiguration
    }
}
