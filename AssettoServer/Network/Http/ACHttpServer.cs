using AssettoServer.Server;
using NetCoreServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Http
{
    public class ACHttpServer : NetCoreServer.HttpServer
    {
        public ACServer Server { get; }

        public ACHttpServer(ACServer server, IPAddress address, int port) : base(address, port) 
        {
            Server = server;
        }

        protected override TcpSession CreateSession() { return new ACHttpSession(Server, this); }

        protected override void OnStarting()
        {
            Server.Log.Information("Starting HTTP server on port {0}.", Server.Configuration.HttpPort);
            base.OnStarting();
        }

        protected override void OnError(SocketError error)
        {
            Server.Log.Information("HTTP session caught an error: {0}.", error);
        }
    }
}
