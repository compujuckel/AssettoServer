using System.IO;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Utils;
using AssettoServer.Utils;

namespace AssettoServer.Server.Configuration;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public class CSPServerExtraOptions
{
    private readonly ACServerConfiguration _configuration;

    public event EventHandler<ACTcpClient, WelcomeMessageSendingEventArgs>? WelcomeMessageSending;
    public event EventHandler<ACTcpClient, CSPServerExtraOptionsSendingEventArgs>? CSPServerExtraOptionsSending;

    public event EventHandler<ACTcpClient, WelcomeMessageSentEventArgs>? WelcomeMessageSent; 

    public string WelcomeMessage { get; set; }
    public string ExtraOptions { get; set; }

    public CSPServerExtraOptions(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        (WelcomeMessage, ExtraOptions) = CSPServerExtraOptionsParser.Decode(_configuration.WelcomeMessage);
        
        if (configuration.Extra.EnableCustomUpdate)
        {
            ExtraOptions += $"\r\n[EXTRA_DATA]\r\nCUSTOM_UPDATE_FORMAT = '{CSPPositionUpdate.CustomUpdateFormat}'";
        }

        if (!configuration.Extra.UseSteamAuth)
        {
            ExtraOptions += "\r\n[EXTRA_TWEAKS]\r\nVERIFY_STEAM_API_INTEGRITY = 1";
        }
        
        if (!ExtraOptions.Contains("MIN_TIME_BETWEEN_COLLISIONS"))
        {
            ExtraOptions += "\r\n[EXTRA_TWEAKS]\r\nMIN_TIME_BETWEEN_COLLISIONS = 2\r\n";
        }
    }

    internal async Task<string> GenerateWelcomeMessageAsync(ACTcpClient client)
    {
        var sb = new StringBuilder();
        sb.Append(WelcomeMessage);
        WelcomeMessageSending?.Invoke(client, new WelcomeMessageSendingEventArgs { Builder = sb });
        sb.Append(LegalNotice.WelcomeMessage);
        var welcomeMessage = sb.ToString();

        sb.Clear();
        
        sb.AppendLine(ExtraOptions);
        sb.AppendLine(_configuration.CSPExtraOptions);
        await CSPServerExtraOptionsSending.InvokeAsync(client, new CSPServerExtraOptionsSendingEventArgs { Builder = sb });
        var extraOptions = sb.ToString();

        var encodedWelcomeMessage = CSPServerExtraOptionsParser.Encode(welcomeMessage, extraOptions);

        if (_configuration.Extra.DebugWelcomeMessage)
        {
            await File.WriteAllTextAsync(Path.Join(_configuration.BaseFolder, $"debug_welcome.{client.SessionId}.txt"), encodedWelcomeMessage);
            await File.WriteAllTextAsync(Path.Join(_configuration.BaseFolder, $"debug_csp_extra_options.{client.SessionId}.ini"), extraOptions);
        }

        if (encodedWelcomeMessage.Length > 2039 && client.CSPVersion is null or < CSPVersion.V0_1_77)
        {
            client.Logger.Warning("Welcome message is too long for {Name} ({SessionId}), their game will crash. Consider setting a minimum CSP version of 0.1.77 (1937)", client.Name, client.SessionId);
        }
        
        WelcomeMessageSent?.Invoke(client, new WelcomeMessageSentEventArgs
        {
            ExtraOptions = extraOptions,
            WelcomeMessage = welcomeMessage,
            EncodedWelcomeMessage = encodedWelcomeMessage
        });
        return encodedWelcomeMessage;
    }
}
