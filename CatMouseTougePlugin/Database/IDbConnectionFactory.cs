using System.Data.Common;

namespace TougePlugin.Database;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
    Task InitializeDatabase();
}
