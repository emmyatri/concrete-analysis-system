using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

public class CementOxideAnalysisConfiguration : AuditableEntityConfiguration<CementOxideAnalysis>
{
    public override void Configure(EntityTypeBuilder<CementOxideAnalysis> builder)
    {
        base.Configure(builder);

        // Batch has up to one CementOxideAnalysis. CementOxideAnalysis belongs to one Batch.
        builder.HasOne(x => x.Batch)
            .WithOne(x => x.CementOxideAnalysis)
            .HasForeignKey<CementOxideAnalysis>(x => x.BatchId);
    }
}
