﻿
using System.Reflection;
using Torch.Managers.PatchManager;
using NLog;
using VRage.Network;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.ChatManager;
using VRage.Game;
using Torch.Utils;
using Sandbox.Game.Gui;
using System;
using Sandbox.Game.World;
using VRage.GameServices;

namespace Essentials.Patches {
    [PatchShim]
    public static class ChatMessagePatch {
        public static PlayerAccountModule PlayerAccountData = new PlayerAccountModule();
        public static RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();
        public static bool debug = false;

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static MethodInfo FindOverLoadMethod( MethodInfo[] methodInfo,string name, int parameterLenth) {
            MethodInfo method = null;
            foreach (var DecalredMethod in methodInfo) {
                if (debug)
                    Log.Info($"Method name: {DecalredMethod.Name}");
                if (DecalredMethod.GetParameters().Length == parameterLenth && DecalredMethod.Name == name) {
                    method = DecalredMethod;
                    break;
                }
            }
            return method;
        }

        public static void Patch(PatchContext ctx) {
            try {
                var target = FindOverLoadMethod(typeof(MyMultiplayerBase).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static), "OnChatMessageReceived_Server", 1);
                var patchMethod = typeof(ChatMessagePatch).GetMethod(nameof(OnChatMessageReceived_Server), BindingFlags.Static | BindingFlags.NonPublic);
                ctx.GetPattern(target).Prefixes.Add(patchMethod);

                Log.Info("Patched OnChatMessageReceived_Server!");
            }
            catch {
                Log.Error("Failed to patch!");
            }
        }

        private static bool OnChatMessageReceived_Server(ref ChatMsg msg) {
            if (EssentialsPlugin.Instance.Config.EnableRanks) {
                var Account = PlayerAccountData.GetAccount(msg.Author);
                if (Account != null) {
                    var Rank = RanksAndPermissions.GetRankData(Account.Rank);
                    if (Rank.DisplayPrefix) {
                        msg.Author = 0;
                        var customData = msg.CustomData ?? new ChatMessageCustomData();
                        customData.AuthorName = $"{Rank.Prefix}{Account.Player}";
                        msg.CustomData = customData;
                    }
                }
                return true;
            }
            return true;
        }
      
    }
}