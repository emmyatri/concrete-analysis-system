using Microsoft.EntityFrameworkCore;
using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Infrastructure.Services;

public class ValidationLimitRepository(VulcanDbContext db) : IValidationLimitRepository
{
    public async Task<List<ValidationLimit>> GetValidationLimitsAsync()
    {
        return await db.ValidationLimit.ToListAsync();
    }
}
