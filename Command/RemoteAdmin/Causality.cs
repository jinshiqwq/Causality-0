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

namespace Causality0.Command.RemoteAdmin;

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
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = "use start, stop, spawn, play, save, load; alias: c0";
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

        Timeline.StartRecord(p.ReferenceHub);
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
            if (t.Frames.Count <= 0)
            {
                continue;
            }

            ReferenceHub h = DummyUtils.SpawnDummy(t.ActorName);
            if (h == null)
            {
                continue;
            }

            Vector3 pos = t.Frames[0].Pos;
            Vector2 rot = t.Frames[0].Rot;
            RoleTypeId safeRole = (RoleTypeId)(sbyte)t.Role;
            for (int i = 0; i < t.LifeEvents.Count; i++)
            {
                if (t.LifeEvents[i].Type == EventType.RoleChanged)
                {
                    safeRole = (RoleTypeId)t.LifeEvents[i].RoleId;
                    break;
                }
            }

            h.roleManager.ServerSetRole(safeRole, RoleChangeReason.RemoteAdmin);
            Timing.CallDelayed(0.1f, () =>
            {
                if (h == null)
                {
                    return;
                }

                h.TryOverridePosition(pos);
                h.TryOverrideRotation(rot);
                t.Dummy = h;
            });
            n++;
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
            response = "Load failed.";
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

        response = $"Replay loaded: {p}";
        return true;
    }
}
