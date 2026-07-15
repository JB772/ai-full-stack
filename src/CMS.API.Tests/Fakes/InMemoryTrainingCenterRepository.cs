using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>In-memory <see cref="TrainingCenterRepository"/>: three centres, returned in DisplayOrder.</summary>
public class InMemoryTrainingCenterRepository : ITrainingCenterRepository
{
    private readonly List<TrainingCenterLookup> _centers =
    [
        new() { Pkid = 1, Name = "台北", DisplayOrder = 1 },
        new() { Pkid = 2, Name = "新竹", DisplayOrder = 2 },
        new() { Pkid = 3, Name = "台中", DisplayOrder = 3 }
    ];

    public Task<IEnumerable<TrainingCenterLookup>> GetAllAsync()
        => Task.FromResult<IEnumerable<TrainingCenterLookup>>(
            _centers.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Pkid).ToList());
}
