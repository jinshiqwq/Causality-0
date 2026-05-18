using UnityEngine;

namespace Causality0.Core
{

    public struct InteractFrame
    {
        public float Timestamp;
        public int PlayerId;
        public byte DoorId;
        public byte Act;
        public bool CanOpen;
        public bool HasPos;
        public Vector3 Pos;

        public InteractFrame(float timestamp, int playerId, byte doorId, byte act, bool canOpen, Vector3 pos, bool hasPos)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            DoorId = doorId;
            Act = act;
            CanOpen = canOpen;
            HasPos = hasPos;
            Pos = pos;
        }
    }
}
