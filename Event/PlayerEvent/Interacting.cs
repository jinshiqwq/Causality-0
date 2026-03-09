using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Interacting
{
    public void Enable()
    {
        PlayerEvents.InteractedDoor += OnInteractedDoor;
    }

    public void Disable()
    {
        PlayerEvents.InteractedDoor -= OnInteractedDoor;
    }

    private void OnInteractedDoor(PlayerInteractedDoorEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null || ev.Door?.Base == null)
        {
            return;
        }

        Timeline.TrackInteract(ev.Player.ReferenceHub.PlayerId, ev.Door.Base.DoorId, 1, ev.CanOpen);
    }
}