using UnityEngine;

namespace Causality0.Core
{

    public struct FrameData
    {
        public Vector3 Pos;
        public Vector2 Rot;
        public byte MoveState;
        public bool Grounded;
        public ushort HeldItem;
        public bool IsPrimaryAction;
        public byte InputMask;
        public uint Attachments;
        public float Hp;
        public float Ahp;

        public FrameData(Vector3 pos, Vector2 rot, byte moveState, bool grounded, ushort heldItem, bool isPrimaryAction, byte inputMask, uint attachments, float hp, float ahp)
        {
            Pos = pos;
            Rot = rot;
            MoveState = moveState;
            Grounded = grounded;
            HeldItem = heldItem;
            IsPrimaryAction = isPrimaryAction;
            InputMask = inputMask;
            Attachments = attachments;
            Hp = hp;
            Ahp = ahp;
        }
    }
}
