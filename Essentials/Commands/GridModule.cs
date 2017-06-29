using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.Entity.EntityComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using VRage.Game;

namespace Essentials
{
    [Category("grids")]
    public class GridModule : CommandModule
    {
        [Command("setowner", "Sets grid ownership to the given player or ID.", "Usage: setowner <grid> <newowner>")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void SetOwner(string gridName, string playerName)
        {
            var firstArg = Context.Args.FirstOrDefault();
            Utilities.TryGetEntityByNameOrId(gridName, out IMyEntity entity);

            if (!(entity is IMyCubeGrid grid))
            {
                Context.Respond($"Grid {gridName} not found.");
                return;
            }

            var secondArg = Context.Args.ElementAtOrDefault(1);
            long identityId;
            if (!long.TryParse(playerName, out identityId))
            {
                var player = Context.Torch.Multiplayer.GetPlayerByName(playerName);
                if (player == null)
                {
                    Context.Respond($"Player {playerName} not found.");
                    return;
                }
                identityId = player.IdentityId;
            }

            grid.ChangeGridOwnership(identityId, MyOwnershipShareModeEnum.Faction);
            Context.Respond($"Transferred ownership of {grid.DisplayName} to {identityId}");

            /*
            grid.GetBlocks(new List<IMySlimBlock>(), block =>
            {
                var cubeBlock = block.FatBlock as MyCubeBlock;
                var ownerComp = cubeBlock?.Components.Get<MyEntityOwnershipComponent>();
                if (ownerComp == null)
                    return false;

                cubeBlock?.ChangeOwner(0, MyOwnershipShareModeEnum.All);
                cubeBlock?.ChangeOwner(identityId, ownerComp.ShareMode);
                return false;
            });*/
        }

        [Command("static large", "Makes all large grids static.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void StaticLarge()
        {
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(g => g.GridSizeEnum == MyCubeSize.Large))
                grid.ConvertToStatic();
        }

        [Command("list", "List all grids owned by you.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var sb = new StringBuilder("Grids:\n");
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BigOwners.Contains(Context.Player?.IdentityId ?? 0))
                    sb.AppendLine($"{grid.DisplayName}: {grid.PositionComp.GetPosition().ToString("N")}");
            }
            Context.Respond(sb.ToString());
        }
    }
}
