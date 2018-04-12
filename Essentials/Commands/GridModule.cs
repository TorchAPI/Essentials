using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Valve.VR;
using VRage;
using VRage.Game.Entity.EntityComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;

namespace Essentials
{
    [Category("grids")]
    public class GridModule : CommandModule
    {
        [Command("setowner", "Sets grid ownership to the given player or ID.", "Usage: setowner <grid> <newowner>")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void SetOwner(string gridName, string playerName)
        {
            var firstArg = Context.Args.FirstOrDefault();
            Utilities.TryGetEntityByNameOrId(gridName, out IMyEntity entity);

            if (!(entity is IMyCubeGrid grid))
            {
                Context.Respond($"Grid {gridName} not found.");
                return;
            }

            var secondArg = Context.Args.ElementAtOrDefault(1);
            long identityId;
            if (!long.TryParse(playerName, out identityId))
            {
                var player = Context.Torch.CurrentSession?.Managers?.GetManager<IMultiplayerManagerBase>().GetPlayerByName(playerName);
                if (player == null)
                {
                    Context.Respond($"Player {playerName} not found.");
                    return;
                }
                identityId = player.IdentityId;
            }

            grid.ChangeGridOwnership(identityId, MyOwnershipShareModeEnum.Faction);
            Context.Respond($"Transferred ownership of {grid.DisplayName} to {identityId}");

            /*
            grid.GetBlocks(new List<IMySlimBlock>(), block =>
            {
                var cubeBlock = block.FatBlock as MyCubeBlock;
                var ownerComp = cubeBlock?.Components.Get<MyEntityOwnershipComponent>();
                if (ownerComp == null)
                    return false;

                cubeBlock?.ChangeOwner(0, MyOwnershipShareModeEnum.All);
                cubeBlock?.ChangeOwner(identityId, ownerComp.ShareMode);
                return false;
            });*/
        }

        [Command("static large", "Makes all large grids static.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void StaticLarge()
        {
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(g => g.GridSizeEnum == MyCubeSize.Large))
                grid.ConvertToStatic();
        }

        [Command("list", "List all grids owned by you.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var sb = new StringBuilder("Grids:\n");
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (grid.BigOwners.Contains(Context.Player?.IdentityId ?? 0))
                    sb.AppendLine($"{grid.DisplayName}: {grid.PositionComp.GetPosition().ToString("N")}");
            }
            Context.Respond(sb.ToString());
        }

        private readonly string ExportPath = "ExportedGrids\\{0}.xml";

        [Command("export", "Export the given grid to the given file name.")]
        public void Export(string gridName, string exportName)
        {
            Directory.CreateDirectory("ExportedGrids");
            if (!Utilities.TryGetEntityByNameOrId(gridName, out var ent) || !(ent is IMyCubeGrid))
            {
                Context.Respond("Grid not found.");
                return;
            }

            var path = string.Format(ExportPath, exportName);
            if (File.Exists(path))
            {
                Context.Respond("Export file already exists.");
                return;
            }

            MyObjectBuilderSerializer.SerializeXML(path, false, ent.GetObjectBuilder());
            Context.Respond($"Grid saved to {path}");
        }
        
        [Command("import", "Import a grid from file and spawn it by the given entity/player.")]
        public void Import(string gridName, string targetName = null)
        {
            Directory.CreateDirectory("ExportedGrids");
            if (targetName == null)
            {
                if (Context.Player == null)
                {
                    Context.Respond("Target entity must be specified.");
                    return;   
                }

                targetName = Context.Player.Controller.ControlledEntity.Entity.DisplayName;
            }

            if (!Utilities.TryGetEntityByNameOrId(targetName, out var ent))
            {
                Context.Respond("Target entity not found.");
                return;
            }
            
            var path = string.Format(ExportPath, gridName);
            if (!File.Exists(path))
            {
                Context.Respond("File does not exist.");
                return;
            }

            if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_CubeGrid grid))
            {
                Context.Respond($"Importing grid from {path}");
                MyEntities.RemapObjectBuilder(grid);
                var pos = MyEntities.FindFreePlace(ent.GetPosition(), grid.CalculateBoundingSphere().Radius);
                if (pos == null)
                {
                    Context.Respond("No free place.");
                    return;
                }

                var x = grid.PositionAndOrientation ?? new MyPositionAndOrientation();
                x.Position = pos.Value;
                grid.PositionAndOrientation = x;
                MyEntities.CreateFromObjectBuilderParallel(grid, true);
            }
        }
    }
}
