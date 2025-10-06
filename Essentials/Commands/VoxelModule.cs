using Essentials.Utils;
using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;
using static Sandbox.Game.Entities.MyVoxelBase;
using Parallel = ParallelTasks.Parallel;

namespace Essentials.Commands
{
    [Category("voxels")]
    public class VoxelModule : CommandModule
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        

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

                    var s = map.PositionComp.WorldVolume;
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
                if (map.StorageName.Contains(planetName, StringComparison.CurrentCultureIgnoreCase))
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
                    ModCommunication.SendMessageToClients(new VoxelResetMessage(new long[] { map.EntityId }));
                    Context.Respond($"Reset planet {map.Name}");
                    return;
                default:
                    Context.Respond($"Found {maps.Count} planets matching '{planetName}'. Please select from list:");
                    Context.Respond(string.Join("\r\n", maps.Select(m => m.StorageName)));
                    return;
            }
        }


        [Command("reset area", "Resets voxel damange in specified radius from player")]
        [Permission(MyPromoteLevel.Admin)]
        public void ResetVoxelArea(float Radius)
        {
            if (Context.Player == null)
                Context.Respond("Invalid command input! Must be ingame!");


            if (Radius <= 0)
            {
                Context.Respond("Inavlid radius!");
                return;
            }


            if (ResetVoxelInArea(Context.Player.GetPosition(), Radius))
            {
                Context.Respond("Voxel reset complete!");
            }
            else
            {
                Context.Respond("Couldnt reset voxels! Check log for more information!");
            }


        }

        [Command("reset gps", "Resets voxel damange in specified radius from given point")]
        [Permission(MyPromoteLevel.Admin)]
        public void ResetVoxelArea(float X, float Y, float Z, float Radius)
        {
            Vector3D ResetTarget = new Vector3D(X, Y, Z);
            if (Radius <= 0)
            {
                _log.Info("Invalid Radius Input!");
                return;
            }


            if (ResetVoxelInArea(ResetTarget, Radius))
            {
                Context.Respond("Voxel reset complete!");
            }
            else
            {
                Context.Respond("Couldnt reset voxels! Check log for more information!");
            }
        }

        public static bool ResetVoxelInArea(Vector3D Center, float Radius, bool UpdatePlayers = true)
        {

            try
            {
                BoundingSphereD Sphere = new BoundingSphereD(Center, Radius);
                List<MyVoxelBase> Maps = MyEntities.GetEntitiesInSphere(ref Sphere).OfType<MyVoxelBase>().ToList();
                if (Maps.Count == 0)
                    return true;

                var m = typeof(MyVoxelBase).GetMethod("PerformVoxelOperationSphere_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (var voxelMap in Maps)
                {
                    if (voxelMap.MarkedForClose)
                        continue;



                    MyShapeSphere shape = new MyShapeSphere();
                    shape.Center = Center;
                    shape.Radius = Radius;

                    MyVoxelGenerator.RevertShape(voxelMap, shape);

                    if (UpdatePlayers)
                        Events.RaiseEvent<MyVoxelBase, Vector3D, float, byte, OperationType>(voxelMap.RootVoxel, m, shape.Center, shape.Radius, 0, MyVoxelBase.OperationType.Revert);

                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Voxel reset failed!");
                return false;
            }
        }





        private static LockToken PinDelete()
        {
            /* 
             * If SEWorldGenerator Plugin is used or Procedural Seed is 0 there 
             * will be no GeneratorInstance. In this case this Method will NRE.
             * 
             * However we still would like to delete stuff. So just return null. 
             * Because when there is no GeneratorInstance we dont have to tell it
             * entities are being deleted.
             */
            if (GeneratorInstance == null)
                return null;

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
