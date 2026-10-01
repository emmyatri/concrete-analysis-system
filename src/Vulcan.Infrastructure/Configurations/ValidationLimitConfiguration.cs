using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class ValidationLimitConfiguration : AuditableEntityConfiguration<ValidationLimit>
{
    public override void Configure(EntityTypeBuilder<ValidationLimit> builder)
    {
        base.Configure(builder);

        builder.HasIndex(x => new { x.Key, x.Severity })
            .IsUnique();
        builder.Property(x => x.Key)
            .HasMaxLength(64);
        builder.Property(x => x.Severity)
            .HasConversion(
                x => x.ToString(),
                x => Enum.Parse<Severity>(x));
        builder.Property(x => x.Unit)
            .HasMaxLength(16);
        builder.Property(x => x.Message)
            .HasMaxLength(200);
        builder.Property(x => x.Source)
            .HasMaxLength(100);
    }
}
