using PlayerRoles;
using PlayerStatsSystem;

namespace Causality0.Core;

public enum EventType : byte
{
    RoleChanged,
    Died,
    Left
}

public struct LifecycleEvent
{
    public int FrameIndex;
    public EventType Type;
    public sbyte RoleId;
    public DamageData FatalDamage;

    public LifecycleEvent(int frameIndex, EventType type, sbyte roleId, DamageData fatalDamage)
    {
        FrameIndex = frameIndex;
        Type = type;
        RoleId = roleId;
        FatalDamage = fatalDamage;
    }

    public static LifecycleEvent NewRole(int f, RoleTypeId r)
    {
        return new LifecycleEvent(f, EventType.RoleChanged, (sbyte)r, default);
    }

    public static LifecycleEvent NewDeath(int f, DamageHandlerBase h)
    {
        return new LifecycleEvent(f, EventType.Died, 0, DamageData.FromHandler(h));
    }

    public static LifecycleEvent NewLeft(int f)
    {
        return new LifecycleEvent(f, EventType.Left, 0, default);
    }
}