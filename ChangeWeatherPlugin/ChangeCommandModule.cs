using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using Qmmands;

namespace ChangeWeatherPlugin;

[RequireAdmin]
public class ChangeWeatherCommandModule : ACModuleBase
{
    [Command("cw")]
    public void ChangeWeather(int choice)
    {
        ChangeWeatherPlugin.Instance?.ProcessChoice(Context.Client, choice);
    }

    [Command("cwl")]
    public void ChangeWeatherList()
    {
        ChangeWeatherPlugin.Instance?.GetWeathers(Context.Client);
    }
}
