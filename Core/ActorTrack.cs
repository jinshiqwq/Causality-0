using System.Collections.Generic;
using PlayerRoles;

namespace Causality0.Core
{

    public struct AudioPacket
    {
        public float Timestamp;
        public byte Channel;
        public byte[] Data;
        public int DataLength;

        public AudioPacket(float timestamp, byte channel, byte[] data, int dataLength)
        {
            Timestamp = timestamp;
            Channel = channel;
            Data = data;
            DataLength = dataLength;
        }
    }

    public sealed class ActorTrack
    {
        public int PlayerId { get; set; }

        public string ActorName { get; set; }

        public sbyte Role { get; set; }

        public int StartFrame { get; set; }

        public List<FrameData> Frames { get; } = new List<FrameData>();

        public List<AudioPacket> AudioFrames { get; } = new List<AudioPacket>();

        public List<LifecycleEvent> LifeEvents { get; } = new List<LifecycleEvent>();

        public ReferenceHub Dummy { get; set; }
    }
}
