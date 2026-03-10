using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Verified
{
    public void Enable()
    {
        PlayerEvents.Joined += OnJoined;
    }

    public void Disable()
    {
        PlayerEvents.Joined -= OnJoined;
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        if (ev.Player == null || ev.Player.IsDummy)
        {
            return;
        }

        Timeline.TrackActor(ev.Player.ReferenceHub);
    }
}
