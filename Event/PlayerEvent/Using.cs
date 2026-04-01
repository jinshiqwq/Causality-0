using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;

namespace Causality0.Event.PlayerEvent;

public sealed class Using
{
    public void Enable()
    {
        PlayerEvents.UsingItem += OnUsingItem;
        PlayerEvents.CancellingUsingItem += OnCancellingUsingItem;
    }

    public void Disable()
    {
        PlayerEvents.CancellingUsingItem -= OnCancellingUsingItem;
        PlayerEvents.UsingItem -= OnUsingItem;
    }

    private void OnUsingItem(PlayerUsingItemEventArgs ev)
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

        Timeline.MarkInput(h.PlayerId, Timeline.InputUse);
    }

    private void OnCancellingUsingItem(PlayerCancellingUsingItemEventArgs ev)
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

        Timeline.MarkInput(h.PlayerId, Timeline.InputUseCancel);
    }
}