using System.Collections.Generic;
using MEC;
using Footprinting;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items;
using InventorySystem.Items.Autosync;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using InventorySystem.Items.Usables;
using InventorySystem;
using LabApi.Features.Wrappers;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.HumeShield;
using PlayerStatsSystem;
using UnityEngine;
using Utils;
using VoiceChat;
using VoiceChat.Networking;

namespace Causality0.Core;

public static class Timeline
{
    public static Dictionary<int, ActorTrack> Tracks { get; } = new Dictionary<int, ActorTrack>();

    public static List<ProjectileTrack> ProjTracks { get; } = new List<ProjectileTrack>();

    public static List<InteractFrame> Interacts { get; } = new List<InteractFrame>();

    public static List<PickupData> WorldPickups { get; } = new List<PickupData>();

    public static List<PickupOp> PickupOps { get; } = new List<PickupOp>();

    public static List<LockerData> LockerStates { get; } = new List<LockerData>();

    public static List<LockerOp> LockerOps { get; } = new List<LockerOp>();

    public static int MapSeed { get; set; }

    public static int CurrentFps { get; set; } = 60;

    public static float Step => 1f / Mathf.Max(1, CurrentFps);

    public static float RecordStartTime;

    public static int RecFrame;

    public static bool HasWorldState { get; set; }

    public const byte InputShoot = 1;

    public const byte InputReload = 2;

    public const byte InputUse = 4;

    public const byte InputUseCancel = 8;

    private static Dictionary<int, byte> _im = new Dictionary<int, byte>();

    private static Dictionary<int, ProjectileTrack> _pm = new Dictionary<int, ProjectileTrack>();

    private static Dictionary<byte, DoorVariant> _dm = new Dictionary<byte, DoorVariant>();

    private static Dictionary<ItemPickupBase, int> _pim = new Dictionary<ItemPickupBase, int>();

    private static Dictionary<int, Pickup> _ppm = new Dictionary<int, Pickup>();

    private static Dictionary<int, PickupData> _psm = new Dictionary<int, PickupData>();

    private static int _nextPickupId = 1;

    private static bool _wa;

    public static bool IsRec => _rh.IsRunning;

    public static bool IsPlay => _ph.IsRunning;

    private static CoroutineHandle _rh;

    private static CoroutineHandle _ph;

    public static void Clear()
    {
        Tracks.Clear();
        ProjTracks.Clear();
        Interacts.Clear();
        WorldPickups.Clear();
        PickupOps.Clear();
        LockerStates.Clear();
        LockerOps.Clear();
        MapSeed = 0;
        RecordStartTime = 0f;
        RecFrame = 0;
        HasWorldState = false;
        _im.Clear();
        _pm.Clear();
        _dm.Clear();
        _pim.Clear();
        _ppm.Clear();
        _psm.Clear();
        _nextPickupId = 1;
        _wa = false;
    }

    public static int Count
    {
        get
        {
            int n = 0;
            foreach (ActorTrack t in Tracks.Values)
            {
                n += t.Frames.Count;
            }

            return n;
        }
    }

    public static void TrackActor(ReferenceHub h)
    {
        if (!IsRec || h == null || h.isLocalPlayer || h.PlayerId == 0 || Tracks.ContainsKey(h.PlayerId))
        {
            return;
        }

        ActorTrack t = new ActorTrack
        {
            PlayerId = h.PlayerId,
            ActorName = h.nicknameSync?.MyNick ?? string.Empty,
            Role = (sbyte)h.GetRoleId(),
            StartFrame = -1
        };
        t.LifeEvents.Add(LifecycleEvent.NewRole(RecFrame, h.GetRoleId()));
        Tracks[h.PlayerId] = t;
    }

    public static void StartRecord(ReferenceHub h)
    {
        StopRecord();
        MapSeed = MapGeneration.SeedSynchronizer.Seed;
        RecordStartTime = Time.time;
        RecFrame = 0;
        Tracks.Clear();
        ProjTracks.Clear();
        Interacts.Clear();
        _im.Clear();
        _pm.Clear();
        _dm.Clear();
        foreach (ReferenceHub hub in ReferenceHub.AllHubs)
        {
            if (hub == null || hub.isLocalPlayer || hub.PlayerId == 0)
            {
                continue;
            }

            ActorTrack t = new ActorTrack
            {
                PlayerId = hub.PlayerId,
                ActorName = hub.nicknameSync.MyNick,
                Role = (sbyte)hub.GetRoleId(),
                StartFrame = 0
            };
            t.LifeEvents.Add(LifecycleEvent.NewRole(0, hub.GetRoleId()));
            Tracks[hub.PlayerId] = t;
        }

        CaptureWorldState();
        _rh = Timing.RunCoroutine(RunRecord());
    }

    public static int StopRecord()
    {
        if (_rh.IsRunning)
        {
            Timing.KillCoroutines(_rh);
        }

        return Count;
    }

    public static bool StartPlay()
    {
        bool ok = false;
        foreach (ActorTrack t in Tracks.Values)
        {
            if (t.Frames.Count > 0)
            {
                ok = true;
                break;
            }
        }

        if (!ok)
        {
            return false;
        }

        StopPlay();
        RebuildDoors();
        Interacts.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        PickupOps.Sort((a, b) => a.Ts.CompareTo(b.Ts));
        LockerOps.Sort((a, b) => a.Ts.CompareTo(b.Ts));
        ProjTracks.Sort((a, b) => a.StartFrame.CompareTo(b.StartFrame));
        _ph = Timing.RunCoroutine(RunPlay());
        return true;
    }

