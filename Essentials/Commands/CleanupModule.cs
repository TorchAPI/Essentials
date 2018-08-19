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
        private static readonly Logger Log = LogManager.GetLogger("Essentials");
        [Command("scan", "Find grids matching the given conditions")]
        public void Scan()
        {
            var count = ScanConditions(Context.Args).Count();
            Context.Respond($"Found {count} grids matching the given conditions.");
        }

        [Command("list", "Lists grids matching the given conditions")]
        public void List()
        {
            var grids = ScanConditions(Context.Args).OrderBy(g => g.DisplayName).ToList();
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
            foreach (var grid in ScanConditions(Context.Args))
            {
                Log.Info($"Deleting grid: {grid.EntityId}: {grid.DisplayName}");
                EjectPilots(grid);
                grid.Close();
                count++;
            }

            Context.Respond($"Deleted {count} grids matching the given conditions.");
            Log.Info($"Cleanup deleted {count} grids matching conditions {string.Join(", ", Context.Args)}");
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

                var c = new Condition(m, a);

                _conditionLookup.Add(c);
            }
        }

        private List<Condition> _conditionLookup;

        private IEnumerable<MyCubeGrid> ScanConditions(IReadOnlyList<string> args)
        {
            var conditions = new List<Func<MyCubeGrid, bool?>>();
            
            for (var i = 0; i < args.Count; i ++)
            {
                string parameter;
                if (i + 1 >= args.Count)
                {
                    parameter = null;
                }
                else
                {
                    parameter = args[i + 1];
                }

                var arg = args[i];
                

                if (parameter != null)
                {
                    //parameter is the name of a command. Assume this command requires no parameters
                    if (_conditionLookup.Any(c => parameter.Equals(c.Command, StringComparison.CurrentCultureIgnoreCase) || parameter.Equals(c.InvertCommand, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        parameter = null;
                    }
                    //next string is a parameter, so pass it to the condition and skip it next loop
                    else
                        i++;
                }

                bool found = false;

                foreach (var condition in _conditionLookup)
                {
                    if (arg.Equals(condition.Command, StringComparison.CurrentCultureIgnoreCase))
                    {
                        conditions.Add(g => condition.Evaluate(g, parameter, false, this));
                        found = true;
                        break;
                    }
                    else if (arg.Equals(condition.InvertCommand, StringComparison.CurrentCultureIgnoreCase))
                    {
                        conditions.Add(g => condition.Evaluate(g, parameter, true, this));
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Context.Respond($"Unknown argument '{arg}'");
                    yield break;
                }
            }

            //default scan to find grids without pilots
            if(!args.Contains("haspilot", StringComparer.CurrentCultureIgnoreCase))
                conditions.Add(g => !Piloted(g));

            foreach (var group in MyCubeGridGroups.Static.Logical.Groups)
            {
                //if (group.Nodes.All(grid => conditions.TrueForAll(func => func(grid.NodeData))))
                bool res = true;
                foreach (var node in group.Nodes)
                {
                    foreach (var c in conditions)
                    {
                        bool? r = c.Invoke(node.NodeData);
                        if (r == null)
                            yield break;
                        if (r == true)
                            continue;

                        res = false;
                        break;
                    }
                    if (!res)
                        break;
                }

                if(res)
                    foreach (var grid in group.Nodes)
                        yield return grid.NodeData;
            }
        }

        [Condition("name", helpText: "Finds grids with a matching name. Accepts regex format.")]
        public bool NameMatches(MyCubeGrid grid, string str)
        {
            if (string.IsNullOrEmpty(grid.DisplayName))
                return false;

            var regex = new Regex(str);
            return regex.IsMatch(grid.DisplayName);
        }

        [Condition("blockslessthan", helpText: "Finds grids with less than the given number of blocks.")]
        public bool BlocksLessThan(MyCubeGrid grid, int count)
        {
            return grid.BlocksCount < count;
        }

        [Condition("blocksgreaterthan", helpText: "Finds grids with more than the given number of blocks.")]
        public bool BlocksGreaterThan(MyCubeGrid grid, int count)
        {
            return grid.BlocksCount > count;
        }

        [Condition("haspower", "nopower", "Finds grids with, or without power.")]
        public bool HasPower(MyCubeGrid grid)
        {
            foreach (var b in grid.GetFatBlocks())
            {
                var c = b.Components?.Get<MyResourceSourceComponent>();
                if (c == null)
                    continue;

                //some sources don't have electricity and Keen apparently doesn't know what TryGetValue is
                if (!c.ResourceTypes.Contains(MyResourceDistributorComponent.ElectricityId))
                    continue;

                if (c.HasCapacityRemainingByType(MyResourceDistributorComponent.ElectricityId) && c.ProductionEnabledByType(MyResourceDistributorComponent.ElectricityId))
                    return true;
            }

            return false;
        }

        [Condition("insideplanet", helpText: "Finds grids that are trapped inside planets.")]
        public bool InsidePlanet(MyCubeGrid grid)
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
        public bool PlayerDistanceLessThan(MyCubeGrid grid, double dist)
        {
            dist *= dist;
            foreach (var player in MySession.Static.Players.GetOnlinePlayers())
            {
                if (Vector3D.DistanceSquared(player.GetPosition(), grid.PositionComp.GetPosition()) < dist)
                    return true;
            }
            return false;
        }

        [Condition("ownedby", helpText: "Finds grids owned by the given player. Can specify player name, IdentityId, 'nobody', or 'pirates'.")]
        public bool OwnedBy(MyCubeGrid grid, string str)
        {
            long identityId;
            
            String digitsOnly = @"\d+";
            
            try
            {
                if (Regex.IsMatch(str, digitsOnly) == true)
                {
                    long tryID = ToInt64(str);
                    if (grid.BigOwners.Contains(tryID) == true) return grid.BigOwners.Contains(tryID);
                }
            }
            catch {}

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

        [Condition("hastype", "notype", "Finds grids containing blocks of the given type.")]
        public bool BlockType(MyCubeGrid grid, string str)
        {
            return grid.HasBlockType(str);
        }

        [Condition("hassubtype", "nosubtype", "Finds grids containing blocks of the given subtype.")]
        public bool BlockSubType(MyCubeGrid grid, string str)
        {
            return grid.HasBlockSubtype(str);
        }

        [Condition("haspilot", "Finds grids with pilots")]
        public bool Piloted(MyCubeGrid grid)
        {
            return grid.GetFatBlocks().OfType<MyCockpit>().Any(b => b.Pilot != null);
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

        private class Condition
        {
            public string Command;
            public string InvertCommand;
            public string HelpText;
            private MethodInfo _method;
            public readonly ParameterInfo Parameter;

            public Condition(MethodInfo evalMethod, ConditionAttribute attribute)
            {
                Command = attribute.Command;
                InvertCommand = attribute.InvertCommand;
                HelpText = attribute.HelpText;
                _method = evalMethod;
                if(_method.ReturnType!= typeof(bool))
                    throw new TypeLoadException("Condition does not return a bool!");
                var p = _method.GetParameters();
                if (p.Length < 1 || p[0].ParameterType != typeof(MyCubeGrid))
                    throw new TypeLoadException("Condition does not accept MyCubeGrid as first parameter");
                if (p.Length > 2)
                    throw new TypeLoadException("Condition can only have two parameters");
                if (p.Length == 1)
                    Parameter = null;
                else
                    Parameter = p[1];

            }

            public bool? Evaluate(MyCubeGrid grid, string arg, bool invert, CleanupModule module)
            {
                var context = module.Context;
                bool result;
                if (!string.IsNullOrEmpty(arg) && Parameter == null)
                {
                    context.Respond($"Condition does not accept an argument. Cannot continue!");
                    return null;
                }
                if (string.IsNullOrEmpty(arg) && Parameter != null && !Parameter.HasDefaultValue)
                {
                    context.Respond($"Condition requires an argument! {Parameter.ParameterType.Name}: {Parameter.Name} Not supplied, cannot continue!");
                    return null;
                }
                if (Parameter != null && !string.IsNullOrEmpty(arg))
                {
                    if (!arg.TryConvert(Parameter.ParameterType, out object val))
                    {
                        context.Respond($"Could not parse argument!");
                        return null;
                    }

                    result = (bool)_method.Invoke(module, new[] {grid, val});
                }
                else
                {
                    result = (bool)_method.Invoke(module, new object[] {grid});
                }
                
                return result != invert;
            }
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
