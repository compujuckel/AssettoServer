using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using CatMouseTougePlugin.Packets;

namespace CatMouseTougePlugin;

// Instance attached to each EntryCar that manages TougeSessions
// Handles sending and accepting invites and starting the TougeSession
public class EntryCarTougeSession
{
    private readonly EntryCarManager _entryCarManager;
    private readonly CatMouseTouge _plugin;
    private readonly EntryCar _entryCar;
    private readonly TougeSession.Factory _tougeSessionFactory;

    internal TougeSession? CurrentSession { get; set; }

    public EntryCarTougeSession(EntryCar entryCar, EntryCarManager entryCarManager, CatMouseTouge plugin, TougeSession.Factory tougeSessionFactory)
    {
        _entryCar = entryCar;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _tougeSessionFactory = tougeSessionFactory;
        _entryCar.ResetInvoked += OnResetInvoked;
    }

    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        CurrentSession = null;
    }

    internal EntryCar? FindNearbyCar()
    {
        EntryCar? bestMatch = null;
        const float distanceSquared = 30 * 30;
        float closestDistanceSquared = float.MaxValue; // Start with the largest possible distance

        foreach (EntryCar car in _entryCarManager.EntryCars)
        {
            ACTcpClient? carClient = car.Client;
            if (carClient != null && car != _entryCar)
            {
                // Calculate the squared distance between the two cars
                float distanceToCarSquared = Vector3.DistanceSquared(car.Status.Position, _entryCar.Status.Position);

                // Only consider the car if it's within the range
                if (distanceToCarSquared < distanceSquared)
                {
                    // If this car is closer than the previous best match, update bestMatch
                    if (distanceToCarSquared < closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceToCarSquared;
                        bestMatch = car;
                    }
                }
            }
        }

        return bestMatch;
    }

    internal List<EntryCar> FindClosestCars(int count)
    {
        var closestCars = _entryCarManager.EntryCars
            .Where(car => car.Client != null && car != _entryCar)
            .Select(car => new
            {
                Car = car,
                DistanceSquared = Vector3.DistanceSquared(car.Status.Position, _entryCar.Status.Position)
            })
            .OrderBy(x => x.DistanceSquared)
            .Take(count)
            .Select(x => x.Car)
            .ToList();

        return closestCars;
    }

    // Challenges car to a touge session.
    // Updates CurrentSession for both cars if invite is succesfully sent.
    // If session isn't active after 10 seconds, it withdraws the invite.
    // In this case it sets the CurrentSession back to null for both cars.
    internal void ChallengeCar(EntryCar car)
    {
        void Reply(string message)
        {
            CatMouseTouge.SendNotification(_entryCar.Client!, message);
        }

        var currentSession = CurrentSession;
        if (currentSession != null)
        {
            if (currentSession.IsActive)
                Reply("You are already in an active touge session.");
            else
                Reply("You have a pending session invite.");
        }
        else
        {
            if (car == _entryCar)
                Reply("You cannot invite yourself to a session.");
            else
            {
                currentSession = _plugin.GetSession(car).CurrentSession;
                if (currentSession != null)
                {
                    if (currentSession.IsActive)
                        Reply("This car is already in a touge session.");
                    else
                        Reply("This car has a pending touge session invite.");
                }
                else
                {
                    // Create a new TougeSession instance and set this for both cars.
                    currentSession = _tougeSessionFactory(_entryCar, car);
                    CurrentSession = currentSession;
                    _plugin.GetSession(car).CurrentSession = currentSession;

                    // Send messages to both players
                    _entryCar.Client?.SendChatMessage($"You have challenged {car.Client!.Name} to a touge session.");
                    car.Client?.SendPacket(new InvitePacket { InviteSenderName = _entryCar.Client!.Name! });

                    _ = Task.Delay(10000).ContinueWith(_ =>
                    {
                        if (!currentSession.IsActive)
                        {
                            CurrentSession = null;
                            _plugin.GetSession(car).CurrentSession = null;

                            var timeoutMessage = "Touge session request has timed out.";
                            _entryCar.Client?.SendChatMessage(timeoutMessage);
                            car.Client?.SendChatMessage(timeoutMessage);
                        }
                    });
                }
            }
        }
    }


}