    public static void StopPlay()
    {
        if (_ph.IsRunning)
        {
            Timing.KillCoroutines(_ph);
        }
    }

    public static int MaxFrames()
    {
        int n = 0;
        foreach (ActorTrack t in Tracks.Values)
        {
            if (t.Frames.Count > n)
            {
                n = t.Frames.Count;
            }
        }

        return n;
    }

    public static void MarkInput(int id, byte mask)
    {
        if (!IsRec || mask == 0 || !Tracks.ContainsKey(id))
        {
            return;
        }

        if (_im.TryGetValue(id, out byte v))
        {
            _im[id] = (byte)(v | mask);
        }
        else
        {
            _im[id] = mask;
        }
    }

    private static byte PullInput(int id)
    {
        if (!_im.TryGetValue(id, out byte v))
        {
            return 0;
        }

        _im.Remove(id);
        return v;
    }

    private static int NextPickupId()
    {
        int id = _nextPickupId;
        _nextPickupId++;
        return id;
    }

    private static void RegisterPickup(Pickup p, int id)
    {
        if (p?.Base == null)
        {
            return;
        }

        _pim[p.Base] = id;
        _ppm[id] = p;
    }

    private static bool IsWorldPickup(Pickup p)
    {
        return p != null && !p.IsDestroyed && p.IsSpawned && p.Base != null && p.Base is not ThrownProjectile;
    }

    private static bool TryBuildPickupData(Pickup p, int id, out PickupData x)
    {
        x = default;
        if (p == null || p.IsDestroyed || p.Base == null || p.Base is ThrownProjectile)
        {
            return false;
        }

        uint at = 0;
        ushort am = 0;
        if (p is LabApi.Features.Wrappers.FirearmPickup fp)
        {
            at = fp.AttachmentCode;
        }
        else if (p is LabApi.Features.Wrappers.AmmoPickup ap)
        {
            am = ap.Ammo;
        }

        x = new PickupData(id, p.Type, p.Position, p.Rotation, at, am, p.IsLocked);
        return true;
    }

    private static void ApplyPickupData(Pickup p, PickupData x)
    {
        if (p == null || p.IsDestroyed)
        {
            return;
        }

        p.Position = x.Pos;
        p.Rotation = x.Rot;
        p.IsLocked = x.Locked;
        if (p is LabApi.Features.Wrappers.FirearmPickup fp)
        {
            fp.AttachmentCode = x.At;
        }
        else if (p is LabApi.Features.Wrappers.AmmoPickup ap)
        {
            ap.Ammo = x.Am;
        }
    }

    private static void CaptureWorldState()
    {
        WorldPickups.Clear();
        PickupOps.Clear();
        LockerStates.Clear();
        LockerOps.Clear();
        HasWorldState = true;
        _pim.Clear();
        _ppm.Clear();
        _nextPickupId = 1;
        foreach (Locker lk in Locker.List)
        {
            if (lk == null)
            {
                continue;
            }

            for (int i = 0; i < lk.Chambers.Count; i++)
            {
                LockerChamber c = lk.Chambers[i];
                if (c == null)
                {
                    continue;
                }

                LockerData s = new LockerData
                {
                    Pos = lk.Position,
                    Id = c.Id,
                    Open = c.IsOpen,
                    WasOpen = c.Base.WasEverOpened
                };
                for (int j = 0; j < c.Base.Content.Count; j++)
                {
                    ItemPickupBase b = c.Base.Content[j];
                    if (b == null)
                    {
                        continue;
                    }

                    Pickup p = Pickup.Get(b);
                    if (!TryBuildPickupData(p, NextPickupId(), out PickupData x))
                    {
                        continue;
                    }

                    s.Items.Add(x);
                    RegisterPickup(p, x.Id);
                    _psm[x.Id] = x;
                }

                LockerStates.Add(s);
            }
        }

        foreach (Pickup p in Map.Pickups)
        {
            if (!IsWorldPickup(p) || _pim.ContainsKey(p.Base))
            {
                continue;
            }

            if (!TryBuildPickupData(p, NextPickupId(), out PickupData x))
            {
                continue;
            }

            WorldPickups.Add(x);
            RegisterPickup(p, x.Id);
            _psm[x.Id] = x;
        }
    }

    public static void TrackPickupCreate(Pickup p)
    {
        if (!IsRec || _wa || !IsWorldPickup(p) || _pim.ContainsKey(p.Base))
        {
            return;
        }

        if (!TryBuildPickupData(p, NextPickupId(), out PickupData x))
        {
            return;
        }

        RegisterPickup(p, x.Id);
        _psm[x.Id] = x;
        PickupOps.Add(PickupOp.NewAdd(RecFrame * Step, x));
    }

    public static void TrackPickupDestroy(Pickup p)
    {
        if (!IsRec || _wa || p?.Base == null)
        {
            return;
        }

        if (!_pim.TryGetValue(p.Base, out int id))
        {
            return;
        }

        _pim.Remove(p.Base);
        _ppm.Remove(id);
        _psm.Remove(id);
        PickupOps.Add(PickupOp.NewRemove(RecFrame * Step, id));
    }

