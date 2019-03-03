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
using VRage.GameServices;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Replication;
using VRage.Serialization;
using VRageMath;

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

        private static ITorchSessionManager _sessionManager;

        [ReflectedMethod(Name = "RaiseClientLeft")]
        private static Action<MyMultiplayerBase, ulong, MyChatMemberStateChangeEnum> _raseClientLeft;

        [ReflectedGetter(Name = "TransportLayer")]
        private static Func<MySyncLayer, object> _transportLayer;

        [ReflectedMethod(Name = "SendFlush", TypeName = "Sandbox.Engine.Multiplayer.MyTransportLayer, Sandbox.Game")]
        private static Action<object, ulong> _sendFlush;

        private static int _lastSize = 0x8000;

        [ReflectedGetter(Name = "m_callback")]
        private static Func<MyReplicationServer, IReplicationServerCallback> _getCallback;
        
        private static readonly TypedObjectPool Pool = new TypedObjectPool();

        [ReflectedGetter(Name = "m_sessionComponents")]
        private static Func<MySession, CachingDictionary<Type, MySessionComponentBase>> SessionComponents_Getter;

        [ReflectedGetter(Name = "Cameras")]
        private static Func<MySession, object> Camera_Getter;

        [ReflectedGetter(Name = "m_entityCameraSettings", TypeName = "Sandbox.Game.Multiplayer.MyCameraCollection, Sandbox.Game")]
        private static Func<object, Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>>> EntityCameraSettings_Getter;

        private static MyObjectBuilder_Checkpoint _checkpoint;
        private static MyObjectBuilder_Gps bGps;

        [ReflectedGetter(Name = "m_objectFactory", TypeName = "Sandbox.Game.Entities.MyEntityFactory, Sandbox.Game")]
        private static Func<MyObjectFactory<MyEntityTypeAttribute, MyEntity>> ObjectFactory_Getter;

        [ReflectedGetter(Name = "m_players")]
        private static Func<MyPlayerCollection, ConcurrentDictionary<MyPlayer.PlayerId, MyPlayer>> Players_Getter;

        [ReflectedGetter(Name = "m_playerIdentityIds")]
        private static Func<MyPlayerCollection, ConcurrentDictionary<MyPlayer.PlayerId, long>> PlayerIdentities_Getter;

        private static List<CameraControllerSettings> _cameraSettings;

        [ReflectedMethod(Name = "SaveInternal")]
        private static Action<MyStorageBase, Stream> _saveInternal;

        private static ITorchSessionManager SessionManager => _sessionManager ?? (_sessionManager = EssentialsPlugin.Instance.Torch.Managers.GetManager<ITorchSessionManager>());

        private static Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>> EntityCameraSettings => EntityCameraSettings_Getter(Camera_Getter(MySession.Static));

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyMultiplayerServerBase).GetMethod("OnWorldRequest", BindingFlags.NonPublic | BindingFlags.Instance)).Prefixes.Add(typeof(SessionDownloadPatch).GetMethod(nameof(PatchGetWorld), BindingFlags.NonPublic | BindingFlags.Static));
            SessionManager.OverrideModsChanged += OverrideModsChanged;
        }

        public static MyObjectBuilder_World GetClientWorld(EndpointId sender)
        {
            if (!EssentialsPlugin.Instance.Config.EnableClientTweaks)
                return MySession.Static.GetWorld(false);

            Log.Info($"Preparing world for {sender.Value}...");

            var ob = new MyObjectBuilder_World
                     {
                         Checkpoint = GetClientCheckpoint(sender.Value),
                         Sector = GetClientSector(sender.Value),
                         Planets = MySession.Static.GetPlanetObjectBuilders()
                     };

            if (EssentialsPlugin.Instance.Config.PackPlanets)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                ob.VoxelMaps = new SerializableDictionary<string, byte[]>(GetUncompressedVoxels(true));
                stopwatch.Stop();
                Log.Info($"Voxel snapshot took {stopwatch.Elapsed.TotalMilliseconds}ms");
            }
            else
                ob.VoxelMaps = new SerializableDictionary<string, byte[]>();

            return ob;
        }

        /// <summary>
        /// Gets uncompressed storage data for all planets in the world.
        /// Data is wrapped in a no-compression gzip stream for vanilla client compatibility.
        /// </summary>
        /// <param name="includeChanged"></param>
        /// <returns></returns>
        public static Dictionary<string, byte[]> GetUncompressedVoxels(bool includeChanged)
        {
            var voxelCache = new Dictionary<string, byte[]>(MySession.Static.VoxelMaps.Instances.Count);
            var i = 0;
            Log.Info("Taking voxel snapshots");
            foreach (MyVoxelBase voxelMap in MySession.Static.VoxelMaps.Instances)
            {
                if (!(voxelMap is MyPlanet))
                    continue;

                if (includeChanged == false && (voxelMap.ContentChanged || voxelMap.BeforeContentChanged))
                    continue;

                if (voxelMap.Save == false)
                    continue;

                if (voxelCache.ContainsKey(voxelMap.StorageName))
                    continue;

                Log.Info($"{i++}: {voxelMap.StorageName}");
                SaveUncompressed((MyStorageBase)voxelMap.Storage, out byte[] data);
                voxelCache.Add(voxelMap.StorageName, data);
            }
            Log.Info("Voxel snapshot finished");
            return voxelCache;
        }

        /// <summary>
        /// Gets the uncompressed, gzip-wrapped storage data for a given voxel map.
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="data"></param>
        private static void SaveUncompressed(MyStorageBase storage, out byte[] data)
        {
            if (storage.CachedWrites)
                storage.WritePending(true);

            using (var ms = new MemoryStream(0x8000))
            using (var gz = new GZipStream(ms, CompressionLevel.NoCompression))
            using (var bf = new BufferedStream(gz, 0x8000))
            {
                string name;
                int version;
                if (storage is MyOctreeStorage)
                {
                    name = "Octree";
                    version = 1;
                }
                else
                    throw new InvalidBranchException("Keen what are you DOING");
                bf.WriteNoAlloc(name);
                bf.Write7BitEncodedInt(version);

                _saveInternal(storage, bf);

                bf.Flush();
                gz.Flush();

                data = ms.ToArray();
            }
        }

        /// <summary>
        /// Event handler for injected client mods.
        /// Needed because we cache the checkpoint and are too lazy to update this list all the time
        /// </summary>
        /// <param name="args"></param>
        private static void OverrideModsChanged(CollectionChangeEventArgs args)
        {
            switch (args.Action)
            {
                case CollectionChangeAction.Add:
                    _checkpoint?.Mods.Add((MyObjectBuilder_Checkpoint.ModItem)args.Element);
                    break;
                case CollectionChangeAction.Remove:
                    _checkpoint?.Mods.Remove((MyObjectBuilder_Checkpoint.ModItem)args.Element);
                    break;
            }
        }

        //private static void Init()
        //{
        //    MySession.Static.Factions.FactionStateChanged += (change, l, arg3, arg4, arg5) => _dirty = true;
        //    MySession.Static.Factions.FactionCreated += id => _dirty = true;
        //    MySession.Static.Factions.FactionEdited += id => _dirty = true;
        //    MySession.Static.Factions.FactionAutoAcceptChanged += (l, b, arg3) => _dirty = true;

        //    MySession.Static.ChatSystem.FactionHistoryDeleted += () => _dirty = true;
        //    MySession.Static.ChatSystem.FactionMessageReceived += id => _dirty = true;
        //    MySession.Static.ChatSystem.PlayerMessageReceived += id => _dirty = true;
        //}


        /// <summary>
        /// Main entry point to this class. Prefix on MyMultiplayerBase.OnWorldRequest.
        /// Effectively replaces all client join logic.
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private static bool PatchGetWorld(EndpointId sender)
        {
            if (!EssentialsPlugin.Instance.Config.EnableClientTweaks)
                return true;

            Log.Info($"World request received: {MyMultiplayer.Static.GetMemberName(sender.Value)}");

            if (MyMultiplayer.Static.KickedClients.ContainsKey(sender.Value) || MyMultiplayer.Static.BannedClients.Contains(sender.Value) || MySandboxGame.ConfigDedicated?.Banned.Contains(sender.Value) == true)
            {
                //a hacked client or a plugin can request world data without properly joining the server
                Log.Error("Banned client requested world. This is bad.");
                _raseClientLeft(MyMultiplayer.Static, sender.Value, MyChatMemberStateChangeEnum.Banned);
                return false;
            }

            if (MySession.Static == null)
            {
                Log.Error("World is not loaded!");
                return false;
            }

            //taking a session snapshot is the only thing that must be done synchronously
            MyObjectBuilder_World worldData = GetClientWorld(sender);
            Stopwatch stopwatch = Stopwatch.StartNew();
            worldData.Checkpoint.WorkshopId = null;
            stopwatch.Stop();
            Log.Info($"World checkpoint took {stopwatch.Elapsed.TotalMilliseconds}ms");

            if (worldData.Clusters == null)
                worldData.Clusters = new List<BoundingBoxD>();
            worldData.Clusters.Clear();
            MyPhysics.SerializeClusters(worldData.Clusters);

            //serializing, compressing, and sending the data can all be dumped into a thread
            if (EssentialsPlugin.Instance.Config.AsyncJoin)
                Task.Run(() => PackAndSend(worldData, sender));
            else
                PackAndSend(worldData, sender);

            return false;
        }

        /// <summary>
        /// Utility to serialize, compress, and send the world data
        /// </summary>
        /// <param name="worldData"></param>
        /// <param name="sender"></param>
        private static void PackAndSend(MyObjectBuilder_World worldData, EndpointId sender)
        {
            Log.Info("Beginning world compression...");
            try
            {
                using (var worldStream = new MemoryStream())
                {
                    SerializeZipped(worldStream, worldData, EssentialsPlugin.Instance.Config.CompressionLevel, _lastSize);
                    _sendFlush(_transportLayer(MyMultiplayer.Static.SyncLayer), sender.Value);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    byte[] buffer = worldStream.ToArray();
                    Log.Info($"Sending {Utilities.FormatDataSize(buffer.Length)} world data...");
                    SendWorld(buffer, sender);
                    stopwatch.Stop();
                    Log.Info($"Data flush took {stopwatch.Elapsed.TotalMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failure during world compression!");
            }
        }

        /// <summary>
        /// Replacement for MyObjectBuilderSerializer that lets you set compression level.
        /// </summary>
        /// <param name="writeTo"></param>
        /// <param name="obj"></param>
        /// <param name="level"></param>
        /// <param name="bufferSize"></param>
        private static void SerializeZipped(Stream writeTo, MyObjectBuilder_Base obj, CompressionLevel level, int bufferSize = 0x8000)
        {
            var ms = new MemoryStream(bufferSize);
            Stopwatch stopwatch = Stopwatch.StartNew();
            XmlSerializer serializer = MyXmlSerializerManager.GetSerializer(obj.GetType());
            serializer.Serialize(ms, obj);
            stopwatch.Stop();
            Log.Info($"Serialization took {stopwatch.Elapsed.TotalMilliseconds}ms");
            ms.Position = 0;
            Log.Info($"Wrote {Utilities.FormatDataSize(ms.Length)}");
            _lastSize = Math.Max(_lastSize, (int)ms.Length);
            stopwatch.Restart();
            using (var gz = new GZipStream(writeTo, level))
            {
                ms.CopyTo(gz);
            }
            stopwatch.Stop();
            Log.Info($"Compression took {stopwatch.Elapsed.TotalMilliseconds}ms");
            ms.Close();
        }

        /// <summary>
        /// Replaces some truly awful Keencode that involves a handful of branches and callvirt
        /// for *each byte* in the byte array. This is at least a 5x improvement in tests.
        /// </summary>
        /// <param name="worldData"></param>
        /// <param name="sendTo"></param>
        private static void SendWorld(byte[] worldData, EndpointId sendTo)
        {
            IReplicationServerCallback callback = _getCallback((MyReplicationServer)MyMultiplayer.Static.ReplicationLayer);
            MyPacketDataBitStreamBase data = callback.GetBitStreamPacketData();
            data.Stream.WriteVariant((uint)worldData.Length);
            for (var i = 0; i < worldData.Length; i++)
                data.Stream.WriteByte(worldData[i]);
            callback.SendWorld(data, sendTo);
        }

        /// <summary>
        /// Gets a session snapshot customized for an individual player. Removes data that player does not need,
        /// which improves download times, and fixes some exploits.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        private static MyObjectBuilder_Checkpoint GetClientCheckpoint(ulong steamId)
        {
            Log.Info($"Saving checkpoint...");
            var cpid = new MyObjectBuilder_Checkpoint.PlayerId(steamId);
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
                    player.Toolbar = EssentialsPlugin.Instance.Config.DefaultToolbar;

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

            if (MyFakes.ENABLE_MISSION_TRIGGERS)
                //usually empty, so meh
                _checkpoint.MissionTriggers = MySessionComponentMissionTriggers.Static.GetObjectBuilder();

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

            //SaveChatHistory(checkpoint);
            /*
            if (player != null && MySession.Static.ChatHistory.TryGetValue(player.IdentityId, out MyChatHistory playerChat))
            {
                var builder = Pool.AllocateOrCreate<MyObjectBuilder_ChatHistory>();
                builder.IdentityId = playerChat.IdentityId;
                if (builder.PlayerChatHistory != null)
                    Pool.DeallocateAndClear(builder.PlayerChatHistory);
                else
                    builder.PlayerChatHistory = new List<MyObjectBuilder_PlayerChatHistory>();
                foreach (MyPlayerChatHistory chat in playerChat.PlayerChatHistory.Values)
                {
                    var cb = Pool.AllocateOrCreate<MyObjectBuilder_PlayerChatHistory>();
                    if (cb.Chat != null)
                        Pool.DeallocateAndClear(cb.Chat);
                    else
                        cb.Chat = new List<MyObjectBuilder_PlayerChatItem>();
                    cb.IdentityId = chat.IdentityId;
                    foreach (MyPlayerChatItem m in chat.Chat)
                    {
                        var mb = Pool.AllocateOrCreate<MyObjectBuilder_PlayerChatItem>();
                        mb.Text = m.Text;
                        mb.IdentityIdUniqueNumber = MyEntityIdentifier.GetIdUniqueNumber(m.IdentityId);
                        mb.TimestampMs = (long)m.Timestamp.TotalMilliseconds;
                        mb.Sent = m.Sent;
                        cb.Chat.Add(mb);
                    }
                    builder.PlayerChatHistory.Add(cb);
                }

                if (builder.GlobalChatHistory == null)
                    builder.GlobalChatHistory = Pool.AllocateOrCreate<MyObjectBuilder_GlobalChatHistory>();
                if (builder.GlobalChatHistory.Chat != null)
                    Pool.DeallocateAndClear(builder.GlobalChatHistory.Chat);
                else
                    builder.GlobalChatHistory.Chat = new List<MyObjectBuilder_GlobalChatItem>();

                foreach (MyGlobalChatItem g in playerChat.GlobalChatHistory.Chat)
                {
                    var gb = Pool.AllocateOrCreate<MyObjectBuilder_GlobalChatItem>();
                    gb.Text = g.Text;
                    gb.Font = g.AuthorFont;
                    if (g.IdentityId == 0)
                    {
                        gb.IdentityIdUniqueNumber = 0;
                        gb.Author = g.Author;
                    }
                    else
                    {
                        gb.IdentityIdUniqueNumber = MyEntityIdentifier.GetIdUniqueNumber(g.IdentityId);
                        gb.Author = string.Empty;
                    }
                    builder.GlobalChatHistory.Chat.Add(gb);
                }

                _checkpoint.ChatHistory.Add(builder);
            }

            if (player != null)
            {
                IMyFaction pfac = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
                if (pfac != null)
                {
                    foreach (MyFactionChatHistory history in MySession.Static.FactionChatHistory)
                    {
                        if (history.FactionId1 == pfac.FactionId || history.FactionId2 == pfac.FactionId)
                        {
                            var builder = Pool.AllocateOrCreate<MyObjectBuilder_FactionChatHistory>();
                            if (builder.Chat != null)
                                Pool.DeallocateAndClear(builder.Chat);
                            else
                                builder.Chat = new List<MyObjectBuilder_FactionChatItem>();

                            builder.FactionId1 = history.FactionId1;
                            builder.FactionId2 = history.FactionId2;

                            foreach (MyFactionChatItem fc in history.Chat)
                            {
                                if (fc.PlayersToSendTo != null && fc.PlayersToSendTo.Count > 0)
                                {
                                    var fb = Pool.AllocateOrCreate<MyObjectBuilder_FactionChatItem>();
                                    fb.Text = fc.Text;
                                    fb.IdentityIdUniqueNumber = MyEntityIdentifier.GetIdUniqueNumber(fc.IdentityId);
                                    fb.TimestampMs = (long)fc.Timestamp.TotalMilliseconds;
                                    if (fb.PlayersToSendToUniqueNumber != null)
                                        fb.PlayersToSendToUniqueNumber.Clear();
                                    else
                                        fb.PlayersToSendToUniqueNumber = new List<long>();
                                    if (fb.IsAlreadySentTo != null)
                                        fb.IsAlreadySentTo.Clear();
                                    else
                                        fb.IsAlreadySentTo = new List<bool>();
                                    foreach (KeyValuePair<long, bool> pair in fc.PlayersToSendTo)
                                    {
                                        fb.PlayersToSendToUniqueNumber.Add(MyEntityIdentifier.GetIdUniqueNumber(pair.Key));
                                        fb.IsAlreadySentTo.Add(pair.Value);
                                    }
                                    builder.Chat.Add(fb);
                                }
                            }
                        }
                    }
                }
            }
            */

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

        /// <summary>
        /// Gets entities to pack into the session snapshot, customized for a given user
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        private static MyObjectBuilder_Sector GetClientSector(ulong steamId)
        {
            MyObjectBuilder_Sector ob = MySession.Static.GetSector(false);

            if (EssentialsPlugin.Instance.Config.PackRespawn)
            {
                ob.SectorObjects = new List<MyObjectBuilder_EntityBase>();
                var grids = new HashSet<MyCubeGrid>();
                /*
                foreach (IMyMedicalRoomProvider room in MyMedicalRoomsSystem.GetMedicalRoomsInScene())
                {
                    if (room.Closed || !room.IsWorking || !(room.SetFactionToSpawnee || room.HasPlayerAccess(MySession.Static.Players.TryGetIdentityId(steamId))))
                        continue;

                    grids.Add(((MyMedicalRoom)room).CubeGrid);
                }
                */
                foreach (var respawn in MyRespawnComponent.GetAllRespawns())
                {
                    if (respawn.Entity.Closed || !respawn.Entity.IsWorking || !respawn.CanPlayerSpawn(MySession.Static.Players.TryGetIdentityId(steamId), true))
                        continue;

                    grids.Add(respawn.Entity.CubeGrid);
                }

                foreach (MyCubeGrid spawngrid in grids)
                {
                    if (EssentialsPlugin.Instance.Config.MaxPackedRespawnSize > 0 && spawngrid.BlocksCount > EssentialsPlugin.Instance.Config.MaxPackedRespawnSize)
                        continue;

                    ob.SectorObjects.Add(spawngrid.GetObjectBuilder());
                }
            }

            return ob;
        }
    }
}
