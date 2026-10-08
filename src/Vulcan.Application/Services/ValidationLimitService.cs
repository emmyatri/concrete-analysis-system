using Vulcan.Application.Interfaces;
using Vulcan.Domain.Entities;

namespace Vulcan.Application.Services;

public class ValidationLimitService(IValidationLimitRepository validationLimitRepository)
{
    public async Task<List<ValidationLimit>> GetValidationLimitsAsync() => await validationLimitRepository.GetValidationLimitsAsync();
}
