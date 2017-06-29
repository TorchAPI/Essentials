using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Essentials
{
    [Category("delete")]
    public class DeleteModule : CommandModule
    {
        [Command("grids notype", "Delete all grids that don't have a block of the given type")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void DeleteByType(string type, bool scanOnly = true)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (ShouldRemove(grid))
                {
                    if (!scanOnly)
                        grid.Close();
                    count++;
                }
            }

            Context.Respond($"{(scanOnly ? "Found" : "Deleted")} {count} grids missing the block subtype '{type}'.");

            bool ShouldRemove(MyCubeGrid grid)
            {
                foreach (var block in grid.GetBlocks())
                {
                    var id = block.BlockDefinition.Id.TypeId.ToString();
                    if (string.Compare(id, type, StringComparison.InvariantCultureIgnoreCase) == 0)
                        return false;
                }

                return true;
            }
        }

        [Command("grids nosubtype", "Delete all grids that don't have a block of the given subtype.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void DeleteBySubtype(string subtype, bool scanOnly = true)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (ShouldRemove(grid))
                {
                    if (!scanOnly)
                        grid.Close();
                    count++;
                }
            }

            Context.Respond($"{(scanOnly ? "Found" : "Deleted")} {count} grids missing the block subtype '{subtype}'.");

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
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void DeleteByOwner(string name, bool scanOnly = true)
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

            Context.Respond($"{(scanOnly ? "Found" : "Deleted")} {count} grids owned by '{name}.'");
        }

        [Command("grids blockslessthan", "Delete grids with fewer than X blocks.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void DeleteBlocksLessThan(int minBlocks, bool scanOnly = true)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BlocksCount < minBlocks)
                {
                    if (!scanOnly)
                        grid.Close();
                    count++;
                }
            }

            Context.Respond($"{(scanOnly ? "Found" : "Deleted")} {count} grids with less than {minBlocks} blocks.");
        }

        [Command("grids blocksgreaterthan", "Delete grids with greater than X blocks.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void DeleteBlocksGreaterThan(int maxBlocks, bool scanOnly = true)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BlocksCount > maxBlocks)
                {
                    if (!scanOnly)
                        grid.Close();
                    count++;
                }
            }

            Context.Respond($"{(scanOnly ? "Found" : "Deleted")} {count} grids with greater than {maxBlocks} blocks.");
        }

        [Command("floating", "Delete all floating objects.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
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
