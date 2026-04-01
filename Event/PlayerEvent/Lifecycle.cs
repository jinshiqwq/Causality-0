using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Lifecycle
{
    public void Enable()
    {
        PlayerEvents.ChangedRole += OnChangedRole;
        PlayerEvents.Dying += OnDying;
        PlayerEvents.Left += OnLeft;
    }

    public void Disable()
    {
        PlayerEvents.Left -= OnLeft;
        PlayerEvents.Dying -= OnDying;
        PlayerEvents.ChangedRole -= OnChangedRole;
    }

    private void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null)
        {
            return;
        }

        ReferenceHub h = ev.Player.ReferenceHub;
        if (h == null || h.authManager?.DoNotTrack == true)
        {
            return;
        }

        Timeline.TrackLifecycleRole(h.PlayerId, ev.NewRole.RoleTypeId);
    }

    private void OnDying(PlayerDyingEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null || ev.DamageHandler == null)
        {
            return;
        }

        ReferenceHub h = ev.Player.ReferenceHub;
        if (h == null || h.authManager?.DoNotTrack == true)
        {
            return;
        }

        Timeline.TrackLifecycleDeath(h.PlayerId, ev.DamageHandler);
    }

    private void OnLeft(PlayerLeftEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null || ev.Player.IsDummy)
        {
            return;
        }

        ReferenceHub h = ev.Player.ReferenceHub;
        if (h == null || h.authManager?.DoNotTrack == true)
        {
            return;
        }

        Timeline.TrackLifecycleLeft(h.PlayerId);
    }
}