using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.Entity;
using VRage.Network;
using VRage.Voxels;
using Parallel = ParallelTasks.Parallel;

namespace Essentials.Commands
{
    [Category("voxels")]
    public class VoxelModule : CommandModule
    {
        private Logger _log = LogManager.GetCurrentClassLogger();

        [Command("reset all", "Resets all voxel maps.")]
        public void ResetAll()
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelBase>();
            
            var resetIds = new List<long>();
            //Parallel.ForEach(voxelMaps, map =>
            foreach(var map in voxelMaps)
                                        {
                                            try
                                            {
                                                if (map.StorageName == null || map.Storage.DataProvider == null)
                                                    continue;

                                                map.Storage.Reset(MyStorageDataTypeFlags.All);
                                                lock (resetIds)
                                                    resetIds.Add(map.EntityId);
                                            }
                                            catch (Exception e)
                                            {
                                                _log.Error($"{e.Message}\n{e.StackTrace}");
                                            }
                                        }//, blocking:true);
            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {resetIds.Count} voxel maps.");
        }

        [Command("cleanup asteroids", "Resets all asteroids that don't have a grid or player nearby")]
        public void CleanupAsteroids()
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelMap>();

            var resetIds = new List<long>();

            //Parallel.ForEach(voxelMaps, map =>
            foreach(var map in voxelMaps)
                                        {
                                            try
                                            {
                                                if (map.StorageName == null || map.Storage.DataProvider == null)
                                                    continue;

                                                var s = map.PositionComp.WorldVolume;
                                                var nearEntities = new List<MyEntity>();

                                                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s, nearEntities);

                                                if(nearEntities.Any(e => e is MyCubeGrid || e is MyCharacter))
                                                    continue;

                                                map.Storage.Reset(MyStorageDataTypeFlags.All);
                                                lock(resetIds)
                                                    resetIds.Add(map.EntityId);
                                            }
                                            catch (Exception e)
                                            {
                                                _log.Error($"{e.Message}\n{e.StackTrace}");
                                            }
                                        }//);

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {resetIds.Count} voxel maps.");
        }

        [Command("reset planets", "Resets all planets.")]
        public void ResetPlanets()
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyPlanet>();

            var resetIds = new List<long>();

            //Parallel.ForEach(voxelMaps, map =>
            foreach(var map in voxelMaps)
                                        {
                                            try
                                            {
                                                if (map.StorageName == null || map.Storage.DataProvider == null)
                                                    continue;

                                                map.Storage.Reset(MyStorageDataTypeFlags.All);
                                                lock (resetIds)
                                                    resetIds.Add(map.EntityId);
                                            }
                                            catch (Exception e)
                                            {
                                                _log.Error($"{e.Message}\n{e.StackTrace}");
                                            }
                                        }//);

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {resetIds.Count} voxel maps.");
        }
    }
}
