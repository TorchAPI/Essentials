using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.Replication;
using VRageMath;

namespace Essentials
{
    [Category("entities")]
    public class EntityModule : CommandModule
    {
        [Command("refresh", "Resyncs all entities for the player running the command.")]
        [Permission(MyPromoteLevel.None)]
        public void Refresh()
        {
            if (Context.Player == null)
                return;

            var playerEndpoint = new Endpoint(Context.Player.SteamUserId, 0);
            var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
            var clientDataDict = typeof(MyReplicationServer).GetField("m_clientStates", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(replicationServer) as IDictionary;
            object clientData;
            try
            {
                clientData = clientDataDict[playerEndpoint];
            }
            catch
            {
                return;
            }

            var clientReplicables = clientData.GetType().GetField("Replicables").GetValue(clientData) as MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>;
            var removeForClientMethod = typeof(MyReplicationServer).GetMethod("RemoveForClient", BindingFlags.Instance | BindingFlags.NonPublic);
            var forceReplicableMethod = typeof(MyReplicationServer).GetMethod("ForceReplicable", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] {typeof(IMyReplicable), typeof(Endpoint)}, null);

            var replicableList = new List<IMyReplicable>(clientReplicables.Count);
            foreach (var pair in clientReplicables)
                replicableList.Add(pair.Key);

            foreach (var replicable in replicableList)
            {
                removeForClientMethod.Invoke(replicationServer, new object[] {replicable, playerEndpoint, clientData, true});
                forceReplicableMethod.Invoke(replicationServer, new object[] {replicable, playerEndpoint});
            }

            Context.Respond($"Forced replication of {replicableList.Count} entities.");
        }

        [Command("stop", "Stops an entity from moving")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Stop(string entityName)
        {
            if (!Utilities.TryGetEntityByNameOrId(entityName, out IMyEntity entity))
            {
                Context.Respond($"Entity '{entityName}' not found.");
                return;
            }

            entity.Physics?.ClearSpeed();
            Context.Respond($"Entity '{entity.DisplayName}' stopped");
        }

        [Command("delete", "Delete an entity.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Delete(string entityName)
        {
            var name = entityName;
            if (string.IsNullOrEmpty(name))
                return;

            if (!Utilities.TryGetEntityByNameOrId(name, out IMyEntity entity))
            {
                Context.Respond($"Entity '{name}' not found.");
                return;
            }

            if (entity is IMyCharacter)
            {
                Context.Respond("You cannot delete characters.");
                return;
            }

            entity.Close();
            Context.Respond($"Entity '{entity.DisplayName}' deleted");
        }

        [Command("find", "Find entities with the given text in their name.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            var sb = new StringBuilder("Found entities:\n");
            foreach (var entity in MyEntities.GetEntities())
            {
                if (entity is IMyVoxelBase voxel && (voxel.StorageName?.Contains(name, StringComparison.CurrentCultureIgnoreCase) ?? false))
                    sb.AppendLine($"{voxel.StorageName} ({entity.EntityId})");
                else if (entity?.DisplayName?.Contains(name, StringComparison.CurrentCultureIgnoreCase) ?? false)
                    //This can be null??? :keen:
                    sb.AppendLine($"{entity.DisplayName} ({entity.EntityId})");
            }

            Context.Respond(sb.ToString());
        }
    }
}
