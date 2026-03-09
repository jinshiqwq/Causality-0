using System;
using CentralAuth;
using CommandSystem;
using Causality0.Core;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using RemoteAdmin;

namespace Causality0.Command.RemoteAdmin;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class SpawnActor : ICommand
{
    public string Command { get; } = "spawnactor";

    public string[] Aliases { get; } = Array.Empty<string>();

    public string Description { get; } = "Spawn an actor dummy at your position.";

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

        ReferenceHub h = DummyUtils.SpawnDummy("Actor-01");
        if (h == null)
        {
            response = "spawn failed";
            return false;
        }

        h.roleManager.ServerSetRole(RoleTypeId.ClassD, RoleChangeReason.RemoteAdmin);
        if (!h.TryOverridePosition(p.ReferenceHub.transform.position))
        {
            response = "spawned but move failed";
            return false;
        }

        h.TryOverrideRotation(Vector3.zero);
        Timeline.SetActor(h);
        response = $"ok {h.PlayerId}";
        return true;
    }
}
