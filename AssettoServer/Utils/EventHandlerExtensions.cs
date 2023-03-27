using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Server;
using CommunityToolkit.Common.Deferred;

namespace AssettoServer.Utils;

// Taken from CommunityToolkit.Common but adapted to typed senders

/// <summary>
/// Extensions to <see cref="EventHandler{TEventArgs}"/> for Deferred Events.
/// </summary>
public static class EventHandlerExtensions
{
    /// <summary>
    /// Use to invoke an async <see cref="EventHandler{TEventArgs}"/> using <see cref="DeferredEventArgs"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="EventHandler{TEventArgs}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
    public static Task InvokeAsync<TSender, TArgs>(this EventHandler<TSender, TArgs>? eventHandler, TSender sender, TArgs eventArgs)
        where TArgs : DeferredEventArgs
    {
        return InvokeAsync(eventHandler, sender, eventArgs, CancellationToken.None);
    }

    /// <summary>
    /// Use to invoke an async <see cref="EventHandler{TEventArgs}"/> using <see cref="DeferredEventArgs"/> with a <see cref="CancellationToken"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="EventHandler{TEventArgs}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> option.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
    public static Task InvokeAsync<TSender, TArgs>(this EventHandler<TSender, TArgs>? eventHandler, TSender sender, TArgs eventArgs, CancellationToken cancellationToken)
        where TArgs : DeferredEventArgs
    {
        if (eventHandler == null)
        {
            return Task.CompletedTask;
        }

        Task[]? tasks = eventHandler.GetInvocationList()
            .OfType<EventHandler<TSender, TArgs>>()
            .Select(invocationDelegate =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                invocationDelegate(sender, eventArgs);

#pragma warning disable CS0618 // Type or member is obsolete
                EventDeferral? deferral = eventArgs.GetCurrentDeferralAndReset();

                return deferral?.WaitForCompletion(cancellationToken) ?? Task.CompletedTask;
#pragma warning restore CS0618 // Type or member is obsolete
            })
            .ToArray();

        return Task.WhenAll(tasks);
    }
}
