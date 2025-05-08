using System.Data.Common;

namespace CatMouseTougePlugin.Database;

public static class DbCommandExtensions
{
    public static void AddParameter(this DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        command.Parameters.Add(param);
    }
}
