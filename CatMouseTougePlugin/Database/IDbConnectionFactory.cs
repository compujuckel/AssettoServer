using System.Data.Common;

namespace CatMouseTougePlugin.Database;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
    Task InitializeDatabase();
}
