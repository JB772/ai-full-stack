using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PromotionRepository(IDbConnectionFactory connectionFactory) : IPromotionRepository
{
    public async Task<IEnumerable<PromotionLookup>> GetAllAsync()
    {
        const string sql = "SELECT pkid, PromoCode, Topic, Description FROM Promotion2 ORDER BY PromoCode ASC";
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<PromotionLookup>(sql);
    }
}
