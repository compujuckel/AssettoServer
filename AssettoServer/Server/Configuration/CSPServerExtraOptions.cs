using System.IO;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Utils;
using AssettoServer.Utils;
using Serilog;

namespace AssettoServer.Server.Configuration;

// https://github.com/ac-custom-shaders-patch/acc-extension-config/wiki/Misc-%E2%80%93-Server-extra-options
public class CSPServerExtraOptions
{
    private readonly ACServerConfiguration _configuration;

    public string WelcomeMessage
    {
        get => _welcomeMessage;
        set
        {
            _welcomeMessage = value;
            Encode();
        }
    }

    public string ExtraOptions
    {
        get => _extraOptions;
        set
        {
            _extraOptions = value;
            Encode();
        }
    }
    public string EncodedWelcomeMessage { get; private set; }

    private bool _hasShownMessageLengthWarning;
    private string _welcomeMessage;
    private string _extraOptions;

    public CSPServerExtraOptions(ACServerConfiguration configuration)
    {
        _configuration = configuration;
        
        (_welcomeMessage, _extraOptions) = CSPServerExtraOptionsParser.Decode(configuration.WelcomeMessage);
        EncodedWelcomeMessage = CSPServerExtraOptionsParser.Encode(_welcomeMessage, _extraOptions);
        
        WelcomeMessage += LegalNotice.WelcomeMessage;
        if (configuration.Extra.EnableCustomUpdate)
        {
            ExtraOptions += $"\r\n[EXTRA_DATA]\r\nCUSTOM_UPDATE_FORMAT = '{CSPPositionUpdate.CustomUpdateFormat}'";
        }

        if (!configuration.Extra.UseSteamAuth)
        {
            ExtraOptions += "\r\n[EXTRA_TWEAKS]\r\nVERIFY_STEAM_API_INTEGRITY = 1";
        }

        ExtraOptions += "\r\n" + configuration.CSPExtraOptions;
    }
    
    private void Encode()
    {
        EncodedWelcomeMessage = CSPServerExtraOptionsParser.Encode(_welcomeMessage, _extraOptions);
        
        if (_configuration.CSPTrackOptions.MinimumCSPVersion is null or < CSPVersion.V0_1_77
            && !_hasShownMessageLengthWarning
            && EncodedWelcomeMessage.Length > 2039)
        {
            _hasShownMessageLengthWarning = true;
            Log.Warning("Long welcome message detected. This will lead to crashes on CSP versions older than 0.1.77");
        }

        if (_configuration.Extra.DebugWelcomeMessage)
        {
            File.WriteAllText(Path.Join(_configuration.BaseFolder, "debug_welcome.txt"), EncodedWelcomeMessage);
            File.WriteAllText(Path.Join(_configuration.BaseFolder, "debug_csp_extra_options.ini"), ExtraOptions);
        }
    }
}
