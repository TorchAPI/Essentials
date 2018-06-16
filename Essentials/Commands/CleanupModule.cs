using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;
using NLog;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Multiplayer;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using Vector3D = VRageMath.Vector3D;

namespace Essentials.Commands
{
    [Category("cleanup")]
    public class CleanupModule : CommandModule
    {
        [Command("scan", "Find grids matching the given conditions: hastype, notype, hassubtype, nosubtype, blockslessthan, blocksgreaterthan, ownedby")]
        public void Scan()
        {
            var count = ScanConditions(Context.Args).Count();
            Context.Respond($"Found {count} grids matching the given conditions.");
        }

        [Command("list", "Lists grids matching the given conditions: hastype, notype, hassubtype, nosubtype, blockslessthan, blocksgreaterthan, ownedby")]
        public void List()
        {
            var grids = ScanConditions(Context.Args).OrderBy(g => g.DisplayName).ToList();
            Context.Respond(String.Join("\n", grids.Select((g, i) => $"{i + 1}. {grids[i].DisplayName} ({grids[i].BlocksCount} block(s))")));
            Context.Respond($"Found {grids.Count} grids matching the given conditions.");
        }

        [Command("delete", "Delete grids matching the given conditions")]
        public void Delete()
        {
            var count = 0;
            foreach (var grid in ScanConditions(Context.Args))
            {
                EssentialsPlugin.Log.Info($"Deleting grid: {grid.EntityId}: {grid.DisplayName}");
                grid.Close();
                count++;
            }

            Context.Respond($"Deleted {count} grids matching the given conditions.");
            EssentialsPlugin.Log.Info($"Cleanup deleted {count} grids matching conditions {string.Join(", ", Context.Args)}");
        }

        [Command("help", "Lists all cleanup conditions.")]
        public void Help()
        {
            var sb = new StringBuilder();
            foreach (var c in _conditionLookup)
            {
                sb.AppendLine($"{c.Command}{(string.IsNullOrEmpty(c.InvertCommand) ? string.Empty : $" ({c.InvertCommand})")}:");
                sb.AppendLine($"   {c.HelpText}");
            }

            if(!Context.SentBySelf)
            ModCommunication.SendMessageTo(new DialogMessage("Cleanup help", null, sb.ToString()), Context.Player.SteamUserId);
            else
                Context.Respond(sb.ToString());
        }

        public CleanupModule()
        {
            var methods = typeof(CleanupModule).GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            _conditionLookup = new List<Condition>(methods.Length);
            foreach (var m in methods)
            {
                var a = m.GetCustomAttribute<ConditionAttribute>();
                if (a == null)
                    continue;

                if (m.ReturnType != typeof(bool))
                {
                    EssentialsPlugin.Log.Warn($"Command {a.Command} does not return a bool! Skipping!");
                    continue;
                }
                var p = m.GetParameters();
                if (p.Length == 0 || p[0].ParameterType != typeof(MyCubeGrid))
                {
                    EssentialsPlugin.Log.Warn($"Command {a.Command} does not accept MyCubeGrid as first parameter! Skipping!");
                    continue;
                }

                Func<MyCubeGrid, string, bool> func;

                if (p.Length == 1)
                    func = (grid, s) => (bool)m.Invoke(this, new object[] {grid});
                else if (p.Length > 1 && p[1].ParameterType == typeof(string))
                    func = (grid, s) => (bool)m.Invoke(this, new object[] {grid, s});
                else
                    throw new InvalidBranchException();

                var c = new Condition();
                c.Command = a.Command;
                c.InvertCommand = a.InvertCommand;
                c.HelpText = a.HelpText;
                c.Action = func;

                _conditionLookup.Add(c);
            }
        }

        private List<Condition> _conditionLookup;

        private IEnumerable<MyCubeGrid> ScanConditions(IReadOnlyList<string> args)
        {
            var conditions = new List<Func<MyCubeGrid, bool>>();

            for (var i = 0; i < args.Count; i += 2)
            {
                if (i + 1 > args.Count)
                    break;

                var arg = args[i];
                var parameter = args[i + 1];

                foreach (var condition in _conditionLookup)
                {
                    if (condition.Command.Equals(arg))
                        conditions.Add(g => condition.Action(g, parameter));
                    else if (condition.InvertCommand.Equals(arg))
                        conditions.Add(g => !condition.Action(g, parameter));
                    else
                    {
                        Context.Respond($"Unknown argument '{arg}'");
                        yield break;
                    }
                }
            }

            foreach (var group in MyCubeGridGroups.Static.Logical.Groups)
            {
                if (group.Nodes.All(grid => conditions.TrueForAll(func => func(grid.NodeData))))
                    foreach (var grid in group.Nodes)
                        yield return grid.NodeData;
            }
        }

