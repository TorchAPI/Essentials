using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Torch.Commands;
using VRage.Game.ModAPI;

namespace Essentials
{
    [Category("delete")]
    public class DeleteModule : CommandModule
    {
        [Command("grids nosubtype", "Delete all grids that don't have a block of the given subtype.")]
        public void DeleteBySubtype(string subtype)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (ShouldRemove(grid))
                {
                    grid.Close();
                    count++;
                }
            }

            Context.Respond($"Deleted {count} grids missing the block subtype '{subtype}'.");

            bool ShouldRemove(MyCubeGrid grid)
            {
                foreach (var block in grid.GetBlocks())
                {
                    var id = block.BlockDefinition.Id.SubtypeId.String;
                    if (string.Compare(id, subtype, StringComparison.InvariantCultureIgnoreCase) == 0)
                        return false;
                }

                return true;
            }
        }

        [Command("grids ownedby", "Delete grids that the given player owns the majority of.")]
        public void DeleteByOwner(string name)
        {
            var player = Utilities.GetPlayerByNameOrId(name);
            if (player == null)
            {
                Context.Respond($"Player '{name}' not found.");
                return;
            }

            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<IMyCubeGrid>())
            {
                if (grid.BigOwners.Contains(player.IdentityId))
                {
                    grid.Close();
                    count++;
                }
            }
            
            Context.Respond($"Deleted {count} grids owned by '{name}.'");
        }

        [Command("grids blockslessthan", "Delete grids with fewer than X blocks.")]
        public void DeleteBlocksLessThan(int minBlocks)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BlocksCount < minBlocks)
                {
                    grid.Close();
                    count++;
                }
            }

            Context.Respond($"Deleted {count} grids with less than {minBlocks} blocks.");
        }

        public void DeleteBlocksGreaterThan(int maxBlocks)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BlocksCount > maxBlocks)
                {
                    grid.Close();
                    count++;
                }
            }

            Context.Respond($"Deleted {count} grids with greater than {maxBlocks} blocks.");
        }

        [Command("floating", "Delete all floating objects.")]
        public void DeleteFloating()
        {
            var count = 0;
            foreach (var floating in MyEntities.GetEntities().OfType<IMyFloatingObject>())
            {
                floating.Close();
                count++;
            }

            Context.Respond($"Deleted {count} floating objects.");
        }
    }
}
