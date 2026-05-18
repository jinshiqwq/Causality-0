using Causality0.Core;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.ServerEvent
{

    public sealed class Pickups
    {
        public void Enable()
        {
            ServerEvents.PickupCreated += OnPickupCreated;
            ServerEvents.PickupDestroyed += OnPickupDestroyed;
        }

        public void Disable()
        {
            ServerEvents.PickupDestroyed -= OnPickupDestroyed;
            ServerEvents.PickupCreated -= OnPickupCreated;
        }

        private void OnPickupCreated(PickupCreatedEventArgs ev)
        {
            if (ev.Pickup == null)
            {
                return;
            }

            Timeline.TrackPickupCreate(ev.Pickup);
        }

        private void OnPickupDestroyed(PickupDestroyedEventArgs ev)
        {
            if (ev.Pickup == null)
            {
                return;
            }

            Timeline.TrackPickupDestroy(ev.Pickup);
        }
    }
}
