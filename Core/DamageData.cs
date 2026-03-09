using System;
using Mirror;
using PlayerStatsSystem;

namespace Causality0.Core;

public struct DamageData
{
    public byte[] Raw;
    public int Length;

    public DamageData(byte[] raw, int length)
    {
        Raw = raw;
        Length = length;
    }

    public bool HasValue => Raw != null && Length > 0;

    public static DamageData FromHandler(DamageHandlerBase h)
    {
        if (h == null)
        {
            return default;
        }

        NetworkWriterPooled w = NetworkWriterPool.Get();
        try
        {
            w.WriteDamageHandler(h);
            byte[] buf = w.ToArray();
            return new DamageData(buf, buf.Length);
        }
        catch
        {
            return default;
        }
        finally
        {
            NetworkWriterPool.Return(w);
        }
    }

    public DamageHandlerBase ToHandler()
    {
        if (!HasValue)
        {
            return new UniversalDamageHandler(-1f, DeathTranslations.Unknown);
        }

        try
        {
            int n = Length;
            if (n > Raw.Length)
            {
                n = Raw.Length;
            }

            using NetworkReaderPooled r = NetworkReaderPool.Get(new ArraySegment<byte>(Raw, 0, n));
            return r.ReadDamageHandler();
        }
        catch
        {
            return new UniversalDamageHandler(-1f, DeathTranslations.Unknown);
        }
    }
}