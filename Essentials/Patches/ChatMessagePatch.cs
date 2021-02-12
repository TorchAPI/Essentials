using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Torch.Commands;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;
using NLog;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.API.Managers;
using VRage.Network;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Multiplayer;
using Torch.Managers.ChatManager;
using VRage.Game;
using Torch.Utils;

namespace Essentials.Patches {
    [PatchShim]
    public static class ChatMessagePatch {
        public static PlayerAccountModule PlayerAccountData = new PlayerAccountModule();
        public static RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static readonly MethodInfo[] methods = typeof(ChatManagerServer).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);

        public static MethodInfo FindOverLoadMethod(string name, int parameterLenth) {
            MethodInfo method = null;
            foreach (var DecalredMethod in methods) {
                if (DecalredMethod.GetParameters().Length == parameterLenth && DecalredMethod.Name == name) {
                    method = DecalredMethod;
                    break;
                }
            }
            return method;
        }

        public static void Patch(PatchContext ctx) {
            var target = FindOverLoadMethod("RaiseMessageRecieved", 2);
            var patchMethod = typeof(ChatMessagePatch).GetMethod(nameof(ChatPrefixProcessing), BindingFlags.Static | BindingFlags.NonPublic);
            ctx.GetPattern(target).Prefixes.Add(patchMethod);
            Log.Info("Patched RaiseMessageRecieved!");
        }

        private static bool ChatPrefixProcessing(ChatMsg message) {

            var Account = PlayerAccountData.GetAccount(message.Author);
            var Rank = RanksAndPermissions.GetRankData(Account.Rank);
            if (Rank.DisplayPrefix) {

                var scripted = new ScriptedChatMsg() {
                    Author = $"{Rank.Prefix}{Account.Player}",
                    Text = message.Text,
                    Target = 0,
                    Font = MyFontEnum.White,
                    Color = ColorUtils.TranslateColor("White")
            };

               
                MyMultiplayerBase.SendScriptedChatMessage(ref scripted);
                Log.Info($"{scripted.Author}: {scripted.Text}");
                return false;
            }

            return true;
        }
    }
}