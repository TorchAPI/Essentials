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

namespace Essentials
{
    [Category("grid")]
    public class GridCommands : CommandModule
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

            grid.GetBlocks(new List<IMySlimBlock>(), block =>
            {
                var cubeBlock = block.FatBlock as MyCubeBlock;
                var ownerComp = cubeBlock?.Components.Get<MyEntityOwnershipComponent>();
                if (ownerComp == null)
                    return false;

                cubeBlock?.ChangeOwner(identityId, ownerComp.ShareMode);
                return false;
            });
        }
    }
}
