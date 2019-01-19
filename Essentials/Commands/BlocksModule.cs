using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Torch.Commands;

namespace Essentials.Commands
{
    [Category("blocks")]
    public class BlocksModule : CommandModule
    {
        [Command("on type", "Turn on all blocks of the given type.")]
        public void OnType(string type)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    var blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16);
                    if (block != null && string.Compare(type, blockType, StringComparison.InvariantCultureIgnoreCase) ==
                        0)
                    {
                        block.Enabled = true;
                        count++;
                    }
                }
            }

            Context.Respond($"Enabled {count} blocks of type {type}.");
        }

        [Command("on subtype", "Turn on all blocks of the given subtype.")]
        public void OnSubtype(string subtype)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    var blockType = block.BlockDefinition.Id.SubtypeName;
                    if (block != null &&
                        string.Compare(subtype, blockType, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        block.Enabled = true;
                        count++;
                    }
                }
            }

            Context.Respond($"Enabled {count} blocks of subtype {subtype}.");
        }

        [Command("off type", "Turn off all blocks of the given type.")]
        public void OffType(string type)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    var blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16);
                    if (block != null && string.Compare(type, blockType, StringComparison.InvariantCultureIgnoreCase) ==
                        0)
                    {
                        block.Enabled = false;
                        count++;
                    }
                }
            }


            Context.Respond($"Disabled {count} blocks of type {type}.");
        }

        [Command("remove subtype", "Turn off all blocks of the given subtype.")]
        public void RemoveSubtype(string subtype)
        {
            var toRemove = new List<MySlimBlock>();
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetBlocks())
                {
                    var blockType = block.BlockDefinition.Id.SubtypeName;
                    if (string.Compare(subtype, blockType, StringComparison.InvariantCultureIgnoreCase) == 0)
                        toRemove.Add(block);
                }
            }

            foreach (var x in toRemove)
                x.CubeGrid.RazeBlock(x.Position);
            Context.Respond($"Removed {toRemove.Count} blocks of subtype {subtype}.");
        }

        [Command("remove type", "Turn off all blocks of the given type.")]
        public void RemoveType(string type)
        {
            var toRemove = new List<MySlimBlock>();
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetBlocks())
                {
                    var blockType = block.BlockDefinition.Id.TypeId.ToString().Substring(16);
                    if (string.Compare(type, blockType, StringComparison.InvariantCultureIgnoreCase) == 0)
                        toRemove.Add(block);
                }
            }

            foreach (var x in toRemove)
                x.CubeGrid.RazeBlock(x.Position);
            Context.Respond($"Removed {toRemove.Count} blocks of type {type}.");
        }

        [Command("off subtype", "Turn off all blocks of the given subtype.")]
        public void OffSubtype(string subtype)
        {
            var count = 0;
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                foreach (var block in grid.GetFatBlocks().OfType<MyFunctionalBlock>())
                {
                    var blockType = block.BlockDefinition.Id.SubtypeName;
                    if (block != null &&
                        string.Compare(subtype, blockType, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        block.Enabled = false;
                        count++;
                    }
                }
            }


            Context.Respond($"Disabled {count} blocks of subtype {subtype}.");
        }

        [Command("on general", "Turn on all blocks of the specified general")]
        public void OnGeneral(string general)
        {
            var count = 0;
            string status = "?";
            foreach (var entity in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                IMyCubeGrid grid = entity as MyCubeGrid;
                if (general.Contains("pow"))
                {
                    status = "Power";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Reactor) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_BatteryBlock) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_SolarPanel)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => !f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = true;
                    }
                    count++;
                }
                if (general.Contains("prod"))
                {
                    status = "Production";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Refinery) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Assembler) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_OxygenGenerator)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => !f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = true;
                    }
                    count++;

                }
                if (general.Contains("wea"))
                {
                    status = "Weapon";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_InteriorTurret) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_TurretBase) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_LargeMissileTurret)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => !f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = true;
                    }
                    count++;

                }
            }

            Context.Respond($"Enabled {count} {status} blocks.");
        }
        [Command("off general", "Turn off all blocks of the specified general")]
        public void OffGeneral(string general)
        {
            var count = 0;
            string status = "?";
            foreach (var entity in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                IMyCubeGrid grid = entity as MyCubeGrid;
                if (general.Contains("pow"))
                {
                    status = "Power";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Reactor) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_BatteryBlock) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_SolarPanel)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = false;
                    }
                    count++;
                }
                if (general.Contains("prod"))
                {
                    status = "Production";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Refinery) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_Assembler) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_OxygenGenerator)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = false;
                    }
                    count++;

                }
                if (general.Contains("wea"))
                {
                    status = "Weapon";
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, f => f.FatBlock != null && f.FatBlock is IMyFunctionalBlock
                    && (f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_InteriorTurret) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_TurretBase) ||
                    f.FatBlock.BlockDefinition.TypeId == typeof(MyObjectBuilder_LargeMissileTurret)));
                    var list = blocks.Select(f => (IMyFunctionalBlock)f.FatBlock).Where(f => f.Enabled).ToArray();
                    foreach (var item in list)
                    {
                        item.Enabled = false;
                    }
                    count++;

                }
            }

            Context.Respond($"Disabled {count} {status} blocks.");
        }
    }
}