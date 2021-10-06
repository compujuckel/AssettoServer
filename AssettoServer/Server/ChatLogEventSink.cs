using System;
using System.Linq;
using AssettoServer.Network.Packets.Shared;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace AssettoServer.Server
{
    public class ChatLogEventSink : ILogEventSink
    {
        private readonly ACServer _server;
        
        private readonly IFormatProvider _formatProvider;

        public ChatLogEventSink(ACServer server, IFormatProvider formatProvider)
        {
            _server = server;
            _formatProvider = formatProvider;
        }
        
        public void Emit(LogEvent logEvent)
        {
            string message = logEvent.RenderMessage(_formatProvider);

            foreach (var client in _server.EntryCars.Where(car => car.Client != null && car.Client.HasSentFirstUpdate && car.Client.IsChatLogEnabled).Select(car => car.Client))
            {
                client.SendPacket(new ChatMessage { SessionId = 255, Message = message });
            }
        }
    }

    public static class ChatLogEventSinkExtensions
    {
        public static LoggerConfiguration ChatLog(this LoggerSinkConfiguration loggerConfiguration,
            ACServer server,
            IFormatProvider formatProvider = null)
        {
            return loggerConfiguration.Sink(new ChatLogEventSink(server, formatProvider));
        }
    }
}