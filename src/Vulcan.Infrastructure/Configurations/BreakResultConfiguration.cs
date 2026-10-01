using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

public class BreakResultConfiguration : AuditableEntityConfiguration<BreakResult>
{
    public override void Configure(EntityTypeBuilder<BreakResult> builder)
    {
        base.Configure(builder);

        // BreakResult belongs to one Specimen. A Specimen has up to one BreakResult
        builder.HasOne(x => x.Specimen)
            .WithOne(x => x.BreakResult)
            .HasForeignKey<BreakResult>(x => x.SpecimenId);
    }
}
