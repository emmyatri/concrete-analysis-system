using Microsoft.EntityFrameworkCore;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure;

public class VulcanDbContext(DbContextOptions<VulcanDbContext> options) : DbContext(options)
{
    public DbSet<Batch> Batch { get; set; }
    public DbSet<BreakResult> BreakResult { get; set; }
    public DbSet<CementOxideAnalysis> CementOxideAnalysis { get; set; }
    public DbSet<Composition> Composition { get; set; }
    public DbSet<CuringReading> CuringReading { get; set; }
    public DbSet<RecordWarning> RecordWarning { get; set; }
    public DbSet<SpecificGravity> SpecificGravity { get; set; }
    public DbSet<Specimen> Specimen { get; set; }
    public DbSet<ValidationLimit> ValidationLimit { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VulcanDbContext).Assembly);
    }
}
