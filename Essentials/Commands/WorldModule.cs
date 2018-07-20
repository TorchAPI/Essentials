using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using SpaceEngineers.Game.GUI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Essentials.Commands
{
    public class WorldModule : CommandModule
    {
        [Command("identity clean", "Remove identities that have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void CleanIdentities(int days)
        {
            var count = 0;
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                }
            }
            
            CleanFactions();
            Context.Respond($"Removed {count} old identities");
        }

        [Command("identity purge", "Remove identities AND the grids they own if they have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void PurgeIdentities(int days)
        {
            var count = 0;
            var count2 = 0;
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                    foreach (var grid in grids)
                    {
                        if (grid.BigOwners.Contains(identity.IdentityId))
                        {
                            grid.Close();
                            count2++;
                        }
                    }
                }
            }
            
            CleanFactions();
            Context.Respond($"Removed {count} old identities and {count2} grids owned by them.");
        }

        private void CleanFactions()
        {
            foreach (var faction in MySession.Static.Factions.ToList())
            {
                if (faction.Value.Members.Count == 0)
                {
                    MyFactionCollection.RemoveFaction(faction.Key);
                }
            }
        }
    }
}
