using System.IO;
using PlayerRoles;

namespace Causality0.Core;

public static class Serializer
{
    public static void Save(string path)
    {
        string d = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(d))
        {
            Directory.CreateDirectory(d);
        }

        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter w = new BinaryWriter(fs);
        w.Write("CAUS");
        w.Write((byte)9);
        w.Write(Timeline.MapSeed);
        w.Write(Timeline.CurrentFps);
        w.Write(Timeline.Tracks.Count);
        foreach (ActorTrack t in Timeline.Tracks.Values)
        {
            w.Write(t.PlayerId);
            w.Write(t.ActorName ?? string.Empty);
            w.Write(t.Role);
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
        }
    }

    public static bool Load(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader r = new BinaryReader(fs);
        if (r.ReadString() != "CAUS")
        {
            return false;
        }

        byte v = r.ReadByte();
        Timeline.Clear();
        Timeline.MapSeed = r.ReadInt32();
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
                Role = r.ReadSByte()
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
                Timeline.Interacts.Add(new InteractFrame(r.ReadSingle(), r.ReadInt32(), r.ReadByte(), r.ReadByte(), r.ReadBoolean()));
            }
        }

        return true;
    }
}