    public static void TrackLocker(LockerChamber c, bool canOpen)
    {
        if (!IsRec || _wa || c == null || c.Locker == null)
        {
            return;
        }

        LockerOps.Add(new LockerOp(RecFrame * Step, c.Locker.Position, c.Id, c.IsOpen, canOpen));
    }

    private static bool PickupChanged(PickupData a, PickupData b)
    {
        if (a.T != b.T || a.At != b.At || a.Am != b.Am || a.Locked != b.Locked)
        {
            return true;
        }

        if ((a.Pos - b.Pos).sqrMagnitude > 0.0001f)
        {
            return true;
        }

        return Quaternion.Angle(a.Rot, b.Rot) > 0.25f;
    }

    private static void ScanPickupStates()
    {
        foreach (KeyValuePair<int, Pickup> kv in _ppm)
        {
            Pickup p = kv.Value;
            if (p == null || p.IsDestroyed || !p.IsSpawned)
            {
                continue;
            }

            if (!TryBuildPickupData(p, kv.Key, out PickupData x))
            {
                continue;
            }

            if (!_psm.TryGetValue(kv.Key, out PickupData prev))
            {
                _psm[kv.Key] = x;
                continue;
            }

            if (!PickupChanged(prev, x))
            {
                continue;
            }

            _psm[kv.Key] = x;
            PickupOps.Add(PickupOp.NewMove(RecFrame * Step, x));
        }
    }

    private static string LockerKey(Vector3 p, byte id)
    {
        int x = Mathf.RoundToInt(p.x * 100f);
        int y = Mathf.RoundToInt(p.y * 100f);
        int z = Mathf.RoundToInt(p.z * 100f);
        return x.ToString() + "|" + y.ToString() + "|" + z.ToString() + "|" + id.ToString();
    }

    private static bool TryGetLockerChamber(Vector3 p, byte id, out LockerChamber c)
    {
        string k = LockerKey(p, id);
        foreach (Locker lk in Locker.List)
        {
            if (lk == null)
            {
                continue;
            }

            for (int i = 0; i < lk.Chambers.Count; i++)
            {
                LockerChamber x = lk.Chambers[i];
                if (x != null && LockerKey(lk.Position, x.Id) == k)
                {
                    c = x;
                    return true;
                }
            }
        }

        c = null;
        return false;
    }

    private static Pickup SpawnWorldPickup(PickupData x)
    {
        Pickup p = Pickup.Create(x.ItemType, x.Pos, x.Rot);
        if (p == null)
        {
            return null;
        }

        p.Spawn();
        ApplyPickupData(p, x);
        return p;
    }

    private static Pickup SpawnLockerPickup(LockerChamber c, PickupData x, int idx, bool v)
    {
        c.Base.GetSpawnpoint(x.ItemType, idx, out Vector3 p0, out Quaternion r0, out Transform t0);
        Pickup p = Pickup.Create(x.ItemType, p0, r0);
        if (p == null)
        {
            return null;
        }

        p.Transform.SetParent(t0);
        p.Position = x.Pos;
        p.Rotation = x.Rot;
        c.Base.Content.Add(p.Base);
        if (p.Base is InventorySystem.Items.Pickups.IPickupDistributorTrigger trg)
        {
            trg.OnDistributed();
        }

        if (c.Base.SpawnOnFirstChamberOpening && !v)
        {
            c.Base.ToBeSpawned.Add(p.Base);
        }
        else
        {
            p.Spawn();
        }

        ApplyPickupData(p, x);
        if (!v)
        {
            p.IsLocked = true;
        }

        return p;
    }

    private static void ClearWorldState()
    {
        HashSet<ItemPickupBase> hs = new HashSet<ItemPickupBase>();
        foreach (Locker lk in Locker.List)
        {
            if (lk == null)
            {
                continue;
            }

            for (int i = 0; i < lk.Chambers.Count; i++)
            {
                LockerChamber c = lk.Chambers[i];
                if (c == null)
                {
                    continue;
                }

                for (int j = 0; j < c.Base.Content.Count; j++)
                {
                    ItemPickupBase b = c.Base.Content[j];
                    if (b != null)
                    {
                        hs.Add(b);
                    }
                }
            }
        }

        foreach (Locker lk in Locker.List)
        {
            if (lk == null)
            {
                continue;
            }

            lk.ClearAllChambers();
            for (int i = 0; i < lk.Chambers.Count; i++)
            {
                LockerChamber c = lk.Chambers[i];
                if (c == null)
                {
                    continue;
                }

                c.Base.ToBeSpawned.Clear();
                c.Base.WasEverOpened = false;
                c.IsOpen = false;
            }
        }

        List<Pickup> ls = new List<Pickup>();
        foreach (Pickup p in Map.Pickups)
        {
            if (p?.Base == null || hs.Contains(p.Base))
            {
                continue;
            }

            ls.Add(p);
        }

        for (int i = 0; i < ls.Count; i++)
        {
            ls[i].Destroy();
        }

        _pim.Clear();
        _ppm.Clear();
        _psm.Clear();
    }

