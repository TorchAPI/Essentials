using NLog;
using Sandbox.Game.Entities;
using System;
using System.Linq;
using System.Text;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;

namespace Essentials.Commands
{
    [Category("cleanup")]
    public class CleanupModule : CommandModule
    {
        private static readonly Logger Log = LogManager.GetLogger("Essentials");

        [Command("scan", "Find grids matching the given conditions")]
        public void Scan()
        {
            var count = ConditionsChecker.ScanConditions(Context, Context.Args).Count();
            Context.Respond($"Found {count} grids matching the given conditions.");
        }

        [Command("list", "Lists grids matching the given conditions")]
        public void List()
        {
            var grids = ConditionsChecker.ScanConditions(Context, Context.Args).OrderBy(g => g.DisplayName).ToList();
            if (Context.SentBySelf)
            {
                Context.Respond(String.Join("\n", grids.Select((g, i) => $"{i + 1}. {grids[i].DisplayName} ({grids[i].BlocksCount} block(s))")));
                Context.Respond($"Found {grids.Count} grids matching the given conditions.");
            }
            else
            {
                var m = new DialogMessage("Cleanup", null, $"Found {grids.Count} matching", String.Join("\n", grids.Select((g, i) => $"{i + 1}. {grids[i].DisplayName} ({grids[i].BlocksCount} block(s))")));
                ModCommunication.SendMessageTo(m, Context.Player.SteamUserId);
            }
        }

        [Command("delete", "Delete grids matching the given conditions")]
        public void Delete()
        {
            var count = 0;
            foreach (var grid in ConditionsChecker.ScanConditions(Context, Context.Args))
            {
                Log.Info($"Deleting grid: {grid.EntityId}: {grid.DisplayName}");
                EjectPilots(grid);
                grid.Close();
                count++;
            }

            Context.Respond($"Deleted {count} grids matching the given conditions.");
            Log.Info($"Cleanup deleted {count} grids matching conditions {string.Join(", ", Context.Args)}");
        }

        [Command("delete floatingobjects", "deletes floating objects")]
        public void FlObjDelete()
        {
            var count = 0;
            foreach (var floater in MyEntities.GetEntities().OfType<MyFloatingObject>())
            {
                Log.Info($"Deleting floating object: {floater.DisplayName}");
                floater.Close();
                count++;
            }
            Context.Respond($"Deleted {count} floating objects.");
            Log.Info($"Cleanup deleted {count} floating objects");
        }

        [Command("help", "Lists all cleanup conditions.")]
        public void Help()
        {
            var sb = new StringBuilder();
            foreach (var c in ConditionsChecker.GetAllConditions())
            {
                sb.AppendLine($"{c.Command}{(string.IsNullOrEmpty(c.InvertCommand) ? string.Empty : $" ({c.InvertCommand})")}:");
                sb.AppendLine($"   {c.HelpText}");
            }

            if (!Context.SentBySelf)
                ModCommunication.SendMessageTo(new DialogMessage("Cleanup help", null, sb.ToString()), Context.Player.SteamUserId);
            else
                Context.Respond(sb.ToString());
        }

        /// <summary>
        /// Removes pilots from grid before deleting,
        /// so the character doesn't also get deleted and break everything
        /// </summary>
        /// <param name="grid"></param>
        public void EjectPilots(MyCubeGrid grid)
        {
            var b = grid.GetFatBlocks<MyCockpit>();
            foreach (var c in b)
            {
                c.RemovePilot();
            }
        }
    }
}
