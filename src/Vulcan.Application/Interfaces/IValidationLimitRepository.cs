using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface IValidationLimitRepository
{
    public Task<List<ValidationLimit>> GetValidationLimitsAsync();
}
