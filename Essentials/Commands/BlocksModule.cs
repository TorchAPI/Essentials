using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.ModAPI;

namespace Essentials.Commands
{

    [Category("blocks")]
    public class BlocksModule : CommandModule
    {

        [Command("count type", "Counts all blocks of the given type.")]
        public void CountType(string type, string target = null)
        {
            var hasTarget = !string.IsNullOrEmpty(target);
            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x=>x.Projector == null).ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            var totalCount = MyEntities.GetEntities().OfType<MyCubeGrid>().Sum(grid => grid.GetBlocks().Select(block => block.BlockDefinition.Id.TypeId.ToString().Substring(16)).Count(blockType => string.Compare(type, blockType, StringComparison.OrdinalIgnoreCase) == 0));

            if (totalCount == 0)
            {
                Context.Respond($"No block of type {type} found on this server");
                return;
            }

            var sb = new StringBuilder();
            foreach (var grid in grids)
            {
                var count = grid.GetBlocks().Select(block => block.BlockDefinition.Id.TypeId.ToString().Substring(16)).Count(blockType => string.Compare(type, blockType, StringComparison.InvariantCultureIgnoreCase) == 0);
                if (count == 0)continue;
                sb.Append($"{grid.DisplayName} has {count} blocks with type {type}");
                sb.AppendLine();
            }

            if (Context?.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("Block Counts", $"Total of {totalCount} blocks of type {type} found on the server", sb.ToString()) , Context.Player.SteamUserId);
            else
            {
                sb.Append($"Total of {totalCount} blocks of type {type} found on the server");
                sb.AppendLine();

                Context?.Respond(sb.ToString());
            }
        }

        [Command("count subtype", "Count all blocks of the given subtype.")]
        public void CountSubtype(string subtype, string target = null)
        {
            var hasTarget = !string.IsNullOrEmpty(target);
            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x=>x.Projector == null).ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            var totalCount = MyEntities.GetEntities().OfType<MyCubeGrid>().Sum(grid => grid.GetBlocks().Select(block => block.BlockDefinition.Id.SubtypeName).Count(blockSubtype => string.Compare(subtype, blockSubtype, StringComparison.OrdinalIgnoreCase) == 0));

            if (totalCount == 0)
            {
                Context.Respond($"No block of type {subtype} found on this server");
                return;
            }

            var sb = new StringBuilder();

            foreach (var grid in grids)
            {
                var count = grid.GetBlocks().Select(block => block.BlockDefinition.Id.SubtypeId.ToString()).Count(blockType => string.Compare(subtype, blockType, StringComparison.InvariantCultureIgnoreCase) == 0);
                if (count == 0)continue;
                sb.Append($"{grid.DisplayName} has {count} blocks with subtype {subtype}");
                sb.AppendLine();
            }

