using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Torch.Commands;
using VRage.Network;
using VRage.Voxels;

namespace Essentials.Commands
{
    [Category("voxels")]
    public class VoxelModule : CommandModule
    {
        [Command("reset all", "Resets all voxel maps.")]
        public void ResetAll()
        {
            var voxelMaps = MyEntities.GetEntities().OfType<MyVoxelBase>();

            var count = 0;
            //Parallel.ForEach(voxelMaps, map =>
            try
            {
                foreach (var map in voxelMaps)
                {
                    if (map.StorageName == null || map.Storage.DataProvider == null)
                        continue;

                    map.Storage.Reset(MyStorageDataTypeFlags.All);
                    ((MyReplicationServer)MyMultiplayer.ReplicationLayer).ForceClientRefresh(map);
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
