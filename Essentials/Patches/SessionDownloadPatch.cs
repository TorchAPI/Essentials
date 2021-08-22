using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Essentials.Utils;
using NLog;
using ParallelTasks;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Utils;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRage.GameServices;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Replication;
using VRage.Serialization;
using VRageMath;
using Parallel = ParallelTasks.Parallel;

namespace Essentials.Patches
{
    /// <summary>
    ///     This is a replacement for the vanilla logic that generates a world save to send to new clients.
    ///     The main focus of this replacement is to drastically reduce the amount of data sent to clients
    ///     (which removes some exploits), and to remove as many allocations as realistically possible,
    ///     in order to speed up the client join process, avoiding lag spikes on new connections.
    /// 
    ///     This code is **NOT** free to use, under the Apache license. You know who this message is for.
    /// </summary>
    public class SessionDownloadPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyMultiplayerServerBase).GetMethod("CleanUpData", BindingFlags.NonPublic | BindingFlags.Static)).Suffixes.Add(typeof(SessionDownloadPatch).GetMethod(nameof(CleanupClientWorld), BindingFlags.NonPublic | BindingFlags.Static));
        }


        private static void CleanupClientWorld(MyObjectBuilder_World worldData, ulong playerId, long senderIdentity)
        {
            /*
             * The entire client join code can be cleaned up massively to reduce server load. However, that needs to be in another plugin or keen
             * 
             * 
             * This is being ran directly after the original cleanup. Original removes:
             * 1. Station store items
             * 2. Player relations (keeps only theirs)
             * 3. Player Faction relations (keeps only theres)
             * 
             */



            //I know ALEs stuff removes this, but lets just add it in essentials too
            foreach (var Identity in worldData.Checkpoint.Identities)
            {
                //Clear all put sender identity last death position
                if (Identity.IdentityId != senderIdentity)
                    Identity.LastDeathPosition = null;

            }


            //I dont trust keen to do it
            worldData.Checkpoint.Gps.Dictionary.TryGetValue(senderIdentity, out MyObjectBuilder_Gps value);
            worldData.Checkpoint.Gps.Dictionary.Clear();
            if (value != null)
            {
                worldData.Checkpoint.Gps.Dictionary.Add(senderIdentity, value);
            }



            foreach (var SessionComponent in worldData.Checkpoint.SessionComponents)
            {

                if (SessionComponent is MyObjectBuilder_SessionComponentResearch)
                {
                    MyObjectBuilder_SessionComponentResearch Component = (MyObjectBuilder_SessionComponentResearch)SessionComponent;



                    // Remove everyone elses research shit (quick and dirty)
                    for(int i = Component.Researches.Count-1; i >= 0; i--)
                    {
                        if (Component.Researches[i].IdentityId == senderIdentity)
                            continue;

                        Component.Researches.RemoveAt(i);
                    }







                }




            }


            foreach (var Player in worldData.Checkpoint.AllPlayersData.Dictionary)
            {

                if (Player.Value.IdentityId == senderIdentity)
                    continue;


                //Clear toolbar junk for other players. Seriously keen what the FUCK
                Player.Value.Toolbar = null;

            }


        }





        /*
        private static MyObjectBuilder_Checkpoint GetClientCheckpoint(ulong steamId)
        {
            Log.Info($"Saving checkpoint...");
            var cpid = new MyObjectBuilder_Checkpoint.PlayerId();
            cpid.ClientId = steamId;
            var ppid = new MyPlayer.PlayerId(steamId);

            if (_checkpoint == null)
            {
                _checkpoint = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Checkpoint>();
                _cameraSettings = new List<CameraControllerSettings>();
                var settings = MyObjectBuilderSerializer.Clone(MySession.Static.Settings) as MyObjectBuilder_SessionSettings;
                bGps = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Gps>();
                bGps.Entries = new List<MyObjectBuilder_Gps.Entry>();

                settings.ScenarioEditMode |= MySession.Static.PersistentEditMode;

                _checkpoint.SessionName = MySession.Static.Name;
                _checkpoint.Description = MySession.Static.Description;
                _checkpoint.PromotedUsers = new SerializableDictionary<ulong, MyPromoteLevel>(MySession.Static.PromotedUsers);
                _checkpoint.CreativeTools = new HashSet<ulong>();
                _checkpoint.Settings = settings;
                //We're replacing the call to MySession.GetWorld, so Torch can't inject the torch mod. Do it here instead
                _checkpoint.Mods = MySession.Static.Mods.ToList();
                _checkpoint.Mods.AddRange(SessionManager.OverrideMods);
                _checkpoint.Scenario = MySession.Static.Scenario.Id;
                _checkpoint.WorldBoundaries = MySession.Static.WorldBoundaries;
                _checkpoint.PreviousEnvironmentHostility = MySession.Static.PreviousEnvironmentHostility;
                _checkpoint.RequiresDX = MySession.Static.RequiresDX;
                _checkpoint.CustomSkybox = MySession.Static.CustomSkybox;
                _checkpoint.GameDefinition = MySession.Static.GameDefinition.Id;
                _checkpoint.Gps = new SerializableDictionary<long, MyObjectBuilder_Gps>();
                _checkpoint.RespawnCooldowns = new List<MyObjectBuilder_Checkpoint.RespawnCooldownItem>();
                _checkpoint.ControlledEntities = new SerializableDictionary<long, MyObjectBuilder_Checkpoint.PlayerId>();
                _checkpoint.ChatHistory = new List<MyObjectBuilder_ChatHistory>();
                _checkpoint.FactionChatHistory = new List<MyObjectBuilder_FactionChatHistory>();
                _checkpoint.AppVersion = MyFinalBuildConstants.APP_VERSION;
                _checkpoint.SessionComponents = new List<MyObjectBuilder_SessionComponent>();
                _checkpoint.AllPlayersData = new SerializableDictionary<MyObjectBuilder_Checkpoint.PlayerId, MyObjectBuilder_Player>();
                _checkpoint.AllPlayersColors = new SerializableDictionary<MyObjectBuilder_Checkpoint.PlayerId, List<Vector3>>();
                _checkpoint.Identities = new List<MyObjectBuilder_Identity>();
                _checkpoint.Clients = new List<MyObjectBuilder_Client>();
                _checkpoint.NonPlayerIdentities = new List<long>();
                _checkpoint.CharacterToolbar = null;
            }

            _checkpoint.CreativeTools.Clear();
            //Pool.DeallocateCollection(_checkpoint.Gps.Dictionary.Values);
            _checkpoint.Gps.Dictionary.Clear();
            //Pool.DeallocateAndClear(bGps.Entries);
            bGps.Entries.Clear();
            //Pool.DeallocateAndClear(_checkpoint.RespawnCooldowns);
            _checkpoint.RespawnCooldowns.Clear();
            Pool.DeallocateAndClear(_checkpoint.ChatHistory);
            _checkpoint.ControlledEntities.Dictionary.Clear();
            Pool.DeallocateAndClear(_checkpoint.FactionChatHistory);
            _checkpoint.SessionComponents.Clear();
            Pool.DeallocateCollection(_checkpoint.AllPlayersData.Dictionary.Values);
            _checkpoint.AllPlayersData.Dictionary.Clear();
            _checkpoint.AllPlayersColors.Dictionary.Clear();
            Pool.DeallocateAndClear(_checkpoint.Identities);
            Pool.DeallocateAndClear(_checkpoint.Clients);
            _checkpoint.NonPlayerIdentities.Clear();
            Pool.DeallocateAndClear(_cameraSettings);

            if (MySession.Static.CreativeToolsEnabled(steamId))
                _checkpoint.CreativeTools.Add(steamId);
            //checkpoint.Briefing = Briefing;
            //checkpoint.BriefingVideo = BriefingVideo;
            _checkpoint.LastSaveTime = DateTime.Now;
            _checkpoint.WorkshopId = MySession.Static.WorkshopId;
            _checkpoint.ElapsedGameTime = MySession.Static.ElapsedGameTime.Ticks;
            _checkpoint.InGameTime = MySession.Static.InGameTime;
            //checkpoint.CharacterToolbar = MyToolbarComponent.CharacterToolbar.GetObjectBuilder();
            //TODO
            _checkpoint.CustomLoadingScreenImage = MySession.Static.CustomLoadingScreenImage;
            _checkpoint.CustomLoadingScreenText = EssentialsPlugin.Instance.Config.LoadingText ?? MySession.Static.CustomLoadingScreenText;

            _checkpoint.SessionComponentDisabled = MySession.Static.SessionComponentDisabled;
            _checkpoint.SessionComponentEnabled = MySession.Static.SessionComponentEnabled;

            //  checkpoint.PlayerToolbars = Toolbars.GetSerDictionary();

            //Sync.Players.SavePlayers(_checkpoint);
            ConcurrentDictionary<MyPlayer.PlayerId, MyPlayer> m_players = Players_Getter(MySession.Static.Players);
            ConcurrentDictionary<MyPlayer.PlayerId, long> m_playerIdentityIds = PlayerIdentities_Getter(MySession.Static.Players);

            foreach (MyPlayer p in m_players.Values)
            {
                var id = new MyObjectBuilder_Checkpoint.PlayerId {ClientId = p.Id.SteamId, SerialId = p.Id.SerialId};
                var playerOb = Pool.AllocateOrCreate<MyObjectBuilder_Player>();

                playerOb.DisplayName = p.DisplayName;
                playerOb.IdentityId = p.Identity.IdentityId;
                playerOb.Connected = true;
                playerOb.ForceRealPlayer = p.IsRealPlayer;

                if (playerOb.BuildColorSlots == null)
                    playerOb.BuildColorSlots = new List<Vector3>();
                else
                    playerOb.BuildColorSlots.Clear();

                foreach (Vector3 color in p.BuildColorSlots)
                    playerOb.BuildColorSlots.Add(color);

                _checkpoint.AllPlayersData.Dictionary.Add(id, playerOb);
            }

            foreach (KeyValuePair<MyPlayer.PlayerId, long> identityPair in m_playerIdentityIds)
            {
                if (m_players.ContainsKey(identityPair.Key))
                    continue;

                var id = new MyObjectBuilder_Checkpoint.PlayerId {ClientId = identityPair.Key.SteamId, SerialId = identityPair.Key.SerialId};
                MyIdentity identity = MySession.Static.Players.TryGetIdentity(identityPair.Value);
                var playerOb = Pool.AllocateOrCreate<MyObjectBuilder_Player>();

                playerOb.DisplayName = identity?.DisplayName;
                playerOb.IdentityId = identityPair.Value;
                playerOb.Connected = false;

                if (MyCubeBuilder.AllPlayersColors != null)
                    MyCubeBuilder.AllPlayersColors.TryGetValue(identityPair.Key, out playerOb.BuildColorSlots);

                _checkpoint.AllPlayersData.Dictionary.Add(id, playerOb);
            }

            if (MyCubeBuilder.AllPlayersColors != null)
            {
                foreach (KeyValuePair<MyPlayer.PlayerId, List<Vector3>> colorPair in MyCubeBuilder.AllPlayersColors)
                {
                    //TODO: check if the player exists in m_allIdentities
                    if (m_players.ContainsKey(colorPair.Key) || m_playerIdentityIds.ContainsKey(colorPair.Key))
                        continue;

                    var id = new MyObjectBuilder_Checkpoint.PlayerId {ClientId = colorPair.Key.SteamId, SerialId = colorPair.Key.SerialId};
                    _checkpoint.AllPlayersColors.Dictionary.Add(id, colorPair.Value);
                }
            }

            _checkpoint.AllPlayersData.Dictionary.TryGetValue(cpid, out MyObjectBuilder_Player player);

            if (player != null)
            {
                //Toolbars.SaveToolbars(checkpoint);
                MyToolbar toolbar = MySession.Static.Toolbars.TryGetPlayerToolbar(ppid);
                if (toolbar != null)
                    player.Toolbar = toolbar.GetObjectBuilder();
                else if (EssentialsPlugin.Instance.Config.EnableToolbarOverride)
                {
                    player.Toolbar = EssentialsPlugin.Instance.Config.DefaultToolbar;
                    _checkpoint.CharacterToolbar = EssentialsPlugin.Instance.Config.DefaultToolbar;
                }

                //MySession.Static.Cameras.SaveCameraCollection(checkpoint);
                player.EntityCameraData = _cameraSettings;
                Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>> d = EntityCameraSettings;
                if (d.TryGetValue(ppid, out Dictionary<long, MyEntityCameraSettings> camera))
                {
                    foreach (KeyValuePair<long, MyEntityCameraSettings> cameraSetting in camera)
                    {
                        var set = Pool.AllocateOrCreate<CameraControllerSettings>();
                        set.Distance = cameraSetting.Value.Distance;
                        set.IsFirstPerson = cameraSetting.Value.IsFirstPerson;
                        set.HeadAngle = cameraSetting.Value.HeadAngle;
                        set.EntityId = cameraSetting.Key;

                        player.EntityCameraData.Add(set);
                    }
                }

                //Gpss.SaveGpss(checkpoint);

                foreach (IMyGps igps in MyAPIGateway.Session.GPS.GetGpsList(player.IdentityId))
                {
                    var gps = igps as MyGps;
                    if (!gps.IsLocal)
                    {
                        if (gps.EntityId == 0 || MyEntities.GetEntityById(gps.EntityId) != null)
                        {
                            var builder = new MyObjectBuilder_Gps.Entry
                                          {
                                              name = gps.Name,
                                              description = gps.Description,
                                              coords = gps.Coords,
                                              isFinal = gps.DiscardAt == null,
                                              showOnHud = gps.ShowOnHud,
                                              alwaysVisible = gps.AlwaysVisible,
                                              color = gps.GPSColor,
                                              entityId = gps.EntityId,
                                              DisplayName = gps.DisplayName
                                          };
                            bGps.Entries.Add(builder);
                        }
                    }
                }

                _checkpoint.Gps.Dictionary.Add(player.IdentityId, bGps);
            }

            //bunch of allocations in here, but can't replace the logic easily because private types
            _checkpoint.Factions = MySession.Static.Factions.GetObjectBuilder();

            //ok for now, clients need to know about dead identities. Might filter out those that own no blocks.
            //amount of data per ID is low, and admins usually clean them, so meh
            //_checkpoint.Identities = Sync.Players.SaveIdentities();
            foreach (MyIdentity identity in MySession.Static.Players.GetAllIdentities())
            {
                if (MySession.Static.Players.TryGetPlayerId(identity.IdentityId, out MyPlayer.PlayerId id))
                {
                    MyPlayer p = MySession.Static.Players.GetPlayerById(id);
                    if (p != null)
                        identity.LastLogoutTime = DateTime.Now;
                }

                var objectBuilder = Pool.AllocateOrCreate<MyObjectBuilder_Identity>();
                objectBuilder.IdentityId = identity.IdentityId;
                objectBuilder.DisplayName = identity.DisplayName;
                objectBuilder.CharacterEntityId = identity.Character?.EntityId ?? 0;
                objectBuilder.Model = identity.Model;
                objectBuilder.ColorMask = identity.ColorMask;
                objectBuilder.BlockLimitModifier = identity.BlockLimits.BlockLimitModifier;
                objectBuilder.LastLoginTime = identity.LastLoginTime;
                objectBuilder.LastLogoutTime = identity.LastLogoutTime;
                objectBuilder.SavedCharacters = identity.SavedCharacters;
                objectBuilder.RespawnShips = identity.RespawnShips;
                objectBuilder.LastDeathPosition = identity.LastDeathPosition;

                _checkpoint.Identities.Add(objectBuilder);
            }

            Sync.Players.RespawnComponent.SaveToCheckpoint(_checkpoint);
            //count for these is low, and the store is internal, so removing unnecessary entries is cheaper than reflection (probably?)
            _checkpoint.RespawnCooldowns.RemoveAll(i => i.PlayerSteamId != steamId);

            //checkpoint.ControlledEntities = Sync.Players.SerializeControlledEntities();
            foreach (KeyValuePair<long, MyPlayer.PlayerId> entry in MySession.Static.Players.ControlledEntities)
            {
                if (entry.Value.SteamId == steamId)
                    _checkpoint.ControlledEntities.Dictionary.Add(entry.Key, cpid);
            }

            //checkpoint.SpectatorPosition = new MyPositionAndOrientation(ref spectatorMatrix);
            //checkpoint.SpectatorIsLightOn = MySpectatorCameraController.Static.IsLightOn;
            //checkpoint.SpectatorCameraMovement = MySpectator.Static.SpectatorCameraMovement;
            //checkpoint.SpectatorDistance = (float)MyThirdPersonSpectator.Static.GetViewerDistance();
            //checkpoint.CameraController = cameraControllerEnum;
            //if (cameraControllerEnum == MyCameraControllerEnum.Entity)
            //    checkpoint.CameraEntity = ((MyEntity)CameraController).EntityId;
            //if (ControlledEntity != null)
            //{
            //    checkpoint.ControlledObject = ControlledEntity.Entity.EntityId;

            //    if (ControlledEntity is MyCharacter)
            //    {
            //        Debug.Assert(LocalCharacter == null || !(LocalCharacter.IsUsing is MyCockpit), "Character in cockpit cannot be controlled entity");
            //    }
            //}
            //else
            _checkpoint.ControlledObject = -1;


            //_checkpoint.Clients = SaveMembers_Imp(MySession.Static, false);
            if (MyMultiplayer.Static.Members.Count() > 1)
            {
                foreach (ulong member in MyMultiplayer.Static.Members)
                {
                    var ob = Pool.AllocateOrCreate<MyObjectBuilder_Client>();
                    ob.SteamId = member;
                    ob.Name = MyMultiplayer.Static.GetMemberName(member);
                    ob.IsAdmin = MySession.Static.IsUserAdmin(member);
                    _checkpoint.Clients.Add(ob);
                }
            }

            //_checkpoint.NonPlayerIdentities = Sync.Players.SaveNpcIdentities();
            foreach (long npc in MySession.Static.Players.GetNPCIdentities())
                _checkpoint.NonPlayerIdentities.Add(npc);

            //SaveSessionComponentObjectBuilders(checkpoint);
            CachingDictionary<Type, MySessionComponentBase> compDic = SessionComponents_Getter(MySession.Static);
            foreach (KeyValuePair<Type, MySessionComponentBase> entry in compDic)
            {
                //literally dozens of MB of duplicated garbage. Ignore all of it.
                //TODO: Keen fixed the duplication but this shouldn't exist at all. Rexxar has a plan
                //if (entry.Value is MyProceduralWorldGenerator)
                //    continue;

                MyObjectBuilder_SessionComponent ob = entry.Value.GetObjectBuilder();
                if (ob != null)
                    _checkpoint.SessionComponents.Add(ob);
            }

            _checkpoint.ScriptManagerData = MySession.Static.ScriptManager.GetObjectBuilder();

            //skipped on DS
            //GatherVicinityInformation(checkpoint);

            //if (OnSavingCheckpoint != null)
            //    OnSavingCheckpoint(checkpoint);
            Log.Info("Done.");
            return _checkpoint;
        }

        */
    }
}
