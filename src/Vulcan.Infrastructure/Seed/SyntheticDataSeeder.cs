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

        /* TODO(SCRUM-105): Reconcile synthetic seeding with the audit interceptor once
        M1-22 is merged. Startup seeding will need an explicit system technician
        identity if audited saves reject records without a current technician. */
        db.Batch.AddRange(batches);
        db.SaveChanges();
    }

}
