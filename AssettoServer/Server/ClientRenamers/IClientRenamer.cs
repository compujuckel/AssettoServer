using System.Threading.Tasks;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.ClientRenamers;

public interface IClientRenamer
{
    public ValueTask<string?> RenameAsync(ACTcpClient client);
}
