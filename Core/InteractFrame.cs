namespace Causality0.Core;

public struct InteractFrame
{
    public float Timestamp;
    public int PlayerId;
    public byte DoorId;
    public byte Act;
    public bool CanOpen;

    public InteractFrame(float timestamp, int playerId, byte doorId, byte act, bool canOpen)
    {
        Timestamp = timestamp;
        PlayerId = playerId;
        DoorId = doorId;
        Act = act;
        CanOpen = canOpen;
    }
}