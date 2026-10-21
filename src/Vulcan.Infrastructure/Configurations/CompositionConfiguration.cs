using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class CompositionConfiguration : AuditableEntityConfiguration<Composition>
{
    public override void Configure(EntityTypeBuilder<Composition> builder)
    {
        base.Configure(builder);

        // Batch has up to one composition. Composition has one batch.
        builder.HasOne(x => x.Batch)
            .WithOne(x => x.Composition)
            .HasForeignKey<Composition>(x => x.BatchId);
        builder.Property(x => x.ScmType)
            .HasConversion(
                x => x.ToString(),
                x => Enum.Parse<ScmType>(x));
    }
}
