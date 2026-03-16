using System.IO;
using System.Text;
using SharpCompress.Compressors.LZMA;
using InventorySystem.Items;
using PlayerRoles;

namespace Causality0.Core;

public static class Serializer
{
    private static readonly byte[] RawMagic = { 0x04, 0x43, 0x41, 0x55, 0x53 };
    private static readonly byte[] LzmaMagic = { 0x43, 0x30, 0x4C, 0x5A, 0x4D, 0x41 };
    private const int OldSeed = 114514;

    public static string LastErr { get; private set; } = string.Empty;

    public static void Save(string path)
    {
        LastErr = string.Empty;
        string d = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(d))
        {
            Directory.CreateDirectory(d);
        }

        C0CompressionMode m = global::Causality0.Causality0.Instance?.Config?.ReplayCompression ?? C0CompressionMode.Lzma;
        switch (m)
        {
            case C0CompressionMode.None:
                using (FileStream f = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    SaveRaw(f);
                }
                return;
            default:
                SavePacked(path, SaveLzma);
                return;
        }
    }

    private static void SavePacked(string path, System.Action<MemoryStream, FileStream> fn)
    {
        using MemoryStream ms = new MemoryStream();
        SaveRaw(ms);
        ms.Position = 0;
        using FileStream f = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        fn(ms, f);
    }

    private static void SaveLzma(MemoryStream s, FileStream d)
    {
        d.Write(LzmaMagic, 0, LzmaMagic.Length);
        using BinaryWriter w = new BinaryWriter(d, Encoding.UTF8, true);
        w.Write(s.Length);
        w.Flush();
        using LzmaStream z = LzmaStream.Create(GetLzmaSettings(), false, d);
        d.Write(z.Properties, 0, z.Properties.Length);
        s.CopyTo(z);
    }

    private static void SaveRaw(Stream s)
    {
        using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
        w.Write("CAUS");
        w.Write((byte)16);
        w.Write(Timeline.MapSeed);
        w.Write(Timeline.CurrentFps);
        w.Write(Timeline.Tracks.Count);
        foreach (ActorTrack t in Timeline.Tracks.Values)
        {
            w.Write(t.PlayerId);
            w.Write(t.ActorName ?? string.Empty);
            w.Write(t.Role);
            w.Write(t.StartFrame);
            w.Write(t.Frames.Count);
            for (int i = 0; i < t.Frames.Count; i++)
            {
                FrameData f = t.Frames[i];
                w.Write(f.Pos.x);
                w.Write(f.Pos.y);
                w.Write(f.Pos.z);
                w.Write(f.Rot.x);
                w.Write(f.Rot.y);
                w.Write(f.MoveState);
                w.Write(f.Grounded);
                w.Write(f.HeldItem);
                w.Write(f.IsPrimaryAction);
                w.Write(f.InputMask);
                w.Write(f.Attachments);
                w.Write(f.Hp);
                w.Write(f.Ahp);
            }

            w.Write(t.AudioFrames.Count);
            for (int i = 0; i < t.AudioFrames.Count; i++)
            {
                AudioPacket p = t.AudioFrames[i];
                int n = p.DataLength;
                if (p.Data == null || n < 0)
                {
                    n = 0;
                }
                else if (n > p.Data.Length)
                {
                    n = p.Data.Length;
                }

                w.Write(p.Timestamp);
                w.Write(p.Channel);
                w.Write(n);
                if (n > 0)
                {
                    w.Write(p.Data, 0, n);
                }
            }

            w.Write(t.LifeEvents.Count);
            for (int i = 0; i < t.LifeEvents.Count; i++)
            {
                LifecycleEvent x = t.LifeEvents[i];
                w.Write(x.FrameIndex);
                w.Write((byte)x.Type);
                w.Write(x.RoleId);
                int n = x.FatalDamage.Length;
                if (x.FatalDamage.Raw == null || n <= 0)
                {
                    n = 0;
                }
                else if (n > x.FatalDamage.Raw.Length)
                {
                    n = x.FatalDamage.Raw.Length;
                }

                w.Write(n);
                if (n > 0)
                {
                    w.Write(x.FatalDamage.Raw, 0, n);
                }
            }
        }

        w.Write(Timeline.Interacts.Count);
        for (int i = 0; i < Timeline.Interacts.Count; i++)
        {
            InteractFrame x = Timeline.Interacts[i];
            w.Write(x.Timestamp);
            w.Write(x.PlayerId);
            w.Write(x.DoorId);
            w.Write(x.Act);
            w.Write(x.CanOpen);
            w.Write(x.HasPos);
            if (x.HasPos)
            {
                w.Write(x.Pos.x);
                w.Write(x.Pos.y);
                w.Write(x.Pos.z);
            }
        }

        w.Write(Timeline.HasWorldState);
        if (Timeline.HasWorldState)
        {
            w.Write(Timeline.WorldPickups.Count);
            for (int i = 0; i < Timeline.WorldPickups.Count; i++)
            {
                PickupData x = Timeline.WorldPickups[i];
                w.Write(x.Id);
                w.Write(x.T);
                w.Write(x.Pos.x);
                w.Write(x.Pos.y);
                w.Write(x.Pos.z);
                w.Write(x.Rot.x);
                w.Write(x.Rot.y);
                w.Write(x.Rot.z);
                w.Write(x.Rot.w);
                w.Write(x.At);
                w.Write(x.Am);
                w.Write(x.Locked);
            }

            w.Write(Timeline.PickupOps.Count);
            for (int i = 0; i < Timeline.PickupOps.Count; i++)
            {
                PickupOp x = Timeline.PickupOps[i];
                w.Write(x.Ts);
                w.Write((byte)x.Act);
                w.Write(x.Id);
                if (x.Act != PickupAct.Remove)
                {
                    PickupData pd = x.Data;
                    w.Write(pd.Id);
                    w.Write(pd.T);
                    w.Write(pd.Pos.x);
                    w.Write(pd.Pos.y);
                    w.Write(pd.Pos.z);
                    w.Write(pd.Rot.x);
                    w.Write(pd.Rot.y);
                    w.Write(pd.Rot.z);
                    w.Write(pd.Rot.w);
                    w.Write(pd.At);
                    w.Write(pd.Am);
                    w.Write(pd.Locked);
                }
            }

        }

        w.Write(Timeline.ProjTracks.Count);
        for (int i = 0; i < Timeline.ProjTracks.Count; i++)
        {
            ProjectileTrack x = Timeline.ProjTracks[i];
            w.Write((ushort)x.ProjectileType);
            w.Write(x.StartFrame);
            w.Write(x.OwnerId);
            w.Write(x.Frames.Count);
            for (int j = 0; j < x.Frames.Count; j++)
            {
                ProjectileFrame f = x.Frames[j];
                w.Write(f.Pos.x);
                w.Write(f.Pos.y);
                w.Write(f.Pos.z);
                w.Write(f.Rot.x);
                w.Write(f.Rot.y);
                w.Write(f.Rot.z);
                w.Write(f.Rot.w);
            }
        }

        w.Flush();
    }

    public static bool Load(string path)
    {
        LastErr = string.Empty;
        if (!File.Exists(path))
        {
            LastErr = "file missing";
            return false;
        }

        string t = string.Empty;
        try
        {
            C0CompressionMode m = DetectMode(path);
            t = m.ToString();
            switch (m)
            {
                case C0CompressionMode.None:
                    using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        bool ok = LoadRaw(f);
                        if (!ok)
                        {
                            Timeline.Clear();
                        }

                        return ok;
                    }
                default:
                    using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long n = ReadLzmaHeader(f, out byte[] p);
                        using LzmaStream z = LzmaStream.Create(p, f, -1L, n, true);
                        return LoadPacked(ms => z.CopyTo(ms));
                    }
            }
        }
        catch (System.Exception ex)
        {
            LastErr = string.IsNullOrEmpty(t) ? ex.GetType().Name + ": " + ex.Message : t + ": " + ex.GetType().Name + ": " + ex.Message;
            Timeline.Clear();
            return false;
        }
    }

    private static bool LoadPacked(System.Action<MemoryStream> fn)
    {
        using MemoryStream ms = new MemoryStream();
        fn(ms);
        ms.Position = 0;
        bool ok = LoadRaw(ms);
        if (!ok)
        {
            if (string.IsNullOrEmpty(LastErr))
            {
                LastErr = "invalid replay payload";
            }

            Timeline.Clear();
        }

        return ok;
    }

    private static long ReadLzmaHeader(Stream s, out byte[] p)
    {
        if (!IsMagic(s, LzmaMagic))
        {
            throw new InvalidDataException("invalid lzma replay header");
        }

        s.Position = LzmaMagic.Length;
        using BinaryReader r = new BinaryReader(s, Encoding.UTF8, true);
        long n = r.ReadInt64();
        if (n < 0)
        {
            throw new InvalidDataException("invalid lzma replay size");
        }

        p = r.ReadBytes(5);
        if (p.Length != 5)
        {
            throw new InvalidDataException("invalid lzma replay properties");
        }

        return n;
    }

    private static C0CompressionMode DetectMode(string path)
    {
        using FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (IsMagic(f, RawMagic))
        {
            return C0CompressionMode.None;
        }

        if (IsMagic(f, LzmaMagic))
        {
            return C0CompressionMode.Lzma;
        }

        throw new InvalidDataException("unknown replay compression");
    }

    private static bool LoadRaw(Stream s)
    {
        using BinaryReader r = new BinaryReader(s, Encoding.UTF8, true);
        if (r.ReadString() != "CAUS")
        {
            LastErr = "invalid replay header";
            return false;
        }

        byte v = r.ReadByte();
        Timeline.Clear();
        Timeline.MapSeed = FixSeed(r.ReadInt32(), v);
        if (v >= 8)
        {
            int fps = r.ReadInt32();
            Timeline.CurrentFps = fps > 0 ? fps : 60;
        }
        else
        {
            Timeline.CurrentFps = 15;
        }

        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            ActorTrack t = new ActorTrack
            {
                PlayerId = r.ReadInt32(),
                ActorName = r.ReadString(),
                Role = r.ReadSByte(),
                StartFrame = v >= 10 ? r.ReadInt32() : 0
            };
            int m = r.ReadInt32();
            for (int j = 0; j < m; j++)
            {
                float px = r.ReadSingle();
                float py = r.ReadSingle();
                float pz = r.ReadSingle();
                float rx = r.ReadSingle();
                float ry = r.ReadSingle();
                byte ms = r.ReadByte();
                bool g = r.ReadBoolean();
                ushort hi = 0;
                bool pa = false;
                byte im = 0;
                uint at = 0;
                float hp = -1f;
                float ah = -1f;
                if (v >= 2)
                {
                    hi = r.ReadUInt16();
                    pa = r.ReadBoolean();
                }

                if (v >= 5)
                {
                    im = r.ReadByte();
                }

                if (v >= 6)
                {
                    at = r.ReadUInt32();
                }

                if (v >= 7)
                {
                    hp = r.ReadSingle();
                    ah = r.ReadSingle();
                }

                t.Frames.Add(new FrameData(new UnityEngine.Vector3(px, py, pz), new UnityEngine.Vector2(rx, ry), ms, g, hi, pa, im, at, hp, ah));
            }

            if (v >= 3)
            {
                int a = r.ReadInt32();
                for (int j = 0; j < a; j++)
                {
                    float ts = r.ReadSingle();
                    byte ch = r.ReadByte();
                    int len = r.ReadInt32();
                    byte[] data = r.ReadBytes(len);
                    t.AudioFrames.Add(new AudioPacket(ts, ch, data, data.Length));
                }
            }

            if (v >= 9)
            {
                int c = r.ReadInt32();
                for (int j = 0; j < c; j++)
                {
                    int fi = r.ReadInt32();
                    EventType tp = (EventType)r.ReadByte();
                    sbyte rid = r.ReadSByte();
                    int len = r.ReadInt32();
                    byte[] raw = r.ReadBytes(len);
                    t.LifeEvents.Add(new LifecycleEvent(fi, tp, rid, new DamageData(raw, raw.Length)));
                }
            }
            else
            {
                t.LifeEvents.Add(LifecycleEvent.NewRole(0, (RoleTypeId)t.Role));
            }

            Timeline.Tracks[t.PlayerId] = t;
        }

        if (v >= 7)
        {
            int c = r.ReadInt32();
            for (int i = 0; i < c; i++)
            {
                float ts = r.ReadSingle();
                int id = r.ReadInt32();
                byte doorId = r.ReadByte();
                byte act = r.ReadByte();
                bool canOpen = r.ReadBoolean();
                bool hasPos = false;
                UnityEngine.Vector3 pos = default;
                if (v >= 14)
                {
                    hasPos = r.ReadBoolean();
                    if (hasPos)
                    {
                        pos = new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    }
                }

                Timeline.Interacts.Add(new InteractFrame(ts, id, doorId, act, canOpen, pos, hasPos));
            }
        }

        if (v >= 11)
        {
            Timeline.HasWorldState = r.ReadBoolean();
            if (Timeline.HasWorldState)
            {
                int c = r.ReadInt32();
                for (int i = 0; i < c; i++)
                {
                    Timeline.WorldPickups.Add(new PickupData(r.ReadInt32(), (ItemType)r.ReadUInt16(), new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new UnityEngine.Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), r.ReadUInt32(), r.ReadUInt16(), r.ReadBoolean()));
                }

                c = r.ReadInt32();
                for (int i = 0; i < c; i++)
                {
                    float ts = r.ReadSingle();
                    PickupAct a = (PickupAct)r.ReadByte();
                    int id = r.ReadInt32();
                    if (a != PickupAct.Remove)
                    {
                        PickupData d = new PickupData(r.ReadInt32(), (ItemType)r.ReadUInt16(), new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new UnityEngine.Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), r.ReadUInt32(), r.ReadUInt16(), r.ReadBoolean());
                        Timeline.PickupOps.Add(new PickupOp(ts, a, id, d));
                    }
                    else
                    {
                        Timeline.PickupOps.Add(PickupOp.NewRemove(ts, id));
                    }
                }

                if (v <= 15)
                {
                    c = r.ReadInt32();
                    for (int i = 0; i < c; i++)
                    {
                        if (v >= 15)
                        {
                            r.ReadString();
                        }

                        r.ReadSingle();
                        r.ReadSingle();
                        r.ReadSingle();
                        r.ReadByte();
                        r.ReadBoolean();
                        r.ReadBoolean();
                        int n2 = r.ReadInt32();
                        for (int j = 0; j < n2; j++)
                        {
                            r.ReadInt32();
                            r.ReadUInt16();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadSingle();
                            r.ReadUInt32();
                            r.ReadUInt16();
                            r.ReadBoolean();
                        }
                    }

                    c = r.ReadInt32();
                    for (int i = 0; i < c; i++)
                    {
                        r.ReadSingle();
                        if (v >= 15)
                        {
                            r.ReadString();
                        }

                        r.ReadSingle();
                        r.ReadSingle();
                        r.ReadSingle();
                        r.ReadByte();
                        r.ReadBoolean();
                        r.ReadBoolean();
                    }
                }
            }
        }

        if (v >= 12)
        {
            int c = r.ReadInt32();
            for (int i = 0; i < c; i++)
            {
                ProjectileTrack x = new ProjectileTrack
                {
                    ProjectileType = (ItemType)r.ReadUInt16(),
                    StartFrame = r.ReadInt32(),
                    OwnerId = r.ReadInt32()
                };
                int n2 = r.ReadInt32();
                for (int j = 0; j < n2; j++)
                {
                    x.Frames.Add(new ProjectileFrame(new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), new UnityEngine.Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle())));
                }

                Timeline.ProjTracks.Add(x);
            }
        }

        return true;
    }

    private static bool IsMagic(Stream s, byte[] x)
    {
        if (!s.CanRead)
        {
            return false;
        }

        long p = 0;
        if (s.CanSeek)
        {
            p = s.Position;
        }

        byte[] b = new byte[x.Length];
        int n = s.Read(b, 0, b.Length);
        if (s.CanSeek)
        {
            s.Position = p;
        }

        if (n != x.Length)
        {
            return false;
        }

        for (int i = 0; i < x.Length; i++)
        {
            if (b[i] != x[i])
            {
                return false;
            }
        }

        return true;
    }

    private static C0CompressionPreset GetPreset()
    {
        return global::Causality0.Causality0.Instance?.Config?.ReplayCompressionPreset ?? C0CompressionPreset.Normal;
    }

    private static LzmaEncoderProperties GetLzmaSettings()
    {
        switch (GetPreset())
        {
            case C0CompressionPreset.FastestSpeed:
                return new LzmaEncoderProperties(false, 1 << 20, 16);
            case C0CompressionPreset.FastSpeed:
                return new LzmaEncoderProperties(false, 1 << 22, 24);
            case C0CompressionPreset.HighCompression:
                return new LzmaEncoderProperties(false, 1 << 25, 48);
            case C0CompressionPreset.MaximumCompression:
                return new LzmaEncoderProperties(false, 1 << 26, 64);
            default:
                return new LzmaEncoderProperties(false, 1 << 24, 32);
        }
    }

    private static int FixSeed(int s, byte v)
    {
        if (s <= 0)
        {
            return OldSeed;
        }

        if (v <= 10 && s < 1000)
        {
            return OldSeed;
        }

        return s;
    }
}
