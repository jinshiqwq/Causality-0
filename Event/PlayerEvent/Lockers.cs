using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Lockers
{
    public void Enable()
    {
        PlayerEvents.InteractedLocker += OnInteractedLocker;
    }

    public void Disable()
    {
        PlayerEvents.InteractedLocker -= OnInteractedLocker;
    }

    private void OnInteractedLocker(PlayerInteractedLockerEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null || ev.Chamber == null)
        {
            return;
        }

        Timeline.TrackLocker(ev.Chamber, ev.CanOpen);
    }
}
