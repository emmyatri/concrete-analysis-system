using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Seed;

public static class SyntheticDataSeeder
{
    public static void Seed(VulcanDbContext db)
    {
        // DEMO HOTFIX (M1 presentation): load the demo set unless it's already there, even if real
        // batches exist. Revert to "only into an empty database" (M1-24) after the demo.
        if (db.Batch.Any(b => b.IsSynthetic))
        {
            return;
        }

        // Skip any demo batch whose number is already taken, so startup can't hit the unique index.
        var existingNumbers = db.Batch
            .Select(b => b.BatchNumber.ToUpper())
            .ToHashSet();

        var batches = SyntheticDataLoader.Load()
            .Where(b => !existingNumbers.Contains(b.BatchNumber.ToUpperInvariant()))
            .ToList();

        foreach (var batch in batches)
        {
            batch.IsSynthetic = true;

            foreach (var specimen in batch.Specimens)
            {
                specimen.IsSynthetic = true;
            }

            SetSyntheticAuditFields(batch);
        }

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