            if (Context?.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("Block Counts", $"Total of {totalCount} blocks of subtype {subtype} found on the server", sb.ToString()) , Context.Player.SteamUserId);
            else
            {
                sb.Append($"Total of {totalCount} blocks of subtype {subtype} found on the server");
                sb.AppendLine();

                Context?.Respond(sb.ToString());
            }
        }


        [Command("on type", "Turn on all blocks of the given type.")]
        public void OnType(string type, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            foreach (var block in from grid in grids from block in grid.GetFatBlocks().OfType<MyFunctionalBlock>().ToList() let blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16) where string.Compare(type, blockType, StringComparison.OrdinalIgnoreCase) == 0 select block)
            {
                block.Enabled = true;
                count++;
            }


            Context.Respond($"Enabled {count} blocks of type {type}.");
        }

        [Command("on subtype", "Turn on all blocks of the given subtype.")]
        public void OnSubtype(string subtype, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            foreach (var block in from grid in grids from block in grid.GetFatBlocks().OfType<MyFunctionalBlock>().ToList() let blockSubtype = block.BlockDefinition.Id.SubtypeName where string.Compare(subtype, blockSubtype, StringComparison.OrdinalIgnoreCase) == 0 select block)
            {
                block.Enabled = true;
                count++;
            }


            Context.Respond($"Enabled {count} blocks of type {subtype}.");
        }

        [Command("off type", "Turn off all blocks of the given type.")]
        public void OffType(string type, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            foreach (var block in from grid in grids from block in grid.GetFatBlocks().OfType<MyFunctionalBlock>().ToList() let blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16) where string.Compare(type, blockType, StringComparison.OrdinalIgnoreCase) == 0 select block)
            {
                block.Enabled = false;
                count++;
            }


            Context.Respond($"Disabled {count} blocks of type {type}.");
        }

        [Command("remove subtype", "remove all blocks of the given subtype.")]
        public void RemoveSubtype(string subtype, string target = null)
        {
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            var toRemove = (from grid in grids from block in grid.GetFatBlocks().OfType<MySlimBlock>().ToList() let blockSubtype = block.BlockDefinition.Id.SubtypeName where string.Compare(subtype, blockSubtype, StringComparison.OrdinalIgnoreCase) == 0 select block).ToList();

            var count = toRemove.Count;

            foreach (var x in toRemove)
                x.CubeGrid.RazeBlock(x.Position);
            Context.Respond($"Removed {count} blocks of subtype {subtype}.");
        }

        [Command("remove type", "remove all blocks of the given type.")]
        public void RemoveType(string type, string target = null)
        {
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            var toRemove = (from grid in grids from block in grid.GetFatBlocks().OfType<MySlimBlock>().ToList() let blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16) where string.Compare(type, blockType, StringComparison.OrdinalIgnoreCase) == 0 select block).ToList();

            var count = toRemove.Count;
            foreach (var x in toRemove)
                x.CubeGrid.RazeBlock(x.Position);
            Context.Respond($"Removed {count} blocks of type {type}.");
        }

        [Command("off subtype", "Turn off all blocks of the given subtype.")]
        public void OffSubtype(string subtype, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            foreach (var block in from grid in grids from block in grid.GetFatBlocks().OfType<MyFunctionalBlock>().ToList() let blockSubtype = block.BlockDefinition.Id.SubtypeName where string.Compare(subtype, blockSubtype, StringComparison.OrdinalIgnoreCase) == 0 select block)
            {
                block.Enabled = true;
                count++;
            }


            Context.Respond($"Enabled {count} blocks of type {subtype}.");
        }

        [Command("on general", "Turn on all blocks of the specified category")]
        public void OnGeneral(string category, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            if (!Enum.TryParse(category, true, out BlockCategory result))
            {
                Context.Respond($"{category} is not a valid category. Use one of the following: " + string.Join(", ", Enum.GetValues(typeof(BlockCategory))));
                return;
            }

            foreach (var grid in grids)
            {
                foreach (var item in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!IsBlockTypeOf(item, result)) continue;
                    item.Enabled = true;
                    count++;
                }
            }


            Context.Respond($"Enabled {count} {category} blocks.");
        }


        [Command("off general", "Turn off all blocks of the specified category")]
        public void OffGeneral(string category, string target = null)
        {
            var count = 0;
            var hasTarget = !string.IsNullOrEmpty(target);

            var grids = hasTarget ? GetGrids(target) : MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();

            if (!grids.Any())
            {
                Context.Respond("No grid found from request");
                return;
            }

            if (!Enum.TryParse(category, true, out BlockCategory result))
            {
                Context.Respond($"{category} is not a valid category. Use one of the following: " + string.Join(", ", Enum.GetValues(typeof(BlockCategory))));
                return;
            }

            foreach (var grid in grids)
            {
                foreach (var item in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!IsBlockTypeOf(item, result)) continue;
                    item.Enabled = false;
                    count++;
                }
            }



            Context.Respond($"Disabled {count} {category} blocks.");
        }

        private List<MyCubeGrid> GetGrids(string target)
        {
            var grids = new List<MyCubeGrid>();
            var allGrids = MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x => x.Projector == null).ToList();

            if (Utilities.TryGetEntityByNameOrId(target, out var entity))
            {
                if (entity is MyCharacter character)
                {
                    return allGrids.Where(x => x.BigOwners.Contains(character.GetPlayerIdentityId())).ToList();
                }

                if (entity is MyCubeGrid grid)
                {
                    if (grid.Projector == null) grids.Add(grid);
                    return grids;
                }

            }

            var player = Utilities.GetPlayerByNameOrId(target);

            if (player != null)
            {
                return allGrids.Where(x => x.BigOwners.Contains(player.IdentityId)).ToList();
            }

            if (!long.TryParse(target, out var factionId))
            {
                var attemptFaction = MySession.Static.Factions.TryGetFactionByTag(target);
                return attemptFaction == null ? grids : allGrids.Where(x => x.BigOwners.Any(o => attemptFaction.Members.ContainsKey(0))).ToList();
            }

            var faction = MySession.Static.Factions.TryGetFactionById(factionId);
            return faction == null ? grids : allGrids.Where(x => x.BigOwners.Any(o => faction.Members.ContainsKey(0))).ToList();



        }


        private static bool IsBlockTypeOf(IMyEntity block, BlockCategory category)
        {
            switch (category)
            {
                case BlockCategory.Power:
                    return block is IMyPowerProducer;
                
                case BlockCategory.Production:
                    if (block is MySurvivalKit) return false;
                    return block is MyProductionBlock || block is MyGasGenerator;

                case BlockCategory.Weapons:
                    return block is IMyLargeTurretBase || block is IMyUserControllableGun;

                default:
                    throw new InvalidBranchException();
            }
        }

        public enum BlockCategory
        {
            Power,
            Production,
            Weapons

        }
    }

}