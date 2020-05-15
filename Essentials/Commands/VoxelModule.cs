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
using Sandbox.Game.World.Generator;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game.Entity;
using VRage.Network;
using VRage.Voxels;
using VRageMath;
using Parallel = ParallelTasks.Parallel;

namespace Essentials.Commands
{
    [Category("voxels")]
    public class VoxelModule : CommandModule
    {
        private Logger _log = LogManager.GetCurrentClassLogger();

        [ReflectedGetter(Name = "m_asteroidsModule")]
        private static Func<MyProceduralWorldGenerator, MyProceduralAsteroidCellGenerator> _asteroidGenerator;
        [ReflectedSetter(Name = "m_isClosingEntities")]
        private static Action<MyProceduralAsteroidCellGenerator, bool> _deletingSet;

        private static MyProceduralAsteroidCellGenerator _generatorInstance;
        private static MyProceduralAsteroidCellGenerator GeneratorInstance => _generatorInstance ?? (_generatorInstance = _asteroidGenerator(MyProceduralWorldGenerator.Static));

        [Command("reset all", "Resets all voxel maps.")]
        public void ResetAll(bool deleteStorage = false)
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelBase>();
            
            var resetIds = new List<long>();
            int count = 0;
            
            foreach (var map in voxelMaps)
            {
                try
                {
                    if (map.StorageName == null || map.Storage.DataProvider == null)
                        continue;

                    count++;

                    long id = map.EntityId;

                    if (deleteStorage && map is MyVoxelMap)
                    {
                        using (PinDelete())
                            map.Close();
                    }
                    else
                    {
                        map.Storage.Reset(MyStorageDataTypeFlags.All);
                        resetIds.Add(id);
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"{e.Message}\n{e.StackTrace}");
                }
            }

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {count} voxel maps.");
        }

        [Command("cleanup asteroids", "Resets all asteroids that don't have a grid or player nearby.")]
        public void CleanupAsteroids(bool deleteStorage = false)
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelMap>();

            var resetIds = new List<long>();
            int count = 0;

            foreach (var map in voxelMaps)
            {
                try
                {
                    if (map.StorageName == null || map.Storage.DataProvider == null)
                        continue;

                    count++;

                    var s = map.PositionComp.WorldVolume;
                    var nearEntities = new List<MyEntity>();

                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s, nearEntities);

                    if (nearEntities.Any(e => e is MyCubeGrid || e is MyCharacter))
                        continue;

                    long id = map.EntityId;

                    if (deleteStorage)
                    {
                        using (PinDelete())
                            map.Close();
                    }
                    else
                    {
                        map.Storage.Reset(MyStorageDataTypeFlags.All);
                        resetIds.Add(id);
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"{e.Message}\n{e.StackTrace}");
                }
            }

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {count} voxel maps.");
        }

        [Command("cleanup distant", "Resets all asteroids that don't have a grid or player inside the specified radius.")]
        public void CleanupAsteroidsDistant(double distance = 1000, bool deleteStorage = false)
        {

            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelMap>();

            var resetIds = new List<long>();
            int count = 0;
            
            foreach (var map in voxelMaps)
            {
                try
                {
                    if (map.StorageName == null || map.Storage.DataProvider == null)
                        continue;

                    var s = new BoundingSphereD(map.PositionComp.GetPosition(), distance);
                    var nearEntities = new List<MyEntity>();

                    MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref s, nearEntities);

                    if (nearEntities.Any(e => e is MyCubeGrid || e is MyCharacter))
                        continue;

                    count++;

                    long id = map.EntityId;

                    if (deleteStorage)
                    {
                        using (PinDelete())
                            map.Close();
                    }
                    else
                    {
                        map.Storage.Reset(MyStorageDataTypeFlags.All);
                        resetIds.Add(id);
                    }
                }
                catch (Exception e)
                {
                    _log.Error($"{e.Message}\n{e.StackTrace}");
                }
            }

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {count} voxel maps.");
        }

        [Command("reset planets", "Resets all planets.")]
        public void ResetPlanets()
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyPlanet>();

            var resetIds = new List<long>();

            foreach (var map in voxelMaps)
            {
                try
                {
                    if (map.StorageName == null || map.Storage.DataProvider == null)
                        continue;

                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    resetIds.Add(map.EntityId);
                }
                catch (Exception e)
                {
                    _log.Error($"{e.Message}\n{e.StackTrace}");
                }
            }

            ModCommunication.SendMessageToClients(new VoxelResetMessage(resetIds.ToArray()));

            Context.Respond($"Reset {resetIds.Count} voxel maps.");
        }

        [Command("reset planet", "Resets the planet with a given name.")]
        public void ResetPlanet(string planetName)
        {
            var maps = new List<MyPlanet>(2);
            foreach (MyPlanet map in MyEntities.GetEntities().OfType<MyPlanet>())
            {
                if( map.StorageName.Contains(planetName, StringComparison.CurrentCultureIgnoreCase))
                    maps.Add(map);
            }

            switch (maps.Count)
            {
                case 0:
                    Context.Respond($"Couldn't find planet with name {planetName}");
                    return;
                case 1:
                    var map = maps[0];
                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    ModCommunication.SendMessageToClients(new VoxelResetMessage(new long[]{map.EntityId}));
                    Context.Respond($"Reset planet {map.Name}");
                    return;
                default:
                    Context.Respond($"Found {maps.Count} planets matching '{planetName}'. Please select from list:");
                    Context.Respond(string.Join("\r\n", maps.Select(m => m.StorageName)));
                    return;
            }
        }

        private static LockToken PinDelete()
        {
            return new LockToken();
        }

        private class LockToken : IDisposable
        {
            public LockToken()
            {
                _deletingSet(GeneratorInstance, true);
            }

            public void Dispose()
            {
                _deletingSet(GeneratorInstance, false);
            }
        }
    }
}
