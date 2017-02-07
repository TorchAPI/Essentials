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
        [Permission(MyPromoteLevel.Moderator)]
        public void SetOwner()
        {
            var firstArg = Context.Args.FirstOrDefault();
            Utilities.TryGetEntityByNameOrId(firstArg, out IMyEntity entity);

            if (!(entity is IMyCubeGrid grid))
            {
                Context.Respond($"Grid {firstArg} not found.");
                return;
            }

            var secondArg = Context.Args.ElementAtOrDefault(1);
            long identityId;
            if (!long.TryParse(secondArg, out identityId))
            {
                var player = Context.Torch.Multiplayer.GetPlayerByName(secondArg);
                if (player == null)
                {
                    Context.Respond($"Player {secondArg} not found.");
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
