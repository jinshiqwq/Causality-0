using Causality0.Core;
using Interactables.Interobjects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent
{

    public sealed class Interacting
    {
        public void Enable()
        {
            PlayerEvents.InteractedDoor += OnInteractedDoor;
            PlayerEvents.InteractedElevator += OnInteractedElevator;
        }

        public void Disable()
        {
            PlayerEvents.InteractedElevator -= OnInteractedElevator;
            PlayerEvents.InteractedDoor -= OnInteractedDoor;
        }

        private void OnInteractedDoor(PlayerInteractedDoorEventArgs ev)
        {
            if (!Timeline.IsRec || ev.Player == null || ev.Door?.Base == null)
            {
                return;
            }

            ReferenceHub h = ev.Player.ReferenceHub;
            if (h == null || h.authManager?.DoNotTrack == true)
            {
                return;
            }

            Timeline.TrackInteract(h.PlayerId, ev.Door.Base.DoorId, 1, ev.CanOpen, ev.Door.Base.transform.position);
        }

        private void OnInteractedElevator(PlayerInteractedElevatorEventArgs ev)
        {
            if (!Timeline.IsRec || ev.Player == null || ev.Elevator == null)
            {
                return;
            }

            ReferenceHub h = ev.Player.ReferenceHub;
            if (h == null || h.authManager?.DoNotTrack == true)
            {
                return;
            }

            Timeline.TrackElevatorInteract(h.PlayerId, (byte)ev.Elevator.Group, ev.Elevator.NextDestinationLevel);
        }
    }
}
