using System.Data;

namespace CMS.API.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
