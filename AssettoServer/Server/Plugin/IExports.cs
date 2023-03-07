using System;

namespace AssettoServer.Server.Plugin;

public interface IExports
{
    Type[] GetExportedTypes();
}
