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

        ReferenceHub h = ev.Player.ReferenceHub;
        if (h == null || h.authManager?.DoNotTrack == true)
        {
            return;
        }

        Timeline.MarkInput(h.PlayerId, Timeline.InputReload);
    }
}
