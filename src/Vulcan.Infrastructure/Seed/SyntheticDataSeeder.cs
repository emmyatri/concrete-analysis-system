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

        db.Batch.AddRange(batches);
        db.SaveChanges();
    }
}