    public static void ApplyWorldState()
    {
        if (!HasWorldState)
        {
            return;
        }

        _wa = true;
        try
        {
            ClearWorldState();
            for (int i = 0; i < LockerStates.Count; i++)
            {
                LockerData x = LockerStates[i];
                if (!TryGetLockerChamber(x.Pos, x.Id, out LockerChamber c) || c == null)
                {
                    continue;
                }

                bool v = x.WasOpen || x.Open;
                c.IsOpen = v;
                c.Base.WasEverOpened = v;
                for (int j = 0; j < x.Items.Count; j++)
                {
                    Pickup p = SpawnLockerPickup(c, x.Items[j], j, v);
                    if (p != null)
                    {
                        RegisterPickup(p, x.Items[j].Id);
                    }
                }

                c.Base.WasEverOpened = x.WasOpen;
                c.IsOpen = x.Open;
            }

            for (int i = 0; i < WorldPickups.Count; i++)
            {
                Pickup p = SpawnWorldPickup(WorldPickups[i]);
                if (p != null)
                {
                    RegisterPickup(p, WorldPickups[i].Id);
                }
            }
        }
        finally
        {
            _wa = false;
        }
    }

    private static void ApplyPickupOp(PickupOp x)
    {
        if (x.Act == PickupAct.Remove)
        {
            if (_ppm.TryGetValue(x.Id, out Pickup p0) && p0 != null)
            {
                if (p0.Base != null)
                {
                    _pim.Remove(p0.Base);
                }

                _ppm.Remove(x.Id);
                _psm.Remove(x.Id);
                p0.Destroy();
            }

            return;
        }

        if (_ppm.TryGetValue(x.Id, out Pickup p1) && p1 != null)
        {
            ApplyPickupData(p1, x.Data);
            _psm[x.Id] = x.Data;
            return;
        }

        Pickup p = SpawnWorldPickup(x.Data);
        if (p != null)
        {
            RegisterPickup(p, x.Id);
            _psm[x.Id] = x.Data;
        }
    }

    private static void ApplyLockerOp(LockerOp x)
    {
        if (TryGetLockerChamber(x.Pos, x.Id, out LockerChamber c) && c != null)
        {
            if (!x.CanOpen)
            {
                c.PlayDeniedSound(DoorPermissionFlags.None);
                return;
            }

            if (x.Open)
            {
                c.Base.WasEverOpened = true;
            }

            c.IsOpen = x.Open;
        }
    }

    public static void TrackInteract(int id, byte doorId, byte act, bool canOpen)
    {
        if (!IsRec || !Tracks.ContainsKey(id))
        {
            return;
        }

        Interacts.Add(new InteractFrame(RecFrame * Step, id, doorId, act, canOpen));
    }

    public static void TrackLifecycleRole(int id, RoleTypeId r)
    {
        if (!IsRec || !Tracks.TryGetValue(id, out var t))
        {
            return;
        }

        if (t.LifeEvents.Count > 0)
        {
            LifecycleEvent prev = t.LifeEvents[t.LifeEvents.Count - 1];
            if (prev.FrameIndex == RecFrame && prev.Type == EventType.RoleChanged && prev.RoleId == (sbyte)r)
            {
                return;
            }
        }

        t.LifeEvents.Add(LifecycleEvent.NewRole(RecFrame, r));
    }

    public static void TrackLifecycleDeath(int id, DamageHandlerBase h)
    {
        if (!IsRec || !Tracks.TryGetValue(id, out var t))
        {
            return;
        }

        t.LifeEvents.Add(LifecycleEvent.NewDeath(RecFrame, h));
    }

    public static void TrackLifecycleLeft(int id)
    {
        if (!IsRec || !Tracks.TryGetValue(id, out var t))
        {
            return;
        }

        if (t.LifeEvents.Count > 0)
        {
            LifecycleEvent prev = t.LifeEvents[t.LifeEvents.Count - 1];
            if (prev.FrameIndex == RecFrame && prev.Type == EventType.Left)
            {
                return;
            }
        }

        _im.Remove(id);
        t.LifeEvents.Add(LifecycleEvent.NewLeft(RecFrame));
    }

    private static RoleTypeId ResolveRole(ActorTrack t, int f)
    {
        if (t == null)
        {
            return default;
        }

        RoleTypeId r = (RoleTypeId)t.Role;

        for (int i = 0; i < t.LifeEvents.Count; i++)
        {
            LifecycleEvent x = t.LifeEvents[i];
            if (x.Type != EventType.RoleChanged)
            {
                continue;
            }

            if (f >= 0 && x.FrameIndex > f)
            {
                break;
            }

            r = (RoleTypeId)x.RoleId;
        }

        return r;
    }

    private static int ResolveEndFrame(ActorTrack t)
    {
        if (t == null)
        {
            return -1;
        }

        for (int i = 0; i < t.LifeEvents.Count; i++)
        {
            if (t.LifeEvents[i].Type == EventType.Left)
            {
                return t.LifeEvents[i].FrameIndex;
            }
        }

        return -1;
    }

    private static Footprint ResolveProjectileOwner(ProjectileTrack tr)
    {
        if (tr == null)
        {
            return default;
        }

        if (Tracks.TryGetValue(tr.OwnerId, out ActorTrack t) && t?.Dummy != null)
        {
            Footprint o = new Footprint(t.Dummy);
            tr.Owner = o;
            return o;
        }

        foreach (ReferenceHub h in ReferenceHub.AllHubs)
        {
            if (h != null && h.PlayerId == tr.OwnerId)
            {
                Footprint o = new Footprint(h);
                tr.Owner = o;
                return o;
            }
        }

        return tr.Owner;
    }

