using System.Collections.Generic;
using Footprinting;
using InventorySystem.Items.ThrowableProjectiles;
using UnityEngine;

namespace Causality0.Core;

public struct ProjectileFrame
{
    public Vector3 Pos;
    public Quaternion Rot;

    public ProjectileFrame(Vector3 pos, Quaternion rot)
    {
        Pos = pos;
        Rot = rot;
    }
}

public sealed class ProjectileTrack
{
    public ItemType ProjectileType { get; set; }
    public List<ProjectileFrame> Frames { get; } = new List<ProjectileFrame>();
    public bool HasDetonated { get; set; }
    public int StartFrame { get; set; }
    public int OwnerId { get; set; }
    public Footprint Owner { get; set; }
    public ThrownProjectile Live { get; set; }
    public ThrownProjectile Puppet { get; set; }
}
