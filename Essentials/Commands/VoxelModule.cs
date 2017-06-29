using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Replication;
using Torch.Commands;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Voxels;
using Parallel = ParallelTasks.Parallel;

namespace Essentials.Commands
{
    [Category("voxels")]
    public class VoxelModule : CommandModule
    {
        [Command("reset all", "Resets all voxel maps.")]
        public void ResetAll()
        {
            var voxelMaps = MyEntities.GetEntities().Select(x => x as IMyVoxelBase);

            Console.WriteLine(voxelMaps.Count());
            var count = 0;
            //Parallel.ForEach(voxelMaps, map =>
            try
            {
                foreach (var map in voxelMaps)
                {
                    if (map?.StorageName == null || ((MyStorageBase)map.Storage).DataProvider == null)
                        continue;

                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    ((MyReplicationServer)MyMultiplayer.ReplicationLayer).ForceClientRefresh((MyVoxelBase)map);
                    count++;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message}\n{e.StackTrace}");
            }

            Context.Respond($"Reset {count} voxel maps.");
        }
    }
}
