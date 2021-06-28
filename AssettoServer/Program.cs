using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssettoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var config = new ACServerConfiguration().FromFiles();
            ACServer server = new ACServer(config);

            await server.StartAsync();
            await Task.Delay(-1);
        }
    }
}