    public static bool TrySpawnActor(ActorTrack t)
    {
        if (t == null || t.Dummy != null || t.Frames.Count <= 0)
        {
            return false;
        }

        string n = string.IsNullOrWhiteSpace(t.ActorName) ? $"Actor-{t.PlayerId}" : t.ActorName;
        ReferenceHub h = DummyUtils.SpawnDummy(n);
        if (h == null)
        {
            return false;
        }

        FrameData f = t.Frames[0];
        RoleTypeId r = ResolveRole(t, t.StartFrame);
        h.roleManager.ServerSetRole(r, RoleChangeReason.RemoteAdmin);
        t.Dummy = h;
        h.TryOverridePosition(f.Pos);
        h.TryOverrideRotation(f.Rot);
        Timing.CallDelayed(0.1f, () =>
        {
            if (t.Dummy != h)
            {
                return;
            }

            h.TryOverridePosition(f.Pos);
            h.TryOverrideRotation(f.Rot);
        });
        return true;
    }

    private static void DespawnActor(ActorTrack t)
    {
        if (t?.Dummy == null)
        {
            return;
        }

        ReferenceHub h = t.Dummy;
        t.Dummy = null;
        if (h != null && h.gameObject.TryGetComponent<DummyInputWrapper>(out var w))
        {
            w.Remove(ActionName.Shoot);
            w.Remove(ActionName.Reload);
        }

        if (h == null || h.gameObject == null)
        {
            return;
        }

        if (NetworkServer.active)
        {
            NetworkServer.Destroy(h.gameObject);
        }
        else
        {
            Object.Destroy(h.gameObject);
        }
    }

    private static void RebuildDoors()
    {
        _dm.Clear();
        foreach (DoorVariant d in DoorVariant.AllDoors)
        {
            if (d != null)
            {
                _dm[d.DoorId] = d;
            }
        }
    }

    private static void SyncMotor(ReferenceHub h, Vector3 v, bool g, PlayerMovementState s)
    {
        if (h == null)
        {
            return;
        }

        if (!h.gameObject.TryGetComponent<DummyMotorWrapper>(out var w))
        {
            w = h.gameObject.AddComponent<DummyMotorWrapper>();
        }

        w.Bind(h);
        w.SetFakeVelocity(v, g, s);
    }

    private static void SyncStats(ReferenceHub h, FrameData f)
    {
        if (h == null || h.playerStats == null || (f.Hp < 0f && f.Ahp < 0f))
        {
            return;
        }

        if (f.Hp >= 0f && h.playerStats.TryGetModule<HealthStat>(out var hp))
        {
            hp.MaxValue = Mathf.Max(hp.MaxValue, f.Hp);
            hp.CurValue = f.Hp;
        }

        if (h.roleManager.CurrentRole is IHumeShieldedRole)
        {
            if (f.Ahp >= 0f && h.playerStats.TryGetModule<HumeShieldStat>(out var hs))
            {
                hs.MaxValue = Mathf.Max(hs.MaxValue, f.Ahp);
                hs.CurValue = f.Ahp;
            }

            if (h.playerStats.TryGetModule<AhpStat>(out var ah0))
            {
                ah0.CurValue = 0f;
            }
        }
        else
        {
            if (f.Ahp >= 0f && h.playerStats.TryGetModule<AhpStat>(out var ah))
            {
                ah.MaxValue = Mathf.Max(ah.MaxValue, f.Ahp);
                ah.CurValue = f.Ahp;
            }

            if (h.playerStats.TryGetModule<HumeShieldStat>(out var hs0))
            {
                hs0.CurValue = 0f;
            }
        }
    }

    private static void ReplayUse(ReferenceHub h, byte m)
    {
        if (h?.inventory?.CurInstance is not InventorySystem.Items.Usables.UsableItem u)
        {
            return;
        }

        if ((m & InputUse) != 0)
        {
            UsableItemsController.ServerEmulateMessage(u.ItemSerial, StatusMessage.StatusType.Start);
        }

        if ((m & InputUseCancel) != 0)
        {
            UsableItemsController.ServerEmulateMessage(u.ItemSerial, StatusMessage.StatusType.Cancel);
        }
    }

    private static void ReplayInteract(InteractFrame x)
    {
        if (!_dm.TryGetValue(x.DoorId, out var d) || d == null)
        {
            RebuildDoors();
            if (!_dm.TryGetValue(x.DoorId, out d) || d == null)
            {
                return;
            }
        }

        if (!Tracks.TryGetValue(x.PlayerId, out var t) || t.Dummy == null)
        {
            return;
        }

        d.ServerInteract(t.Dummy, 0);
    }

    public static void TrackProjectile(ThrownProjectile p, ItemType t, ReferenceHub h)
    {
        if (!IsRec || p == null || h == null)
        {
            return;
        }

        int id = p.GetInstanceID();
        if (_pm.ContainsKey(id))
        {
            return;
        }

        ProjectileTrack tr = new ProjectileTrack
        {
            ProjectileType = t,
            StartFrame = RecFrame,
            OwnerId = h.PlayerId,
            Owner = new Footprint(h),
            Live = p
        };
        ProjTracks.Add(tr);
        _pm[id] = tr;
    }

