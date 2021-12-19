namespace LiveWeatherPlugin;

public class OpenWeatherMapException : Exception
{
    public OpenWeatherMapException()
    {
    }

    public OpenWeatherMapException(string message)
        : base(message)
    {
    }

    public OpenWeatherMapException(string message, Exception inner)
        : base(message, inner)
    {
    }
}