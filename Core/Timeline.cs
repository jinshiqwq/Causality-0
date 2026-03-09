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
using Mirror;
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

    public static int MapSeed { get; set; }

    public static int CurrentFps { get; set; } = 60;

    public static float Step => 1f / Mathf.Max(1, CurrentFps);

    public static float RecordStartTime;

    public static int RecFrame;

    public const byte InputShoot = 1;

    public const byte InputReload = 2;

    public const byte InputUse = 4;

    public const byte InputUseCancel = 8;

    private static Dictionary<int, byte> _im = new Dictionary<int, byte>();

    private static Dictionary<int, ProjectileTrack> _pm = new Dictionary<int, ProjectileTrack>();

    private static Dictionary<byte, DoorVariant> _dm = new Dictionary<byte, DoorVariant>();

    public static bool IsRec => _rh.IsRunning;

    public static bool IsPlay => _ph.IsRunning;

    private static CoroutineHandle _rh;

    private static CoroutineHandle _ph;

    public static void Clear()
    {
        Tracks.Clear();
        ProjTracks.Clear();
        Interacts.Clear();
        MapSeed = 0;
        RecordStartTime = 0f;
        RecFrame = 0;
        _im.Clear();
        _pm.Clear();
        _dm.Clear();
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
                Role = (sbyte)hub.GetRoleId()
            };
            t.LifeEvents.Add(LifecycleEvent.NewRole(0, hub.GetRoleId()));
            Tracks[hub.PlayerId] = t;
        }

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
            if (t.Dummy != null && t.Frames.Count > 0)
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
        if (h?.inventory?.CurInstance is not UsableItem u)
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
            StartFrame = MaxFrames(),
            Owner = new Footprint(h),
            Live = p
        };
        ProjTracks.Add(tr);
        _pm[id] = tr;
    }

    private static T SpawnProjectileEntity<T>(ItemType t, Vector3 p, Quaternion r, Footprint o) where T : ThrownProjectile
    {
        if (!InventoryItemLoader.TryGetItem<ThrowableItem>(t, out var it) || it.Projectile is not T pr)
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
        ThrownProjectile p = SpawnProjectileEntity<ThrownProjectile>(tr.ProjectileType, f.Pos, f.Rot, tr.Owner);
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

        switch (tr.ProjectileType)
        {
            case ItemType.GrenadeHE:
                ExplosionGrenade g1 = tr.Puppet as ExplosionGrenade ?? SpawnProjectileEntity<ExplosionGrenade>(ItemType.GrenadeHE, f.Pos, f.Rot, tr.Owner);
                if (g1 != null)
                {
                    g1.ServerFuseEnd();
                }
                else
                {
                    ExplosionUtils.ServerExplode(f.Pos, tr.Owner, ExplosionType.Grenade);
                }
                break;
            case ItemType.SCP018:
                Scp018Projectile g2 = tr.Puppet as Scp018Projectile;
                if (g2 != null)
                {
                    g2.ServerFuseEnd();
                }
                else
                {
                    ExplosionUtils.ServerExplode(f.Pos, tr.Owner, ExplosionType.SCP018);
                }
                break;
            case ItemType.GrenadeFlash:
                FlashbangGrenade g3 = tr.Puppet as FlashbangGrenade ?? SpawnProjectileEntity<FlashbangGrenade>(ItemType.GrenadeFlash, f.Pos, f.Rot, tr.Owner);
                if (g3 != null)
                {
                    g3.ServerFuseEnd();
                }
                break;
            case ItemType.SCP2176:
                Scp2176Projectile g4 = tr.Puppet as Scp2176Projectile ?? SpawnProjectileEntity<Scp2176Projectile>(ItemType.SCP2176, f.Pos, f.Rot, tr.Owner);
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

            RecFrame++;
            yield return Timing.WaitForSeconds(Step);
        }
    }

    private static IEnumerator<float> RunPlay()
    {
        Dictionary<int, int> a = new Dictionary<int, int>();
        Dictionary<int, byte> p = new Dictionary<int, byte>();
        Dictionary<int, int> l = new Dictionary<int, int>();
        foreach (ActorTrack t in Tracks.Values)
        {
            a[t.PlayerId] = 0;
            p[t.PlayerId] = 0;
            l[t.PlayerId] = 0;
            t.LifeEvents.Sort((x, y) => x.FrameIndex.CompareTo(y.FrameIndex));
        }

        int i = 0;
        int d = 0;
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

            foreach (ActorTrack t in Tracks.Values)
            {
                if (t.Dummy == null)
                {
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
                while (li < t.LifeEvents.Count && t.LifeEvents[li].FrameIndex <= i)
                {
                    LifecycleEvent ev = t.LifeEvents[li];
                    if (ev.FrameIndex == i)
                    {
                        if (ev.Type == EventType.RoleChanged)
                        {
                            RoleTypeId r = (RoleTypeId)ev.RoleId;
                            h.roleManager.ServerSetRole(r, RoleChangeReason.Respawn);
                        }
                        else if (ev.Type == EventType.Died)
                        {
                            h.playerStats.DealDamage(ev.FatalDamage.ToHandler());
                            dead = true;
                        }
                    }

                    li++;
                }

                l[t.PlayerId] = li;
                if (dead)
                {
                    wrapper.Remove(ActionName.Shoot);
                    wrapper.Remove(ActionName.Reload);
                    p[t.PlayerId] = 0;
                    live = true;
                    continue;
                }

                if (i >= t.Frames.Count)
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

                FrameData f = t.Frames[i];
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
                    if (i > 0)
                    {
                        FrameData pf = t.Frames[i - 1];
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
