using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;

namespace Essentials.Commands
{
    [System.ComponentModel.Category("utility")]
    public class UtilityModule : CommandModule
    {
        [Command("listgrids", "Lists all grids you own at least 50% of. Will give you positions if the server admin enables the option.")]
        public void ListGrids()
        {
            var id = Context.Player.IdentityId;
            StringBuilder sb = new StringBuilder();
            
            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null)
                    continue;

                if (grid.BigOwners.Contains(id))
                {
                    sb.AppendLine($"{grid.DisplayName} - {grid.GridSizeEnum} - {grid.BlocksCount} blocks - Position {(EssentialsPlugin.Instance.Config.UtilityShowPosition ? grid.PositionComp.GetPosition().ToString() : "Unknown")}");
                }
            }

            ModCommunication.SendMessageTo(new DialogMessage("Grids List", $"Ships/Stations owned by {Context.Player.DisplayName}", sb.ToString()), Context.Player.SteamUserId);
        }
    }
}
