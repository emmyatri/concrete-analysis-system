using Vulcan.Domain.Entities;

namespace Vulcan.Application.Interfaces;

public interface ICuringRepository
{
    Task<Specimen?> GetSpecimenAsync(int specimenId);

    Task AddAsync(CuringReading reading);

    Task SaveChangesAsync();
}
