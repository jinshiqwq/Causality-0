using System;
using Causality0.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using UnityEngine;
using VoiceChat.Networking;

namespace Causality0.Event.PlayerEvent
{

    public sealed class VoiceChat
    {
        public void Enable()
        {
            PlayerEvents.SendingVoiceMessage += OnSendingVoice;
        }

        public void Disable()
        {
            PlayerEvents.SendingVoiceMessage -= OnSendingVoice;
        }

        private void OnSendingVoice(PlayerSendingVoiceMessageEventArgs ev)
        {
            if (!Timeline.IsRec || global::Causality0.Causality0.Instance?.Config?.RecordVoice != true)
            {
                return;
            }

            ref VoiceMessage m = ref ev.Message;
            if (m.SpeakerNull || m.Speaker == null || m.Speaker.authManager?.DoNotTrack == true)
            {
                return;
            }

            if (!Timeline.Tracks.TryGetValue(m.Speaker.PlayerId, out ActorTrack t))
            {
                return;
            }

            byte[] src = m.Data;
            int n = m.DataLength;
            if (src == null || n <= 0)
            {
                return;
            }

            if (n > src.Length)
            {
                n = src.Length;
            }

            byte[] data = new byte[n];
            Array.Copy(src, 0, data, 0, n);
            float ts = Time.time - Timeline.RecordStartTime;
            if (ts < 0f)
            {
                ts = 0f;
            }

            t.AudioFrames.Add(new AudioPacket(ts, (byte)m.Channel, data, n));
        }
    }
}
