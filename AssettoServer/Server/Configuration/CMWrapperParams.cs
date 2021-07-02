namespace AssettoServer.Server.Configuration
{
    public class CMWrapperParams
    {
        public bool Enabled { get; set; } = false;
        public int DetailsMode { get; set; } = 2;
        public int Port { get; set; }
        public int DownloadSpeedLimit { get; set; }
        public bool VerboseLog { get; set; }
        public string Description { get; set; }
        public bool DownloadPasswordOnly { get; set; } = true;
        public bool PublishPasswordChecksum { get; set; } = false;

    }
}