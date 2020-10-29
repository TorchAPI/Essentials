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

namespace Essentials.Patches {
    [PatchShim]
    public static class CommandPermissionPatch {
        public static PlayerAccountModule PlayerAccountData = new PlayerAccountModule();
        public static RanksAndPermissionsModule RanksAndPermissions = new RanksAndPermissionsModule();

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        internal static readonly MethodInfo update =
            typeof(CommandManager).GetMethod(nameof(CommandManager.HasPermission), BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find HasPermission method");

        internal static readonly MethodInfo updatePatch =
            typeof(CommandPermissionPatch).GetMethod(nameof(CheckPermission), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find CheckPermission patch method");

        public static void Patch(PatchContext ctx) {

            ctx.GetPattern(update).Prefixes.Add(updatePatch);
            Log.Info("Patched CommandManager.HasPermission()");
        }

        public static bool CheckPermission(ulong steamId, Command command) {
            string cmd = "";
            foreach (var part in command.Path) {
                cmd += part + " ";
            }
            cmd = cmd.TrimEnd();
            string playersRank = PlayerAccountData.GetRank(steamId);
            if (!RanksAndPermissions.RankHasPermission(playersRank, cmd)) {
                ModCommunication.SendMessageTo(new NotificationMessage($"You do not have permission to use that command!", 10000, "Red"), steamId);
                return false;
            }
            return true;
        }
    }
}
