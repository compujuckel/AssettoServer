using System.Threading.Tasks;

namespace AssettoServer.Server.TrackParams;

public interface ITrackParamsProvider
{
    public Task InitializeAsync();
    public TrackParams? GetParamsForTrack(string track);
}
