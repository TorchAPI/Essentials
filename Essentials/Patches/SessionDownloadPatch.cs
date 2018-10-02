using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Serialization;
using VRage.Utils;

namespace Essentials.Patches
{
    public class SessionDownloadPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(typeof(MyMultiplayerServerBase).GetMethod("OnWorldRequest", BindingFlags.NonPublic | BindingFlags.Instance)).Transpilers.Add(typeof(SessionDownloadPatch).GetMethod(nameof(PatchGetWorld), BindingFlags.NonPublic | BindingFlags.Static));
        }

        private static IEnumerable<MsilInstruction> PatchGetWorld(IEnumerable<MsilInstruction> input)
        {
            foreach (var ins in input)
            {
                if ((ins.OpCode == OpCodes.Callvirt || ins.OpCode == OpCodes.Call) && ins.Operand is MsilOperandInline<MethodBase> mdo && mdo.Value.Name == "GetWorld")
                {
                    yield return new MsilInstruction(OpCodes.Pop);
                    yield return new MsilInstruction(OpCodes.Pop);
                    yield return new MsilInstruction(OpCodes.Ldarg_1);
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(GetWorldInfo);
                }
                else if (ins.OpCode == OpCodes.Stfld && ins.Operand is MsilOperandInline<FieldInfo> fdo && fdo.Value.Name == "CharacterToolbar")
                {
                    yield return new MsilInstruction(OpCodes.Pop);
                    yield return new MsilInstruction(OpCodes.Pop);
                }
                else
                    yield return ins;
            }
        }

        private static readonly MethodInfo GetWorldInfo = typeof(SessionDownloadPatch).GetMethod(nameof(GetClientWorld), BindingFlags.Public | BindingFlags.Static);

        public static MyObjectBuilder_World GetClientWorld(EndpointId sender)
        {
            Log.Info($"Preparing world for {sender.Value}...");

            var ob = new MyObjectBuilder_World()
                     {
                         Checkpoint = GetClientCheckpoint(sender.Value),
                         VoxelMaps = new SerializableDictionary<string, byte[]>(),
                         Sector = GetClientSector(sender.Value)
                     };

            return ob;
        }

        private static readonly FieldInfo CamerasField = typeof(MySession).GetField("Cameras", BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly Type CameraCollectionType = CamerasField.FieldType;
        private static readonly FieldInfo AllCameraField = CameraCollectionType.GetField("m_entityCameraSettings", BindingFlags.NonPublic|BindingFlags.Instance);
        
        private static MyObjectBuilder_Checkpoint GetClientCheckpoint(ulong steamId)
        {
            Log.Info($"Saving checkpoint...");
            var cpid = new MyObjectBuilder_Checkpoint.PlayerId(steamId);
            var ppid = new MyPlayer.PlayerId(steamId);

            var checkpoint = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Checkpoint>();
            var settings = MyObjectBuilderSerializer.Clone(MySession.Static.Settings) as MyObjectBuilder_SessionSettings;

            settings.ScenarioEditMode |= MySession.Static.PersistentEditMode;

            checkpoint.SessionName = MySession.Static.Name;
            checkpoint.Description = MySession.Static.Description;
            checkpoint.PromotedUsers = new SerializableDictionary<ulong, MyPromoteLevel>(MySession.Static.PromotedUsers);
            checkpoint.CreativeTools = new HashSet<ulong>();
            if(MySession.Static.CreativeToolsEnabled(steamId))
                checkpoint.CreativeTools.Add(steamId);
            //checkpoint.Briefing = Briefing;
            //checkpoint.BriefingVideo = BriefingVideo;
            // AI School scenarios are meant to be read-only, saving makes the game a normal game with a bot.
            //checkpoint.Password = Password;
            checkpoint.LastSaveTime = DateTime.Now;
            checkpoint.WorkshopId = MySession.Static.WorkshopId;
            checkpoint.ElapsedGameTime = MySession.Static.ElapsedGameTime.Ticks;
            checkpoint.InGameTime = MySession.Static.InGameTime;
            checkpoint.Settings = settings;
            checkpoint.Mods = MySession.Static.Mods;
            //TODO
            //checkpoint.CharacterToolbar = MyToolbarComponent.CharacterToolbar.GetObjectBuilder();
            checkpoint.CharacterToolbar = null;
            checkpoint.Scenario = MySession.Static.Scenario.Id;
            checkpoint.WorldBoundaries =MySession.Static.WorldBoundaries;
            checkpoint.PreviousEnvironmentHostility = MySession.Static.PreviousEnvironmentHostility;
            checkpoint.RequiresDX = MySession.Static.RequiresDX;
            //TODO
            checkpoint.CustomLoadingScreenImage = MySession.Static.CustomLoadingScreenImage;
            checkpoint.CustomLoadingScreenText = EssentialsPlugin.Instance.Config.LoadingText ?? MySession.Static.CustomLoadingScreenText;
            checkpoint.CustomSkybox = MySession.Static.CustomSkybox;

            checkpoint.GameDefinition = MySession.Static.GameDefinition.Id;
            checkpoint.SessionComponentDisabled = MySession.Static.SessionComponentDisabled;
            checkpoint.SessionComponentEnabled = MySession.Static.SessionComponentEnabled;

            //  checkpoint.PlayerToolbars = Toolbars.GetSerDictionary();

            Sync.Players.SavePlayers(checkpoint);

            checkpoint.AllPlayersData.Dictionary.TryGetValue(cpid, out MyObjectBuilder_Player player);

            if (player != null)
            {
                //Toolbars.SaveToolbars(checkpoint);
                var toolbar = MySession.Static.Toolbars.TryGetPlayerToolbar(ppid);
                if (toolbar != null)
                    player.Toolbar = toolbar.GetObjectBuilder();

                //MySession.Static.Cameras.SaveCameraCollection(checkpoint);

                player.EntityCameraData = new List<CameraControllerSettings>(8);
                var d = AllCameraField.GetValue(CamerasField.GetValue(MySession.Static)) as Dictionary<MyPlayer.PlayerId, Dictionary<long, MyEntityCameraSettings>>;
                if (d.TryGetValue(ppid, out Dictionary<long, MyEntityCameraSettings> camera))
                {
                    foreach (var cameraSetting in camera)
                    {
                        CameraControllerSettings set = new CameraControllerSettings()
                        {
                            Distance = cameraSetting.Value.Distance,
                            IsFirstPerson = cameraSetting.Value.IsFirstPerson,
                            HeadAngle = cameraSetting.Value.HeadAngle,
                            EntityId = cameraSetting.Key,
                        };
                        player.EntityCameraData.Add(set);
                    }
                }

                //Gpss.SaveGpss(checkpoint);
                checkpoint.Gps = new SerializableDictionary<long, MyObjectBuilder_Gps>();
                MyObjectBuilder_Gps bGps = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Gps>();
                bGps.Entries = new List<MyObjectBuilder_Gps.Entry>();

                foreach (var igps in MyAPIGateway.Session.GPS.GetGpsList(player.IdentityId))
                {
                    var gps = igps as MyGps;
                    if (!gps.IsLocal)
                    {
                        if (gps.EntityId == 0 || MyEntities.GetEntityById(gps.EntityId) != null)
                            bGps.Entries.Add(new MyObjectBuilder_Gps.Entry
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
                                             });
                    }
                }

                checkpoint.Gps.Dictionary.Add(player.IdentityId, bGps);
            }

            if (MyFakes.ENABLE_MISSION_TRIGGERS)
                checkpoint.MissionTriggers = MySessionComponentMissionTriggers.Static.GetObjectBuilder();


            if (MyFakes.SHOW_FACTIONS_GUI)
                checkpoint.Factions = MySession.Static.Factions.GetObjectBuilder();
            else
                checkpoint.Factions = null;

            //ok for now, clients need to know about dead identities. Might filter out those that own no blocks.
            //amount of data per ID is low, and admins usually clean them, so meh
            checkpoint.Identities = Sync.Players.SaveIdentities();

            checkpoint.RespawnCooldowns = new List<MyObjectBuilder_Checkpoint.RespawnCooldownItem>();
            Sync.Players.RespawnComponent.SaveToCheckpoint(checkpoint);
            //count for these is low, and the store is internal, so removing unnecessary entries is cheaper than reflection (probably?)
            checkpoint.RespawnCooldowns.RemoveAll(i => i.PlayerSteamId!=steamId);

            //checkpoint.ControlledEntities = Sync.Players.SerializeControlledEntities();
            checkpoint.ControlledEntities = new SerializableDictionary<long, MyObjectBuilder_Checkpoint.PlayerId>();
            //Log.Info(MySession.Static.Players.ControlledEntities.Count);
            foreach (var entry in MySession.Static.Players.ControlledEntities)
            {
                //Log.Info($"{entry.Value.SteamId} : {entry.Key}");
                if (entry.Value.SteamId == steamId)
                {
                    checkpoint.ControlledEntities.Dictionary.Add(entry.Key, new MyObjectBuilder_Checkpoint.PlayerId()
                                                                            {
                                                                                ClientId = entry.Value.SteamId,
                                                                                SerialId = entry.Value.SerialId
                                                                            });
                    //Log.Info("Added");
                }
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
                checkpoint.ControlledObject = -1;

            //SaveChatHistory(checkpoint);
            checkpoint.ChatHistory = new List<MyObjectBuilder_ChatHistory>(1);
            if(player != null && MySession.Static.ChatHistory.TryGetValue(player.IdentityId, out MyChatHistory playerChat))
                checkpoint.ChatHistory.Add(playerChat.GetObjectBuilder());

            checkpoint.FactionChatHistory = new List<MyObjectBuilder_FactionChatHistory>(8);
            if (player != null)
            {
                var pfac = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
                if (pfac != null)
                {
                    foreach (var history in MySession.Static.FactionChatHistory)
                    {
                        if (history.FactionId1 == pfac.FactionId || history.FactionId2 == pfac.FactionId)
                            checkpoint.FactionChatHistory.Add(history.GetObjectBuilder());
                    }
                }
            }

            checkpoint.AppVersion = MyFinalBuildConstants.APP_VERSION;

            checkpoint.Clients = SaveMembers_Imp(MySession.Static, false);

            checkpoint.NonPlayerIdentities = Sync.Players.SaveNpcIdentities();

            //SaveSessionComponentObjectBuilders(checkpoint);
            var compDic = SessionComponents_Getter(MySession.Static);
            checkpoint.SessionComponents = new List<MyObjectBuilder_SessionComponent>(compDic.Values.Count);
            foreach (var entry in compDic)
            {
                //literally dozens of MB of duplicated garbage. Ignore all of it.
                if(entry.Value is MyProceduralWorldGenerator)
                    continue;

                var ob = entry.Value.GetObjectBuilder();
                if(ob != null)
                    checkpoint.SessionComponents.Add(ob);
            }

            checkpoint.ScriptManagerData = MySession.Static.ScriptManager.GetObjectBuilder();

            //skipped on DS
            //GatherVicinityInformation(checkpoint);
            
            //if (OnSavingCheckpoint != null)
            //    OnSavingCheckpoint(checkpoint);
            return checkpoint;
        }

        [ReflectedMethod(Name = "SaveMembers")]
        private static Func<MySession, bool, List<MyObjectBuilder_Client>> SaveMembers_Imp;

        [ReflectedGetter(Name = "m_sessionComponents")]
        private static Func<MySession, CachingDictionary<Type, MySessionComponentBase>> SessionComponents_Getter;

        private static MyObjectBuilder_Sector GetClientSector(ulong steamId)
        {
            var ob = MySession.Static.GetSector(false);
            
            if (EssentialsPlugin.Instance.Config.PackRespawn)
            {
                ob.SectorObjects = new List<MyObjectBuilder_EntityBase>();
                var grids = new HashSet<MyCubeGrid>();
                foreach (var room in MyMedicalRoomsSystem.GetMedicalRoomsInScene())
                {
                    if (room.Closed || !room.IsWorking || !(room.SetFactionToSpawnee || room.HasPlayerAccess(MySession.Static.Players.TryGetIdentityId(steamId))))
                        continue;

                    grids.Add(((MyMedicalRoom)room).CubeGrid);
                }

                foreach (var spawngrid in grids)
                {
                    if(EssentialsPlugin.Instance.Config.MaxPackedRespawnSize > 0 && spawngrid.BlocksCount > EssentialsPlugin.Instance.Config.MaxPackedRespawnSize)
                        continue;

                    ob.SectorObjects.Add(spawngrid.GetObjectBuilder());
                }
            }

            return ob;
        }
    }
}
