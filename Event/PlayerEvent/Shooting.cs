using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent
{

    public sealed class Shooting
    {
        public void Enable()
        {
            PlayerEvents.ShootingWeapon += OnShootingWeapon;
        }

        public void Disable()
        {
            PlayerEvents.ShootingWeapon -= OnShootingWeapon;
        }

        private void OnShootingWeapon(PlayerShootingWeaponEventArgs ev)
        {
            if (!Timeline.IsRec)
            {
                return;
            }

            if (ev.Player == null)
            {
                return;
            }

            ReferenceHub h = ev.Player.ReferenceHub;
            if (h == null || h.authManager?.DoNotTrack == true)
            {
                return;
            }

            Timeline.MarkInput(h.PlayerId, Timeline.InputShoot);
        }
    }
}
