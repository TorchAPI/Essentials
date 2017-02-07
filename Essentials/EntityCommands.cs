using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Essentials
{
    [Category("entity")]
    public class EntityCommands : CommandModule
    {
        [Command("stop", "Stops an entity from moving")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Stop()
        {
            if (!Utilities.TryGetEntityByNameOrId(Context.Args.FirstOrDefault(), out IMyEntity entity))
            {
                Context.Respond("Entity not found.");
                return;
            }

            entity?.Physics.ClearSpeed();
            Context.Respond($"Entity '{entity.DisplayName}' stopped");
        }

        [Command("delete", "Delete an entity.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Delete()
        {
            if (!Utilities.TryGetEntityByNameOrId(Context.Args.FirstOrDefault(), out IMyEntity entity))
            {
                Context.Respond("Entity not found.");
                return;
            }

            entity.Close();
            Context.Respond($"Entity '{entity.DisplayName}' deleted");
        }
    }
}
