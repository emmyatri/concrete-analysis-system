using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Enums;

namespace Vulcan.Infrastructure.Configurations;

public class PredictionConfiguration : AuditableEntityConfiguration<PredictionEntity>
{
    public override void Configure(EntityTypeBuilder<PredictionEntity> builder)
    {
        base.Configure(builder);

        // Prediction.Batch must be a valid Batch. One batch can have many
        // predictions
        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId);

        // Prediction.Version must be a valid CalibrationVersion. One
        // calibration version can be used in many predictions
        builder.HasOne(x => x.CalibrationVersionEntity)
            .WithMany()
            .HasForeignKey(x => x.CalibrationVersion)
            // use version instead of the id
            .HasPrincipalKey(cv => cv.Version)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.AdjustmentStage)
            .HasConversion(
                x => x.ToString(),
                x => x == null
                    ? null
                    : Enum.Parse<AdjustmentStage>(x));
    }
}
