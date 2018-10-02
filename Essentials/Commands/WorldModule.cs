using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using SpaceEngineers.Game.GUI;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers;
using Torch.Utils;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Network;

namespace Essentials.Commands
{
    public class WorldModule : CommandModule
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        [Command("identity clean", "Remove identities that have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void CleanIdentities(int days)
        {
            var count = 0;
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    //MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    RemoveFromFaction_Internal(identity);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                }
            }
            
            RemoveEmptyFactions();
            Context.Respond($"Removed {count} old identities");
        }

        [Command("identity purge", "Remove identities AND the grids they own if they have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void PurgeIdentities(int days)
        {
            var count = 0;
            var count2 = 0;
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    //MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    RemoveFromFaction_Internal(identity);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                    foreach (var grid in grids)
                    {
                        if (grid.BigOwners.Contains(identity.IdentityId))
                        {
                            grid.Close();
                            count2++;
                        }
                    }
                }
            }
            
            RemoveEmptyFactions();
            Context.Respond($"Removed {count} old identities and {count2} grids owned by them.");
        }

        [Command("faction clean", "Removes factions with fewer than the given number of players.")]
        public void CleanFactions(int memberCount = 1)
        {
            int count = CleanFaction_Internal(memberCount);

            Context.Respond($"Removed {count} factions with fewer than {memberCount} members.");
        }

        private static void RemoveEmptyFactions()
        {
            CleanFaction_Internal(1);
        }

        private static int CleanFaction_Internal(int memberCount = 1)
        {
            int result = 0;

            foreach (var faction in MySession.Static.Factions.ToList())
            {
                int validmembers = 0;

                //O(2n)
                foreach (var member in faction.Value.Members)
                {
                    if (!MySession.Static.Players.HasIdentity(member.Key) && !MySession.Static.Players.IdentityIsNpc(member.Key))
                        continue;

                    validmembers++;

                    if (validmembers >= memberCount)
                        break;
                }

                if (validmembers >= memberCount)
                    continue;

                RemoveFaction(faction.Value);
                result++;
            }

            return result;
        }

        private static bool RemoveFromFaction_Internal(MyIdentity identity)
        {
            var fac = MySession.Static.Factions.GetPlayerFaction(identity.IdentityId);
            if (fac == null)
                return false;
            fac.KickMember(identity.IdentityId);
            return true;
        }

        //Equinox told me you can use delegates for ReflectedMethods.
        //He lied.
        //private delegate void FactionStateDelegate(MyFactionCollection instance, MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId);

        [ReflectedMethod(Name = "ApplyFactionStateChange", Type = typeof(MyFactionCollection))]
        private static Action<MyFactionCollection, MyFactionStateChange, long, long, long, long> _applyFactionState;

        private static MethodInfo _factionChangeSuccessInfo = typeof(MyFactionCollection).GetMethod("FactionStateChangeSuccess", BindingFlags.NonPublic|BindingFlags.Static);

        //TODO: This should probably be moved into Torch base, but I honestly cannot be bothered
        /// <summary>
        /// Removes a faction from the server and all clients because Keen fucked up their own system.
        /// </summary>
        /// <param name="faction"></param>
        private static void RemoveFaction(MyFaction faction)
        {
            //bypass the check that says the server doesn't have permission to delete factions
            _applyFactionState(MySession.Static.Factions, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0, 0);
            var n = EssentialsPlugin.Instance.Torch.CurrentSession.Managers.GetManager<NetworkManager>();
            //send remove message to clients
            n.RaiseStaticEvent(_factionChangeSuccessInfo, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0L, 0L);
        }

        private static readonly FieldInfo GpssField = typeof(MySession).GetField("Gpss", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo GpsDicField = GpssField.FieldType.GetField("m_playerGpss", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SeedParamField = typeof(MyProceduralWorldGenerator).GetField("m_existingObjectsSeeds", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo CamerasField = typeof(MySession).GetField("Cameras", BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly FieldInfo AllCamerasField = CamerasField.FieldType.GetField("m_entityCameraSettings", BindingFlags.NonPublic|BindingFlags.Instance);

        [Command("sandbox clean", "Cleans up junk data from the sandbox file")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void CleanSandbox()
        {
            var validIdentities = new HashSet<long>();
            var idCache = new HashSet<long>();
            var allSteamId = new HashSet<ulong>();

            //find all identities owning a block
            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null)
                    continue;
                validIdentities.UnionWith(grid.SmallOwners);
            }
            //might not be necessary, but just in case
            validIdentities.Remove(0);

            //clean identities that don't own any blocks, or don't have a steam ID for whatever reason
            foreach (var identity in MySession.Static.Players.GetAllIdentities().ToList())
            {
                if (validIdentities.Contains(identity.IdentityId))
                {
                    var steam = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if (steam != 0)
                    {
                        allSteamId.Add(steam);
                        continue;
                    }
                }

                RemoveFromFaction_Internal(identity);
                MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                validIdentities.Remove(identity.IdentityId);
            }

            //clean up empty factions
            CleanFaction_Internal();

            //Keen, for the love of god why is everything about GPS internal.
            var playerGpss = GpsDicField.GetValue(GpssField.GetValue(MySession.Static)) as Dictionary<long, Dictionary<int, MyGps>>;

            foreach (var id in playerGpss.Keys)
            {
                if (!MySession.Static.Players.HasIdentity(id))
                    idCache.Add(id);
            }

            foreach (var id in idCache)
                playerGpss.Remove(id);

            var g = MySession.Static.GetComponent<MyProceduralWorldGenerator>();
            var f = SeedParamField.GetValue(g) as HashSet<MyObjectSeedParams>;
            f.Clear();

            idCache.Clear();
            foreach (var history in MySession.Static.ChatHistory)
            {
                if (!validIdentities.Contains(history.Key))
                    idCache.Add(history.Key);
            }

            foreach (var id in idCache)
            {
                MySession.Static.ChatHistory.Remove(id);
            }
            idCache.Clear();
            
            //delete chat history for deleted factions
            for(int i = MySession.Static.FactionChatHistory.Count -1; i >=0; i--)
            {
                var history = MySession.Static.FactionChatHistory[i];
                if (MySession.Static.Factions.TryGetFactionById(history.FactionId1) == null || MySession.Static.Factions.TryGetFactionById(history.FactionId2) == null)
                {
                    MySession.Static.FactionChatHistory.RemoveAtFast(i);
                }
            }

            var cf = AllCamerasField.GetValue(CamerasField.GetValue(MySession.Static)) as Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>>;
            cf.Clear();
        }
    }
}
