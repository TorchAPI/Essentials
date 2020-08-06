using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
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
using Torch.Mod;
using Torch.Mod.Messages;
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
            FixBlockOwnership();
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
            FixBlockOwnership();
            Context.Respond($"Removed {count} old identities and {count2} grids owned by them.");
        }

        [Command("identity clear", "Clear identity of specific player")]
        [Permission(MyPromoteLevel.Admin)]
        public void PurgeIdentity (string playername) {
            var count2 = 0;
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            foreach (var identity in idents) {
                if (identity.DisplayName == playername) {
                    //MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    RemoveFromFaction_Internal(identity);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    foreach (var grid in grids) {
                        if (grid.BigOwners.Contains(identity.IdentityId)) {
                            grid.Close();
                            count2++;
                        }
                    }
                }
            }

            RemoveEmptyFactions();
            FixBlockOwnership();
            Context.Respond($"Removed identity and {count2} grids owned by them.");
        }

        [Command("rep wipe", "Resets the reputation on the server")]
        public void WipeReputation(bool removePlayerToFaction = true, bool removeFactionToFaction = true)
        {
            var count = WipeRep(removePlayerToFaction, removeFactionToFaction);
            Context.Respond($"Wiped {count} reputations");
        }


        [Command("faction clean", "Removes factions with fewer than the given number of players.")]
        public void CleanFactions(int memberCount = 1)
        {
            int count = CleanFaction_Internal(memberCount);

            Context.Respond($"Removed {count} factions with fewer than {memberCount} members.");
        }


        [Command("faction remove", "removes faction by tag name")]
        [Permission(MyPromoteLevel.Admin)]
        public void RemoveFaction(string tag)
        {
            if (tag == null)
            {
                Context.Respond("You need to add a faction tag to remove");
                return;
            }

            var fac = MySession.Static.Factions.TryGetFactionByTag(tag);
            if (fac == null || !MySession.Static.Factions.FactionTagExists(tag))
            {
                Context.Respond($"{tag} is not a faction on this server");
                return;
            }
            foreach (var player in fac.Members)
            {
                if (!MySession.Static.Players.HasIdentity(player.Key)) continue;
                fac.KickMember(player.Key);
            }
            RemoveFaction(fac);
            Context.Respond(MySession.Static.Factions.FactionTagExists(tag)
                ? $"{tag} removal failed"
                : $"{tag} removal successful");
        }

        [Command("faction info", "lists members of given faction")]
        [Permission(MyPromoteLevel.Admin)]
        public void FactionInfo()
        {

            StringBuilder sb = new StringBuilder();

            foreach (var factionID in MySession.Static.Factions)
            {
                double memberCount;
                var faction = factionID.Value;
                memberCount = faction.Members.Count();
                sb.AppendLine();
                if (faction.IsEveryoneNpc())
                {
                    sb.AppendLine($"{faction.Tag} - {memberCount} NPC found in this faction");
                    continue;
                }
                sb.AppendLine($"{faction.Tag} - {memberCount} players in this faction");
                foreach (var player in faction?.Members)
                {
                    if (!MySession.Static.Players.HasIdentity(player.Key) && !MySession.Static.Players.IdentityIsNpc(player.Key)||
                        string.IsNullOrEmpty(MySession.Static?.Players?.TryGetIdentity(player.Value.PlayerId).DisplayName)) continue; //This is needed to filter out players with no id.
                    sb.AppendLine($"{MySession.Static?.Players?.TryGetIdentity(player.Value.PlayerId).DisplayName}");
                }
            }
            if (Context.Player == null)
                Context.Respond(sb.ToString());
            else if (Context?.Player?.SteamUserId > 0)
            {
                ModCommunication.SendMessageTo(new DialogMessage("Faction Info", null, sb.ToString()), Context.Player.SteamUserId);
            }


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
                if ((faction.Value.IsEveryoneNpc() || !faction.Value.AcceptHumans) && faction.Value.Members.Count != 0) //needed to add this to catch the 0 member factions
                    continue;

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
 
            /* 
             * VisualScriptLogicProvider takes care of removal of faction if last 
             * identity is kicked, and promotes the next player in line to Founder 
             * if the founder is being kicked. 
             * 
             * Factions must have a founder otherwise calls like MyFaction.Members.Keys will NRE. 
             */
            MyVisualScriptLogicProvider.KickPlayerFromFaction(identity.IdentityId);

            return true;
        }
        
        private static MethodInfo _factionChangeSuccessInfo = typeof(MyFactionCollection).GetMethod("FactionStateChangeSuccess", BindingFlags.NonPublic | BindingFlags.Static);
        
        //TODO: This should probably be moved into Torch base, but I honestly cannot be bothered
        /// <summary>
        /// Removes a faction from the server and all clients because Keen fucked up their own system.
        /// </summary>
        /// <param name="faction"></param>
        private static void RemoveFaction(MyFaction faction)
        {
            //bypass the check that says the server doesn't have permission to delete factions
            //_applyFactionState(MySession.Static.Factions, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0L, 0L);
            //MyMultiplayer.RaiseStaticEvent(s =>
            //        (Action<MyFactionStateChange, long, long, long, long>) Delegate.CreateDelegate(typeof(Action<MyFactionStateChange, long, long, long, long>), _factionStateChangeReq),
            //    MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, faction.FounderId, faction.FounderId);
            NetworkManager.RaiseStaticEvent(_factionChangeSuccessInfo, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0L, 0L);
            if(!MyAPIGateway.Session.Factions.FactionTagExists(faction.Tag)) return;
            MyAPIGateway.Session.Factions.RemoveFaction(faction.FactionId); //Added to remove factions that got through the crack
        }

        private static int FixBlockOwnership()
        {
            int count = 0;
            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null)
                    continue;
                var owner = grid.BigOwners.FirstOrDefault();
                var share = owner == 0 ? MyOwnershipShareModeEnum.All : MyOwnershipShareModeEnum.Faction;
                foreach (var block in grid.GetFatBlocks())
                {
                    if (block.OwnerId == 0 || MySession.Static.Players.HasIdentity(block.OwnerId))
                        continue;

                    block.ChangeOwner(owner, share);
                    count++;
                }
            }
            return count;
        }

        private static readonly FieldInfo GpsDicField = typeof(MyGpsCollection).GetField("m_playerGpss", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SeedParamField = typeof(MyProceduralWorldGenerator).GetField("m_existingObjectsSeeds", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo CamerasField = typeof(MySession).GetField("Cameras", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AllCamerasField = CamerasField.FieldType.GetField("m_entityCameraSettings", BindingFlags.NonPublic | BindingFlags.Instance);

        [Command("sandbox clean", "Cleans up junk data from the sandbox file")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void CleanSandbox()
        {
            int count = 0;
            var validIdentities = new HashSet<long>();
            var idCache = new HashSet<long>();

            //find all identities owning a block
            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null)
                    continue;
                validIdentities.UnionWith(grid.SmallOwners);
            }

            foreach (var online in MySession.Static.Players.GetOnlinePlayers())
            {
                validIdentities.Add(online.Identity.IdentityId);
            }

            //might not be necessary, but just in case
            validIdentities.Remove(0);

            //clean identities that don't own any blocks, or don't have a steam ID for whatever reason
            foreach (var identity in MySession.Static.Players.GetAllIdentities().ToList())
            {
                if (MySession.Static.Players.IdentityIsNpc(identity.IdentityId))
                {
                    validIdentities.Add(identity.IdentityId);
                    continue;
                }

                if (validIdentities.Contains(identity.IdentityId))
                {
                    continue;
                }

                RemoveFromFaction_Internal(identity);
                MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                validIdentities.Remove(identity.IdentityId);
                count++;
            }

            //reset ownership of blocks belonging to deleted identities
            count += FixBlockOwnership();

            //clean up empty factions
            count += CleanFaction_Internal();

            //cleanup reputations

            count += CleanupReputations();


            //Keen, for the love of god why is everything about GPS internal.
            var playerGpss = GpsDicField.GetValue(MySession.Static.Gpss) as Dictionary<long, Dictionary<int, MyGps>>;
            foreach (var id in playerGpss.Keys)
            {
                if (!validIdentities.Contains(id))
                    idCache.Add(id);
            }

            foreach (var id in idCache)
                playerGpss.Remove(id);

            count += idCache.Count;
            idCache.Clear();

            var g = MySession.Static.GetComponent<MyProceduralWorldGenerator>();
            var f = SeedParamField.GetValue(g) as HashSet<MyObjectSeedParams>;
            count += f.Count;
            f.Clear();
            
            //TODO
            /*
            foreach (var history in MySession.Static.ChatHistory)
            {
                if (!validIdentities.Contains(history.Key))
                    idCache.Add(history.Key);
            }

            foreach (var id in idCache)
            {
                MySession.Static.ChatHistory.Remove(id);
            }
            count += idCache.Count;
            idCache.Clear();

            //delete chat history for deleted factions
            for (int i = MySession.Static.FactionChatHistory.Count - 1; i >= 0; i--)
            {
                var history = MySession.Static.FactionChatHistory[i];
                if (MySession.Static.Factions.TryGetFactionById(history.FactionId1) == null || MySession.Static.Factions.TryGetFactionById(history.FactionId2) == null)
                {
                    count++;
                    MySession.Static.FactionChatHistory.RemoveAtFast(i);
                }
            }
            */
            
            var cf = AllCamerasField.GetValue(CamerasField.GetValue(MySession.Static)) as Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>>;
            count += cf.Count;
            cf.Clear();

            Context.Respond($"Removed {count} unnecessary elements.");
        }

        [ReflectedGetter(Name = "m_relationsBetweenFactions", Type = typeof(MyFactionCollection))]
        private static Func<MyFactionCollection, Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>> _relationsGet;
        [ReflectedGetter(Name = "m_relationsBetweenPlayersAndFactions", Type = typeof(MyFactionCollection))]
        private static Func<MyFactionCollection, Dictionary<MyFactionCollection.MyRelatablePair, Tuple<MyRelationsBetweenFactions, int>>> _playerRelationsGet;

        private static int WipeRep(bool removePlayerToFaction, bool removeFactionToFaction)
        {
            var result = 0;
            var collection0 = _relationsGet(MySession.Static.Factions);
            var collection1 = _playerRelationsGet(MySession.Static.Factions);

            if (removeFactionToFaction)
            {
                foreach (var pair in collection0.Keys.ToList())
                {
                    collection0.Remove(pair);
                    result++;
                }
            }

            if (removePlayerToFaction)
            {
                foreach (var pair in collection1.Keys.ToList())
                {
                    collection1.Remove(pair);
                    result++;
                }
            }

            return result;

        }
        private static int CleanupReputations()
        {
            var collection = _relationsGet(MySession.Static.Factions);
            var collection2 = _playerRelationsGet(MySession.Static.Factions);


            var validIdentities = new HashSet<long>();

            //find all identities owning a block
            foreach (var entity in MyEntities.GetEntities())
            {
                var grid = entity as MyCubeGrid;
                if (grid == null)
                    continue;
                validIdentities.UnionWith(grid.SmallOwners);
            }


            //find online identities
            foreach (var online in MySession.Static.Players.GetOnlinePlayers())
            {
                validIdentities.Add(online.Identity.IdentityId);
            }

            foreach (var identity in MySession.Static.Players.GetAllIdentities().ToList())
            {
                if (MySession.Static.Players.IdentityIsNpc(identity.IdentityId))
                {
                    validIdentities.Add(identity.IdentityId);
                }
            }

            //Add Factions with at least one member to valid identities
            foreach (var faction in MySession.Static.Factions.Factions.Where(x=>x.Value.Members.Count > 0))
            {
                validIdentities.Add(faction.Key);
            }


            //might not be necessary, but just in case
            validIdentities.Remove(0);
            var result = 0;

            var collection0List = collection.Keys.ToList();
            var collection1List = collection2.Keys.ToList();

            foreach (var pair in collection0List)
            {
                if (validIdentities.Contains(pair.RelateeId1) && validIdentities.Contains(pair.RelateeId2))
                    continue;
                collection.Remove(pair);
                result++;
            }

            foreach (var pair in collection1List)
            {
                if (validIdentities.Contains(pair.RelateeId1) && validIdentities.Contains(pair.RelateeId2))
                    continue;
                collection2.Remove(pair);
                result++;
            }
            

            //_relationsSet.Invoke(MySession.Static.Factions,collection);
            //_playerRelationsSet.Invoke(MySession.Static.Factions,collection2);
            return result;
        }

    }
}