        [Condition("name", helpText: "Finds grids with a matching name. Accepts regex format.")]
        private bool NameMatches(MyCubeGrid grid, string str)
        {
            if (string.IsNullOrEmpty(grid.DisplayName))
                return false;

            var regex = new Regex(str);
            return regex.IsMatch(grid.DisplayName);
        }

        [Condition("blockslessthan", helpText: "Finds grids with less than the given number of blocks.")]
        private bool BlocksLessThan(MyCubeGrid grid, string str)
        {
            if (int.TryParse(str, out int count))
                return grid.BlocksCount < count;

            return false;
        }

        [Condition("blocksgreaterthan", helpText: "Finds grids with more than the given number of blocks.")]
        private bool BlocksGreaterThan(MyCubeGrid grid, string str)
        {
            if (int.TryParse(str, out int count))
                return grid.BlocksCount > count;

            return false;
        }

        [Condition("haspower", "nopower", "Finds grids with, or without power.")]
        private bool HasPower(MyCubeGrid grid)
        {
            foreach (var b in grid.GetFatBlocks())
            {
                var c = b.Components?.Get<MyResourceSourceComponent>();
                if (c == null)
                    continue;

                if (c.HasCapacityRemainingByType(MyResourceDistributorComponent.ElectricityId) && c.ProductionEnabledByType(MyResourceDistributorComponent.ElectricityId))
                    return true;
            }

            return false;
        }

        [Condition("insideplanet", helpText: "Finds grids that are trapped inside planets.")]
        private bool InsidePlanet(MyCubeGrid grid)
        {
            var s = grid.PositionComp.WorldVolume;
            var voxels = new List<MyVoxelBase>();
            MyGamePruningStructure.GetAllVoxelMapsInSphere(ref s, voxels);

            if (!voxels.Any())
                return false;

            foreach (var v in voxels)
            {
                var planet = v as MyPlanet;
                if (planet == null)
                    continue;

                var dist2center = Vector3D.DistanceSquared(s.Center, planet.PositionComp.WorldVolume.Center);
                if (dist2center <= (planet.MaximumRadius * planet.MaximumRadius) / 2)
                    return true;
            }

            return false;
        }

        [Condition("playerdistancelessthan", "playerdistancegreaterthan", "Finds grids that are further than the given distance from players.")]
        private bool PlayerDistanceLessThan(MyCubeGrid grid, string str)
        {
            double dist;
            if (!double.TryParse(str, out dist))
            {
                Context.Respond("Couldn't parse distance");
                return false;
            }
            dist *= dist;
            foreach (var player in MySession.Static.Players.GetOnlinePlayers())
            {
                if (Vector3D.DistanceSquared(player.GetPosition(), grid.PositionComp.GetPosition()) < dist)
                    return true;
            }
            return false;
        }

        [Condition("ownedby", helpText: "Finds grids owned by the given player. Can specify player name, IdentityId, 'nobody', or 'pirates'.")]
        private bool OwnedBy(MyCubeGrid grid, string str)
        {
            long identityId;

            if (string.Compare(str, "nobody", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return grid.BigOwners.Count == 0;
            }

            if (string.Compare(str, "pirates", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                identityId = MyPirateAntennas.GetPiratesId();
            }
            else
            {
                var player = Utilities.GetPlayerByNameOrId(str);
                if (player == null)
                    return false;

                identityId = player.IdentityId;
            }

            return grid.BigOwners.Contains(identityId);
        }

        private class Condition
        {
            public string Command;
            public string InvertCommand;
            public string HelpText;
            public Func<MyCubeGrid, string, bool> Action;
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        private sealed class ConditionAttribute : Attribute
        {
            public string Command;
            public string InvertCommand;
            public string HelpText;

            public ConditionAttribute(string command, string invertCommand = null, string helpText = null)
            {
                Command = command;
                InvertCommand = invertCommand;
                HelpText = helpText;
            }
        }
    }
}