    private static T SpawnProjectileEntity<T>(ItemType t, Vector3 p, Quaternion r, Footprint o) where T : ThrownProjectile
    {
        if (!InventoryItemLoader.TryGetItem<InventorySystem.Items.ThrowableProjectiles.ThrowableItem>(t, out var it) || it.Projectile is not T pr)
        {
            return null;
        }

        T g = Object.Instantiate(pr, p, r);
        g.NetworkInfo = new PickupSyncInfo(t, it.Weight, locked: true);
        g.PreviousOwner = o;
        NetworkServer.Spawn(g.gameObject);
        return g;
    }

    private static void SpawnProjectilePuppet(ProjectileTrack tr)
    {
        if (tr == null || tr.Puppet != null || tr.Frames.Count == 0)
        {
            return;
        }

        ProjectileFrame f = tr.Frames[0];
        Footprint o = ResolveProjectileOwner(tr);
        ThrownProjectile p = SpawnProjectileEntity<ThrownProjectile>(tr.ProjectileType, f.Pos, f.Rot, o);
        if (p == null)
        {
            return;
        }

        p.ServerOnThrown(Vector3.zero, Vector3.zero);
        if (p.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.position = f.Pos;
            rb.rotation = f.Rot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Collider[] cs = p.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cs.Length; i++)
        {
            cs[i].enabled = false;
        }

        tr.Puppet = p;
    }

    private static void MoveProjectilePuppet(ProjectileTrack tr, int pi)
    {
        if (tr == null || tr.Puppet == null)
        {
            return;
        }

        ProjectileFrame f = tr.Frames[pi];
        ProjectileFrame n = pi + 1 < tr.Frames.Count ? tr.Frames[pi + 1] : f;
        if (tr.Puppet.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 v = (n.Pos - rb.position) / Step;
            rb.linearVelocity = v;
            rb.rotation = f.Rot;
        }
        else
        {
            tr.Puppet.transform.SetPositionAndRotation(f.Pos, f.Rot);
        }
    }

