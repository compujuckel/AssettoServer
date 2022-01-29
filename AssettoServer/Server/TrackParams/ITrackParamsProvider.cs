using System.Threading.Tasks;

namespace AssettoServer.Server.TrackParams
{
    public interface ITrackParamsProvider
    {
        public Task Initialize();
        public TrackParams? GetParamsForTrack(string track);
    }
}