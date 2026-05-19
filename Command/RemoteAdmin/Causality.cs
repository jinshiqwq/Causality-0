using System;
using System.IO;
using CentralAuth;
using CommandSystem;
using Causality0.Core;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RemoteAdmin;
using UnityEngine;

namespace Causality0.Command.RemoteAdmin
{

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class Causality : ParentCommand
    {
        public Causality()
        {
            LoadGeneratedCommands();
        }

        public override void LoadGeneratedCommands()
        {
            RegisterCommand(new Start());
            RegisterCommand(new Stop());
            RegisterCommand(new Spawn());
            RegisterCommand(new Play());
            RegisterCommand(new Save());
            RegisterCommand(new Load());
            RegisterCommand(new Seek());
            RegisterCommand(new Fwd());
            RegisterCommand(new Back());
        }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "use start, stop, spawn, play, save, load, seek, fwd, back; alias: c0";
            return false;
        }

        public override string Command { get; } = "causality";

        public override string[] Aliases { get; } = new[] { "c0" };

        public override string Description { get; } = "Timeline controls.";
    }

    public sealed class Start : ICommand
    {
        public string Command { get; } = "start";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Start recording.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (sender is not PlayerCommandSender p)
            {
                response = "player only";
                return false;
            }

            if (p.ReferenceHub.roleManager.CurrentRole is not IFpcRole)
            {
                response = "role invalid";
                return false;
            }

            Timeline.StartRecord();
            response = "recording";
            return true;
        }
    }

    public sealed class Stop : ICommand
    {
        public string Command { get; } = "stop";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Stop recording.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            int n = Timeline.StopRecord();
            response = $"Timeline sealed: {n} frames.";
            return true;
        }
    }

    public sealed class Spawn : ICommand
    {
        public string Command { get; } = "spawn";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Spawn actor dummies.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (sender is not PlayerCommandSender p)
            {
                response = "player only";
                return false;
            }

            int n = 0;
            foreach (ActorTrack t in Timeline.Tracks.Values)
            {
                if (t.Frames.Count <= 0 || t.StartFrame > 0)
                {
                    continue;
                }

                if (Timeline.TrySpawnActor(t))
                {
                    n++;
                }
            }

            if (n == 0)
            {
                response = "no track";
                return false;
            }

            response = $"spawned {n}";
            return true;
        }
    }

    public sealed class Play : ICommand
    {
        public string Command { get; } = "play";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Play timeline on actors.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            int cur = SeedSynchronizer.Seed;
            int rec = Timeline.MapSeed;
            if (cur != rec)
            {
                response = $"Playback failed! Current map seed [{cur}] does not match replay seed [{rec}]. Use load first to schedule a forced restart with the replay seed.";
                return false;
            }

            if (!Timeline.StartPlay())
            {
                response = "Actor missing.";
                return false;
            }

            response = "Playback started.";
            return true;
        }
    }

    public sealed class Save : ICommand
    {
        public string Command { get; } = "save";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Save tracks.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "filename required";
                return false;
            }

            string p = Path.Combine("CausalityRecords", arguments.At(0) + ".c0");
            Serializer.Save(p);
            response = p;
            return true;
        }
    }

    public sealed class Load : ICommand
    {
        public string Command { get; } = "load";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Load tracks.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "filename required";
                return false;
            }

            string p = Path.Combine("CausalityRecords", arguments.At(0) + ".c0");
            if (!Serializer.Load(p))
            {
                string s = Serializer.LastErr;
                response = string.IsNullOrWhiteSpace(s) ? "Load failed." : $"Load failed: {s}";
                return false;
            }

            int cur = SeedSynchronizer.Seed;
            int rec = Timeline.MapSeed;
            if (cur != rec)
            {
                Causality0.Instance.ServerEvent.Seed = rec;
                Causality0.Instance.ServerEvent.UseSeed = true;
                Round.Restart();
                response = $"Replay loaded. Current map seed [{cur}] does not match replay seed [{rec}]. Forced round restart scheduled with replay seed [{rec}]. Load the replay again next round.";
                return true;
            }

            Timeline.ApplyWorldState();
            response = $"Replay loaded: {p}";
            return true;
        }
    }

    public sealed class Seek : ICommand
    {
        public string Command { get; } = "seek";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Seek to a specific time (seconds) in the playing timeline.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (!Timeline.IsPlay)
            {
                response = "no playback running";
                return false;
            }

            if (arguments.Count < 1 || !float.TryParse(arguments.At(0), out float seconds) || seconds < 0f)
            {
                response = "usage: c0 seek <seconds>";
                return false;
            }

            Timeline.SeekToTime(seconds);
            response = $"seeked to {seconds:F1}s";
            return true;
        }
    }

    public sealed class Fwd : ICommand
    {
        public string Command { get; } = "fwd";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Skip forward N seconds (default 10s) in the playing timeline.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (!Timeline.IsPlay)
            {
                response = "no playback running";
                return false;
            }

            float delta = 10f;
            if (arguments.Count >= 1 && (!float.TryParse(arguments.At(0), out delta) || delta <= 0f))
            {
                response = "usage: c0 fwd [seconds]";
                return false;
            }

            Timeline.SkipForward(delta);
            response = $"skipped forward {delta:F1}s";
            return true;
        }
    }

    public sealed class Back : ICommand
    {
        public string Command { get; } = "back";

        public string[] Aliases { get; } = Array.Empty<string>();

        public string Description { get; } = "Skip backward N seconds (default 10s) in the playing timeline.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.CheckPermission(PlayerPermissions.PlayersManagement))
            {
                response = "no permission";
                return false;
            }

            if (!Timeline.IsPlay)
            {
                response = "no playback running";
                return false;
            }

            float delta = 10f;
            if (arguments.Count >= 1 && (!float.TryParse(arguments.At(0), out delta) || delta <= 0f))
            {
                response = "usage: c0 back [seconds]";
                return false;
            }

            Timeline.SkipBack(delta);
            response = $"skipped back {delta:F1}s";
            return true;
        }
    }
}
