using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;

namespace Essentials.Commands
{
    public class WorldModule : CommandModule
    {
        [Command("identity clean", "Remove identities that have not logged on in X days.")]
        public void CleanIdentities(int days)
        {
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
            }
        }

        [Command("identity purge", "Remove identities AND the grids they own if they have not logged on in X days.")]
        public void PurgeIdentities(int days)
        {
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    foreach (var grid in grids)
                    {
                        if (grid.BigOwners.Contains(identity.IdentityId))
                            grid.Close();
                    }
                }
            }

        }
    }
}
