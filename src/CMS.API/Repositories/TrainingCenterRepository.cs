using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class TrainingCenterRepository(IDbConnectionFactory connectionFactory) : ITrainingCenterRepository
{
    public async Task<IEnumerable<TrainingCenterLookup>> GetAllAsync()
    {
        const string sql = "SELECT pkid, Name, DisplayOrder FROM TrainingCenter ORDER BY DisplayOrder ASC, pkid ASC";
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<TrainingCenterLookup>(sql);
    }
}
