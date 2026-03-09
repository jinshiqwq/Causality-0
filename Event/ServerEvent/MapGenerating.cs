using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using MapGeneration;

namespace Causality0.Event.ServerEvent;

public sealed class MapGenerating
{
    public int Seed { get; set; }

    public bool UseSeed { get; set; }

    public void Enable()
    {
        ServerEvents.MapGenerating += OnMapGenerating;
    }

    public void Disable()
    {
        ServerEvents.MapGenerating -= OnMapGenerating;
    }

    private void OnMapGenerating(MapGeneratingEventArgs ev)
    {
        if (!UseSeed || Seed <= 0)
        {
            return;
        }

        ev.Seed = Seed;
        UseSeed = false;
    }
}
