using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Configurations;

/// <summary>
/// Common database configuration for AuditableEntity fields.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class AuditableEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : AuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();
    }
}
