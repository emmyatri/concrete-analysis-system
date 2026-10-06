using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Seed;

public static class SyntheticDataSeeder
{
    public static void Seed(VulcanDbContext db)
    {
        // Synthetic demo data is only loaded when there are no existing batches.
        if (db.Batch.Any())
        {
            return;
        }

        var batches = SyntheticDataLoader.Load();

        foreach (var batch in batches)
        {
            batch.IsSynthetic = true;

            foreach (var specimen in batch.Specimens)
            {
                specimen.IsSynthetic = true;
            }

            SetSyntheticAuditFields(batch);
        }

        /* TODO(SCRUM-105): Reconcile synthetic seeding with the audit interceptor once
        M1-22 is merged. Startup seeding will need an explicit system technician
        identity if audited saves reject records without a current technician. */
        db.Batch.AddRange(batches);
        db.SaveChanges();
    }

    private static void SetSyntheticAuditFields(Batch batch)
    {
        const string syntheticUser = "Synthetic data";

        SetCreated(batch, batch.CastAt, syntheticUser);

        if (batch.Composition is not null)
        {
            SetCreated(batch.Composition, batch.CastAt, syntheticUser);
        }

        if (batch.CementOxideAnalysis is not null)
        {
            SetCreated(batch.CementOxideAnalysis, batch.CastAt, syntheticUser);
        }

        foreach (var specimen in batch.Specimens)
        {
            SetCreated(specimen, batch.CastAt, syntheticUser);

            if (specimen.BreakResult is not null)
            {
                SetCreated(
                    specimen.BreakResult,
                    specimen.BreakResult.BrokenAt,
                    syntheticUser);
            }

            foreach (var reading in specimen.CuringReadings)
            {
                SetCreated(reading, reading.TakenAt, syntheticUser);
            }
        }
    }

    private static void SetCreated(
        AuditableEntity entity,
        DateTime createdAtUtc,
        string createdBy)
    {
        entity.CreatedAtUtc = createdAtUtc;
        entity.CreatedBy = createdBy;
    }
}
