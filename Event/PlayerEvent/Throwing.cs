using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent
{

    public sealed class Throwing
    {
        public void Enable()
        {
            PlayerEvents.ThrowingProjectile += OnThrowingProjectile;
            PlayerEvents.ThrewProjectile += OnThrewProjectile;
        }

        public void Disable()
        {
            PlayerEvents.ThrewProjectile -= OnThrewProjectile;
            PlayerEvents.ThrowingProjectile -= OnThrowingProjectile;
        }

        private void OnThrowingProjectile(PlayerThrowingProjectileEventArgs ev)
        {
            if (Timeline.IsPlay && ev.Player != null && ev.Player.IsDummy)
            {
                ev.IsAllowed = false;
            }
        }

        private void OnThrewProjectile(PlayerThrewProjectileEventArgs ev)
        {
            if (!Timeline.IsRec || ev.Player == null || ev.Projectile == null || ev.ThrowableItem == null)
            {
                return;
            }

            ReferenceHub h = ev.Player.ReferenceHub;
            if (h == null || h.authManager?.DoNotTrack == true)
            {
                return;
            }

            Timeline.TrackProjectile(ev.Projectile.Base, ev.ThrowableItem.Base.ItemTypeId, h);
        }
    }
}
