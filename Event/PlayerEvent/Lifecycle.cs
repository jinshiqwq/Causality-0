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
    }

    public void Disable()
    {
        PlayerEvents.Dying -= OnDying;
        PlayerEvents.ChangedRole -= OnChangedRole;
    }

    private void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null)
        {
            return;
        }

        Timeline.TrackLifecycleRole(ev.Player.ReferenceHub.PlayerId, ev.NewRole.RoleTypeId);
    }

    private void OnDying(PlayerDyingEventArgs ev)
    {
        if (!Timeline.IsRec || ev.Player == null || ev.DamageHandler == null)
        {
            return;
        }

        Timeline.TrackLifecycleDeath(ev.Player.ReferenceHub.PlayerId, ev.DamageHandler);
    }
}