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
            

            var totalBlockList = new List<IMySlimBlock>();

            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetBlocks())
                {
                    var allow = block.BlockDefinition.Id.TypeId.ToString().Substring(16)
                        .Equals(type, StringComparison.OrdinalIgnoreCase);
                    
                    if (!allow) continue;
                   totalBlockList.Add(block);
                }
            }

            if (!totalBlockList.Any())
            {
                Context.Respond($"No block of type {type} found on this server");
                return;
            }

            var totalBlockCount = totalBlockList.Count;

            var sb = new StringBuilder();
            foreach (var grid in grids)
            {
                var count = 0;
                foreach (var block in totalBlockList)
                {
                    var makeCount = block.CubeGrid.EntityId == grid.EntityId;
                    if (!makeCount)continue;
                    count++;
                }
                
                if (count == 0)continue;
                sb.Append($"{grid.DisplayName} has {count} blocks with type {type}");
                sb.AppendLine();
            }

            if (Context.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("Block Counts", $"Total of {totalBlockCount} blocks of type {type} found on the server", sb.ToString()) , Context.Player.SteamUserId);
            else
            {
                sb.Append($"Total of {totalBlockCount} blocks of type {type} found on the server");
                sb.AppendLine();

                Context.Respond(sb.ToString());
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

           
            var totalBlockList = new List<IMySlimBlock>();

            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetBlocks())
                {
                    var allow = block.BlockDefinition.Id.SubtypeName
                        .Equals(subtype, StringComparison.OrdinalIgnoreCase);
                    
                    if (!allow) continue;
                    totalBlockList.Add(block);
                }
            }


            if (!totalBlockList.Any())
            {
                Context.Respond($"No block of type {subtype} found on this server");
                return;
            }

            var totalBlockCount = totalBlockList.Count;

            var sb = new StringBuilder();

            foreach (var grid in grids)
            {
                var count = 0;
                foreach (var block in totalBlockList)
                {
                    var makeCount = block.CubeGrid.EntityId == grid.EntityId;
                    if (!makeCount)continue;
                    count++;
                }
                
                if (count == 0)continue;
                sb.Append($"{grid.DisplayName} has {count} blocks with subtype {subtype}");
                sb.AppendLine();
            }

            if (Context.Player?.SteamUserId > 0)
                ModCommunication.SendMessageTo(new DialogMessage("Block Counts", $"Total of {totalBlockCount} blocks of subtype {subtype} found on the server", sb.ToString()) , Context.Player.SteamUserId);
            else
            {
                sb.Append($"Total of {totalBlockCount} blocks of subtype {subtype} found on the server");
                sb.AppendLine();

                Context.Respond(sb.ToString());
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

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!block.BlockDefinition.Id.TypeId.ToString().Substring(16)
                        .Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
                    block.Enabled = true;
                    count++;
                }
            }


            Context.Respond($"Turned on {count} blocks of type {type}.");
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

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!block.BlockDefinition.Id.SubtypeName
                        .Equals(subtype, StringComparison.OrdinalIgnoreCase)) continue;
                    block.Enabled = true;
                    count++;
                }
            }



            Context.Respond($"Turned on {count} blocks of type {subtype}.");
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

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!block.BlockDefinition.Id.TypeId.ToString().Substring(16)
                        .Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
                    block.Enabled = false;
                    count++;
                }
            }

            Context.Respond($"Turned off {count} blocks of type {type}.");
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

            var count = 0;

            var toRemove = new List<MySlimBlock>();

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetBlocks())
                {
                    if (!block.BlockDefinition.Id.SubtypeName
                        .Equals(subtype, StringComparison.OrdinalIgnoreCase)) continue;
                    toRemove.Add(block);
                    count++;
                }
            }
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

            var count = 0;

            var toRemove = new List<MySlimBlock>();

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetBlocks())
                {
                    if (!block.BlockDefinition.Id.TypeId.ToString().Substring(16)
                        .Equals(type, StringComparison.OrdinalIgnoreCase)) continue;
                    toRemove.Add(block);
                    count++;
                }
            }
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

            foreach (var grid in grids)
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    if (!block.BlockDefinition.Id.SubtypeName
                        .Equals(subtype, StringComparison.OrdinalIgnoreCase)) continue;
                    block.Enabled = false;
                    count++;
                }
            }

            Context.Respond($"Turned off {count} blocks of type {subtype}.");
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
                var usableString = string.Join(", ", Enum.GetValues(typeof(BlockCategory)));
                Context.Respond($"{category} is not a valid category. Use one of the following: {usableString}" );
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


            Context.Respond($"Turned on {count} {category} blocks.");
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
                var usableString = string.Join(", ", Enum.GetValues(typeof(BlockCategory)));
                Context.Respond($"{category} is not a valid category. Use one of the following: {usableString}" );
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



            Context.Respond($"Turned off {count} {category} blocks.");
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
                return attemptFaction == null ? grids : allGrids.Where(x => x.BigOwners.Any(owner => attemptFaction.Members.ContainsKey(owner))).ToList();
            }

            var faction = MySession.Static.Factions.TryGetFactionById(factionId);
            return faction == null ? grids : allGrids.Where(x => x.BigOwners.Any(owner => faction.Members.ContainsKey(owner))).ToList();



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

                case BlockCategory.ShipTools:
                    return block is IMyShipDrill || block is IMyShipWelder || block is IMyShipGrinder;

                default:
                    throw new ArgumentException();
            }
        }

        public enum BlockCategory
        {
            Power,
            Production,
            Weapons,
            ShipTools

        }
    }

}