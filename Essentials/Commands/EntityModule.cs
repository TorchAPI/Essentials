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
using Torch.Utils;
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
#pragma warning disable 649
        [ReflectedGetter(Name = "m_clientStates")]
        private static Func<MyReplicationServer, IDictionary> _clientStates;

        private const string CLIENT_DATA_TYPE_NAME = "VRage.Network.MyReplicationServer+ClientData, VRage";
        [ReflectedGetter(TypeName = CLIENT_DATA_TYPE_NAME, Name = "Replicables")]
        private static Func<object, MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>> _replicables;

        [ReflectedMethod(Name = "RemoveForClient", OverrideTypeNames = new[] { null, null, CLIENT_DATA_TYPE_NAME, null })]
        private static Action<MyReplicationServer, IMyReplicable, Endpoint, object, bool> _removeForClient;

        [ReflectedMethod(Name = "ForceReplicable")]
        private static Action<MyReplicationServer, IMyReplicable, Endpoint> _forceReplicable;
#pragma warning restore 649

        [Command("refresh", "Resyncs all entities for the player running the command.")]
        [Permission(MyPromoteLevel.None)]
        public void Refresh()
        {
            if (Context.Player == null)
                return;

            var playerEndpoint = new Endpoint(Context.Player.SteamUserId, 0);
            var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
            var clientDataDict = _clientStates.Invoke(replicationServer);
            object clientData;
            try
            {
                clientData = clientDataDict[playerEndpoint];
            }
            catch
            {
                return;
            }

            var clientReplicables = _replicables.Invoke(clientData);

            var replicableList = new List<IMyReplicable>(clientReplicables.Count);
            foreach (var pair in clientReplicables)
                replicableList.Add(pair.Key);

            foreach (var replicable in replicableList)
            {
                _removeForClient.Invoke(replicationServer, replicable, playerEndpoint, clientData, true);
                _forceReplicable.Invoke(replicationServer, replicable, playerEndpoint);
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
