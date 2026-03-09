using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Reloading
{
    public void Enable()
    {
        PlayerEvents.ReloadingWeapon += OnReloadingWeapon;
    }

    public void Disable()
    {
        PlayerEvents.ReloadingWeapon -= OnReloadingWeapon;
    }

    private void OnReloadingWeapon(PlayerReloadingWeaponEventArgs ev)
    {
        if (ev.Player == null)
        {
            return;
        }

        Timeline.MarkInput(ev.Player.ReferenceHub.PlayerId, Timeline.InputReload);
    }
}
