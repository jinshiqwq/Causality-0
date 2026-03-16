using System.Collections.Generic;
using InventorySystem.Items;
using UnityEngine;

namespace Causality0.Core;

public enum PickupAct : byte
{
    Add,
    Move,
    Remove
}

public struct PickupData
{
    public int Id;
    public ushort T;
    public Vector3 Pos;
    public Quaternion Rot;
    public uint At;
    public ushort Am;
    public bool Locked;

    public PickupData(int id, ItemType t, Vector3 pos, Quaternion rot, uint at, ushort am, bool locked)
    {
        Id = id;
        T = (ushort)t;
        Pos = pos;
        Rot = rot;
        At = at;
        Am = am;
        Locked = locked;
    }

    public ItemType ItemType => (ItemType)T;
}

public struct PickupOp
{
    public float Ts;
    public PickupAct Act;
    public int Id;
    public PickupData Data;

    public PickupOp(float ts, PickupAct act, int id, PickupData data)
    {
        Ts = ts;
        Act = act;
        Id = id;
        Data = data;
    }

    public static PickupOp NewAdd(float ts, PickupData data)
    {
        return new PickupOp(ts, PickupAct.Add, data.Id, data);
    }

    public static PickupOp NewMove(float ts, PickupData data)
    {
        return new PickupOp(ts, PickupAct.Move, data.Id, data);
    }

    public static PickupOp NewRemove(float ts, int id)
    {
        return new PickupOp(ts, PickupAct.Remove, id, default);
    }
}

