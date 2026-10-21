using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;
using Vulcan.Domain.Interfaces;
using Vulcan.Domain.Technicians;

namespace Vulcan.Infrastructure.Interceptors;

public sealed class AuditSaveChangesInterceptor(
    ICurrentTechnician currentTechnician,
    IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampRecords(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StampRecords(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void StampRecords(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var entries = context.ChangeTracker.Entries<AuditableEntity>()
            .Where(entry => entry.State is EntityState.Added
                or EntityState.Modified)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var name = currentTechnician.Name;
        var error = TechnicianName.Validate(name);

        if (error is not null)
        {
            throw new InvalidOperationException(
                $"Cannot save records without a valid technician name: {error}.");
        }

        var technician = TechnicianName.Normalize(name);
        var now = clock.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                {
                    entry.Entity.CreatedBy = technician;
                    entry.Entity.CreatedAtUtc = now;
                }

                entry.Entity.ModifiedBy = null;
                entry.Entity.ModifiedAtUtc = null;
            }
            else
            {
                var createdBy = entry.Property(record => record.CreatedBy);
                var createdAt = entry.Property(record => record.CreatedAtUtc);

                createdBy.CurrentValue = createdBy.OriginalValue;
                createdAt.CurrentValue = createdAt.OriginalValue;
                createdBy.IsModified = false;
                createdAt.IsModified = false;

                entry.Entity.ModifiedBy = technician;
                entry.Entity.ModifiedAtUtc = now;
                entry.Property(record => record.ModifiedBy).IsModified = true;
                entry.Property(record => record.ModifiedAtUtc).IsModified = true;
            }
        }
    }
}
