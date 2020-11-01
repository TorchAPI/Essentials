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
using static Torch.Commands.CommandTree;

namespace Essentials.Patches {
    [PatchShim]
    public static class CommandPermissionPatch {
        private static readonly Dictionary<string, CommandNode> _root = new Dictionary<string, CommandNode>();
        public static PlayerAccountModule PlayerAccountData = new PlayerAccountModule();
        public static RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static readonly MethodInfo[] methods = typeof(CommandManager).GetMethods();

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

        internal static readonly MethodInfo update = FindOverLoadMethod("HasPermission", 2);

        internal static readonly MethodInfo updatePatch =
            typeof(CommandPermissionPatch).GetMethod(nameof(CheckPermission), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find CheckPermission patch method");

        public static void Patch(PatchContext ctx) {
            ctx.GetPattern(update).Prefixes.Add(updatePatch);
            Log.Info("Patched CommandManager.HandleCommand()");
        }

        public static bool CheckPermission(ulong steamId, Command command, ref bool __result) {
            string cmd = "";
            foreach (var part in command.Path) {
                cmd += part + " ";
            }
            cmd = cmd.TrimEnd();
            Log.Fatal($"Checking {cmd}");
            string playersRank = PlayerAccountData.GetRank(steamId);
            if (!RanksAndPermissions.RankHasPermission(playersRank, cmd)) {
                Log.Info($"{steamId} tried to use the blocked command '{cmd}'");
                ModCommunication.SendMessageTo(new NotificationMessage($"You do not have permission to use that command!", 10000, "Red"), steamId);
                __result = false;
                return false;
            }
            return true;
        }
    }
}
