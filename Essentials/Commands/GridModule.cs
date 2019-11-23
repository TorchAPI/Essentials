using System.IO;
using System.Linq;
using System.Text;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using ALE_Core.Utils;
using System.Collections.Concurrent;
using VRage.Groups;

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

        [Command("ejectall", "Ejects all Players from given grid.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Eject(string gridName = null) 
        {
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> gridGroups;

            if (gridName == null) 
            {
                if(Context.Player == null) 
                {
                    Context.Respond("The console always has to pass a gridname!");
                    return;
                }

                IMyCharacter character = Context.Player.Character;

                if (character == null) 
                {
                    Context.Respond("You need to spawn into a character when not using gridname!");
                    return;
                }

                gridGroups = GridFinder.FindLookAtGridGroupMechanical(character);

                if (gridGroups.Count == 0) 
                {
                    Context.Respond("No grid in your line of sight found! Remember to NOT use spectator!");
                    return;
                }
            } 
            else 
            {
                gridGroups = GridFinder.FindGridGroupMechanical(gridName);

                if (gridGroups.Count == 0) 
                {
                    Context.Respond($"Grid with name '{gridName}' was not found!");
                    return;
                }

                if (gridGroups.Count > 1) 
                {
                    Context.Respond($"There were multiple grids with name '{gridName}' to prevent any mistakes this command will not be executed!");
                    return;
                }
            }

            var group = gridGroups.First();

            foreach(var node in group.Nodes) 
            {

                MyCubeGrid grid = node.NodeData;

                foreach(var fatBlock in grid.GetFatBlocks()) 
                {

                    if (!(fatBlock is MyShipController shipController))
                        continue;

                    if (shipController.Pilot != null)
                        shipController.Use();
                }
            }
        }

        [Command("static large", "Makes all large grids static.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void StaticLarge()
        {
            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(g => g.GridSizeEnum == MyCubeSize.Large).Where(x => x.Projector == null))
                grid.OnConvertedToStationRequest(); //Keen why do you do this to me?
        }

        [Command("stopall", "Stops all moving grids.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void StopAll()
        {
                foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>().Where(x => x.Projector == null))
                {
                    grid.Physics.ClearSpeed();
                }
        }

        [Command("list", "Lists all grids you own at least 50% of. Will give you positions if the server admin enables the option.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var id = Context.Player?.IdentityId ?? 0;
            StringBuilder sb = new StringBuilder();

            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null || grid.Projector != null)
                    continue;

                if (grid.BigOwners.Contains(id))
                {

                    sb.AppendLine($"{grid.DisplayName} - {grid.GridSizeEnum} - {grid.BlocksCount} blocks - Position {(EssentialsPlugin.Instance.Config.UtilityShowPosition ? grid.PositionComp.GetPosition().ToString() : "Unknown")}");
                    if (EssentialsPlugin.Instance.Config.MarkerShowPosition)
                    {
                        var gridGPS = MyAPIGateway.Session?.GPS.Create(grid.DisplayName, ($"{grid.DisplayName} - {grid.GridSizeEnum} - {grid.BlocksCount} blocks"), grid.PositionComp.GetPosition(), true);

                        MyAPIGateway.Session?.GPS.AddGps(Context.Player.IdentityId, gridGPS);
                    }
                }
            }

            ModCommunication.SendMessageTo(new DialogMessage("Grids List", $"Ships/Stations owned by {Context.Player.DisplayName}", sb.ToString()), Context.Player.SteamUserId);
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
