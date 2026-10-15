using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class BatchConfiguration : AuditableEntityConfiguration<Batch>
{
    public override void Configure(EntityTypeBuilder<Batch> builder)
    {
        base.Configure(builder);

        // make batch number unique case-insensitive
        builder.Property(x => x.BatchNumber)
            // see: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/collation
            .UseCollation("NOCASE")
            .HasMaxLength(32);
        builder.HasIndex(x => x.BatchNumber)
            .IsUnique();
        // see: https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions?tabs=data-annotations#configuring-a-value-converter
        // map enum so that sqlite stores enums as strings; not integers
        builder.Property(x => x.CementType)
            .HasConversion(
                x => x.ToString(),
                x => Enum.Parse<CementType>(x));
        builder.Property(x => x.CementSurfacePh)
            .HasPrecision(18, 2)
            .IsRequired(false);

        // one batch can have many specimens
        builder.HasMany(x => x.Specimens)
            .WithOne(x => x.Batch)
            .HasForeignKey(x => x.BatchId);

        // optional foreign entities are configured in CompositionConfiguration and CementOxideAnalysis respectively
    }
}
