using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

public class RecordWarningConfiguration : AuditableEntityConfiguration<RecordWarning>
{
    public override void Configure(EntityTypeBuilder<RecordWarning> builder)
    {
        base.Configure(builder);

        builder.Property(x => x.EntityType)
            .HasMaxLength(40);
        builder.Property(x => x.FieldName)
            .HasMaxLength(64);
        builder.Property(x => x.LimitKey)
            .HasMaxLength(64);
        builder.Property(x => x.Message)
            .HasMaxLength(200);
    }
}
