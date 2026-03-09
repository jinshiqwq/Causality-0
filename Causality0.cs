using System;
using Causality0.Event.PlayerEvent;
using Causality0.Event.ServerEvent;
using LabApi.Features;
using LabApi.Loader.Features.Plugins;

namespace Causality0;

public sealed class Causality0 : Plugin
{
    public static Causality0 Instance { get; private set; }

    public MapGenerating ServerEvent { get; } = new();

    public Verified VerifiedEvent { get; } = new();

    public Shooting ShootingEvent { get; } = new();

    public Reloading ReloadingEvent { get; } = new();

    public Using UsingEvent { get; } = new();

    public global::Causality0.Event.PlayerEvent.VoiceChat VoiceChatEvent { get; } = new();

    public Throwing ThrowingEvent { get; } = new();

    public Interacting InteractingEvent { get; } = new();

    public Lifecycle LifecycleEvent { get; } = new();

    public override string Name { get; } = "Causality-0";

    public override string Description { get; } = "Seed override and dummy injection core.";

    public override string Author { get; } = "MiaoMiao";

    public override Version Version { get; } = new(1, 0, 0);

    public override Version RequiredApiVersion { get; } = new(LabApiProperties.CompiledVersion);

    public override void Enable()
    {
        Instance = this;
        ServerEvent.Enable();
        VerifiedEvent.Enable();
        ShootingEvent.Enable();
        ReloadingEvent.Enable();
        UsingEvent.Enable();
        VoiceChatEvent.Enable();
        ThrowingEvent.Enable();
        InteractingEvent.Enable();
        LifecycleEvent.Enable();
    }

    public override void Disable()
    {
        LifecycleEvent.Disable();
        InteractingEvent.Disable();
        ThrowingEvent.Disable();
        VoiceChatEvent.Disable();
        UsingEvent.Disable();
        ReloadingEvent.Disable();
        ShootingEvent.Disable();
        VerifiedEvent.Disable();
        ServerEvent.Disable();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