    private static void DetonateProjectileTrack(ProjectileTrack tr)
    {
        if (tr == null || tr.HasDetonated || tr.Frames.Count == 0)
        {
            return;
        }

        ProjectileFrame f = tr.Frames[tr.Frames.Count - 1];
        if (tr.Puppet != null)
        {
            tr.Puppet.transform.SetPositionAndRotation(f.Pos, f.Rot);
            if (tr.Puppet.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.position = f.Pos;
                rb.rotation = f.Rot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        Footprint o = ResolveProjectileOwner(tr);
        switch (tr.ProjectileType)
        {
            case ItemType.GrenadeHE:
                ExplosionGrenade g1 = tr.Puppet as ExplosionGrenade ?? SpawnProjectileEntity<ExplosionGrenade>(ItemType.GrenadeHE, f.Pos, f.Rot, o);
                if (g1 != null)
                {
                    g1.ServerFuseEnd();
                }
                else
                {
                    ExplosionUtils.ServerExplode(f.Pos, o, ExplosionType.Grenade);
                }
                break;
            case ItemType.SCP018:
                InventorySystem.Items.ThrowableProjectiles.Scp018Projectile g2 = tr.Puppet as InventorySystem.Items.ThrowableProjectiles.Scp018Projectile;
                if (g2 != null)
                {
                    g2.ServerFuseEnd();
                }
                else
                {
                    ExplosionUtils.ServerExplode(f.Pos, o, ExplosionType.SCP018);
                }
                break;
            case ItemType.GrenadeFlash:
                FlashbangGrenade g3 = tr.Puppet as FlashbangGrenade ?? SpawnProjectileEntity<FlashbangGrenade>(ItemType.GrenadeFlash, f.Pos, f.Rot, o);
                if (g3 != null)
                {
                    g3.ServerFuseEnd();
                }
                break;
            case ItemType.SCP2176:
                InventorySystem.Items.ThrowableProjectiles.Scp2176Projectile g4 = tr.Puppet as InventorySystem.Items.ThrowableProjectiles.Scp2176Projectile ?? SpawnProjectileEntity<InventorySystem.Items.ThrowableProjectiles.Scp2176Projectile>(ItemType.SCP2176, f.Pos, f.Rot, o);
                if (g4 != null)
                {
                    g4.ServerImmediatelyShatter();
                }
                break;
        }

        tr.Puppet = null;
        tr.HasDetonated = true;
    }

    private static IEnumerator<float> RunRecord()
    {
        while (Tracks.Count > 0)
        {
            foreach (ActorTrack t in Tracks.Values)
            {
                ReferenceHub h = null;
                foreach (ReferenceHub hub in ReferenceHub.AllHubs)
                {
                    if (hub.PlayerId == t.PlayerId)
                    {
                        h = hub;
                        break;
                    }
                }

                if (h == null || h.roleManager.CurrentRole is not IFpcRole r || r.FpcModule == null)
                {
                    continue;
                }

                ItemType curType = h.inventory.CurItem.TypeId;
                ushort cur = unchecked((ushort)curType);
                bool act = curType != ItemType.None;
                byte im = PullInput(t.PlayerId);
                uint at = 0;
                float hp = -1f;
                float ah = -1f;
                if (h.inventory.CurInstance is Firearm fa)
                {
                    at = fa.GetCurrentAttachmentsCode();
                    if (fa.TryGetModule<ITriggerControllerModule>(out var trg) && trg.TriggerHeld)
                    {
                        im |= InputShoot;
                    }
                }

                if (h.playerStats.TryGetModule<HealthStat>(out var hs))
                {
                    hp = hs.CurValue;
                }

                if (h.roleManager.CurrentRole is IHumeShieldedRole)
                {
                    if (h.playerStats.TryGetModule<HumeShieldStat>(out var hm))
                    {
                        ah = hm.CurValue;
                    }
                }
                else if (h.playerStats.TryGetModule<AhpStat>(out var am))
                {
                    ah = am.CurValue;
                }

                t.Role = (sbyte)h.GetRoleId();
                t.ActorName = h.nicknameSync.MyNick;
                if (t.StartFrame < 0)
                {
                    t.StartFrame = RecFrame;
                }

                t.Frames.Add(new FrameData(r.FpcModule.Position, new Vector2(r.FpcModule.MouseLook.CurrentVertical, r.FpcModule.MouseLook.CurrentHorizontal), (byte)r.FpcModule.SyncMovementState, r.FpcModule.IsGrounded, cur, act, im, at, hp, ah));
            }

            List<int> rm = null;
            foreach (KeyValuePair<int, ProjectileTrack> kv in _pm)
            {
                ProjectileTrack tr = kv.Value;
                if (tr.Live == null)
                {
                    if (rm == null)
                    {
                        rm = new List<int>();
                    }

                    rm.Add(kv.Key);
                    continue;
                }

                tr.Frames.Add(new ProjectileFrame(tr.Live.transform.position, tr.Live.transform.rotation));
            }

            if (rm != null)
            {
                for (int i = 0; i < rm.Count; i++)
                {
                    _pm.Remove(rm[i]);
                }
            }

            ScanPickupStates();
            RecFrame++;
            yield return Timing.WaitForSeconds(Step);
        }
    }

    private static IEnumerator<float> RunPlay()
    {
        Dictionary<int, int> a = new Dictionary<int, int>();
        Dictionary<int, byte> p = new Dictionary<int, byte>();
        Dictionary<int, int> l = new Dictionary<int, int>();
        Dictionary<int, int> g = new Dictionary<int, int>();
        foreach (ActorTrack t in Tracks.Values)
        {
            a[t.PlayerId] = 0;
            p[t.PlayerId] = 0;
            l[t.PlayerId] = 0;
            t.LifeEvents.Sort((x, y) => x.FrameIndex != y.FrameIndex ? x.FrameIndex.CompareTo(y.FrameIndex) : ((byte)x.Type).CompareTo((byte)y.Type));
            g[t.PlayerId] = ResolveEndFrame(t);
        }

        int i = 0;
        int d = 0;
        int k = 0;
        int w = 0;
        while (true)
        {
            float e = i * Step;
            bool live = false;
            for (int j = 0; j < ProjTracks.Count; j++)
            {
                ProjectileTrack tr = ProjTracks[j];
                int pi = i - tr.StartFrame;
                if (pi < 0)
                {
                    live = true;
                    continue;
                }

                if (pi == 0 && tr.Puppet == null)
                {
                    SpawnProjectilePuppet(tr);
                }

                if (pi >= 0 && pi < tr.Frames.Count)
                {
                    MoveProjectilePuppet(tr, pi);
                    live = true;
                    continue;
                }

                if (!tr.HasDetonated)
                {
                    DetonateProjectileTrack(tr);
                    live = true;
                }
            }

            while (k < LockerOps.Count && LockerOps[k].Ts <= e)
            {
                ApplyLockerOp(LockerOps[k]);
                k++;
                live = true;
            }

            if (k < LockerOps.Count)
            {
                live = true;
            }

            while (w < PickupOps.Count && PickupOps[w].Ts <= e)
            {
                ApplyPickupOp(PickupOps[w]);
                w++;
                live = true;
            }

            if (w < PickupOps.Count)
            {
                live = true;
            }

            foreach (ActorTrack t in Tracks.Values)
            {
                int sf = t.StartFrame < 0 ? 0 : t.StartFrame;
                int ef = g.TryGetValue(t.PlayerId, out int gv) ? gv : -1;
                if (t.Dummy == null && t.Frames.Count > 0 && i >= sf && (ef < 0 || i < ef))
                {
                    TrySpawnActor(t);
                }

                if (t.Dummy == null)
                {
                    if (t.Frames.Count > 0 && (i < sf || ef < 0 || i < ef))
                    {
                        live = true;
                    }

                    continue;
                }

                int idx = a.TryGetValue(t.PlayerId, out int v) ? v : 0;
                while (idx < t.AudioFrames.Count && t.AudioFrames[idx].Timestamp <= e)
                {
                    AudioPacket ap = t.AudioFrames[idx];
                    if (ap.Data != null && ap.DataLength > 0)
                    {
                        int len = ap.DataLength;
                        if (len > ap.Data.Length)
                        {
                            len = ap.Data.Length;
                        }

                        VoiceMessage msg = new VoiceMessage(t.Dummy, (VoiceChatChannel)ap.Channel, ap.Data, len, false);
                        foreach (ReferenceHub client in ReferenceHub.AllHubs)
                        {
                            if (client == null || client.isLocalPlayer || client == t.Dummy || client.connectionToClient == null)
                            {
                                continue;
                            }

                            client.connectionToClient.Send(msg);
                        }
                    }

                    idx++;
                }

                a[t.PlayerId] = idx;
                if (idx < t.AudioFrames.Count)
                {
                    live = true;
                }

                ReferenceHub h = t.Dummy;
                if (!h.gameObject.TryGetComponent<DummyInputWrapper>(out var wrapper))
                {
                    wrapper = h.gameObject.AddComponent<DummyInputWrapper>();
                }

                wrapper.Bind(h.inventory);
                int li = l.TryGetValue(t.PlayerId, out int lv) ? lv : 0;
                bool dead = false;
                bool left = false;
                while (li < t.LifeEvents.Count && t.LifeEvents[li].FrameIndex <= i)
                {
                    LifecycleEvent ev = t.LifeEvents[li];
                    if (ev.FrameIndex == i)
                    {
                        if (ev.Type == EventType.RoleChanged)
                        {
                            RoleTypeId nr = (RoleTypeId)ev.RoleId;
                            h.roleManager.ServerSetRole(nr, RoleChangeReason.Respawn);
                        }
                        else if (ev.Type == EventType.Died)
                        {
                            h.playerStats.DealDamage(ev.FatalDamage.ToHandler());
                            dead = true;
                        }
                        else if (ev.Type == EventType.Left)
                        {
                            wrapper.Remove(ActionName.Shoot);
                            wrapper.Remove(ActionName.Reload);
                            p[t.PlayerId] = 0;
                            DespawnActor(t);
                            left = true;
                        }
                    }

                    li++;
                }

                l[t.PlayerId] = li;
                if (left)
                {
                    live = true;
                    continue;
                }

                if (dead)
                {
                    wrapper.Remove(ActionName.Shoot);
                    wrapper.Remove(ActionName.Reload);
                    p[t.PlayerId] = 0;
                    live = true;
                    continue;
                }

                int fi = i - sf;
                if (fi < 0)
                {
                    live = true;
                    continue;
                }

                if (fi >= t.Frames.Count)
                {
                    byte oldMask = p.TryGetValue(t.PlayerId, out byte ov) ? ov : (byte)0;
                    if (oldMask != 0)
                    {
                        wrapper.Remove(ActionName.Shoot);
                        wrapper.Remove(ActionName.Reload);
                        p[t.PlayerId] = 0;
                    }

                    continue;
                }

                FrameData f = t.Frames[fi];
                byte prev = p.TryGetValue(t.PlayerId, out byte pv) ? pv : (byte)0;
                ItemType cur = h.inventory.CurItem.TypeId;
                ItemType rec = (ItemType)(short)f.HeldItem;
                if (cur != rec)
                {
                    wrapper.Remove(ActionName.Shoot);
                    wrapper.Remove(ActionName.Reload);
                    prev = 0;
                    p[t.PlayerId] = 0;
                    List<ushort> ids = new List<ushort>(h.inventory.UserInventory.Items.Keys);
                    for (int j = 0; j < ids.Count; j++)
                    {
                        h.inventory.ServerRemoveItem(ids[j], null);
                    }

                    if (rec != ItemType.None)
                    {
                        ItemBase it = h.inventory.ServerAddItem(rec, ItemAddReason.AdminCommand, 0);
                        if (it != null)
                        {
                            if (it is AutosyncItem newAi)
                            {
                                wrapper.InjectInto(newAi);
                            }

                            if (it is Firearm fa)
                            {
                                fa.ApplyAttachmentsCode(f.Attachments, true);
                                fa.ServerResendAttachmentCode();
                            }

                            h.inventory.ServerSelectItem(it.ItemSerial);
                        }
                    }
                }

                h.TryOverridePosition(f.Pos);
                h.TryOverrideRotation(f.Rot);
                if (h.roleManager.CurrentRole is IFpcRole r)
                {
                    PlayerMovementState s = (PlayerMovementState)f.MoveState;
                    r.FpcModule.CurrentMovementState = s;
                    r.FpcModule.IsGrounded = f.Grounded;
                    Vector3 mv = Vector3.zero;
                    if (fi > 0)
                    {
                        FrameData pf = t.Frames[fi - 1];
                        mv = (f.Pos - pf.Pos) / Step;
                    }

                    SyncMotor(h, mv, f.Grounded, s);
                }

                SyncStats(h, f);
                if (h.inventory.CurInstance is AutosyncItem curAi)
                {
                    wrapper.InjectInto(curAi);
                }

                if ((f.InputMask & InputShoot) != 0)
                {
                    if ((prev & InputShoot) == 0)
                    {
                        wrapper.Add(ActionName.Shoot, false);
                    }
                }
                else if ((prev & InputShoot) != 0)
                {
                    wrapper.Remove(ActionName.Shoot);
                }

                if ((f.InputMask & InputReload) != 0 && (prev & InputReload) == 0)
                {
                    wrapper.Add(ActionName.Reload, true);
                }

                ReplayUse(h, f.InputMask);
                p[t.PlayerId] = f.InputMask;
                live = true;
            }

            while (d < Interacts.Count && Interacts[d].Timestamp <= e)
            {
                ReplayInteract(Interacts[d]);
                d++;
                live = true;
            }

            if (d < Interacts.Count)
            {
                live = true;
            }

            if (!live)
            {
                yield break;
            }

            i++;
            yield return Timing.WaitForSeconds(Step);
        }
    }
}
