using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

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
        if (ev.Player == null)
        {
            return;
        }

        Timeline.MarkInput(ev.Player.ReferenceHub.PlayerId, Timeline.InputShoot);
    }
}
