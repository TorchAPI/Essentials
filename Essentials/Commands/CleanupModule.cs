using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;
using VRage.Game.Entity;

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
                grid.Close();
                count++;
            }

            Context.Respond($"Deleted {count} grids matching the given conditions.");
        }

        private IEnumerable<MyCubeGrid> ScanConditions(IReadOnlyList<string> args)
        {
            var conditions = new List<Func<MyCubeGrid, bool>>();

            for (var i = 0; i < args.Count; i += 2)
            {
                if (i + 1 > args.Count)
                    break;

                var arg = args[i];
                var parameter = args[i + 1];

                switch (arg)
                {
                    case "hastype":
                        conditions.Add(g => g.HasBlockType(parameter));
                        break;
                    case "notype":
                        conditions.Add(g => !g.HasBlockType(parameter));
                        break;
                    case "hassubtype":
                        conditions.Add(g => g.HasBlockSubtype(parameter));
                        break;
                    case "nosubtype":
                        conditions.Add(g => !g.HasBlockSubtype(parameter));
                        break;
                    case "blockslessthan":
                        conditions.Add(g => BlocksLessThan(g, parameter));
                        break;
                    case "blocksgreaterthan":
                        conditions.Add(g => BlocksGreaterThan(g, parameter));
                        break;
                    case "ownedby":
                        conditions.Add(g => OwnedBy(g, parameter));
                        break;
                    case "matches":
                        conditions.Add(g => NameMatches(g, parameter));
                        break;
                    default:
                        Context.Respond($"Unknown argument '{arg}'");
                        yield break;
                }
            }

            foreach (var group in MyCubeGridGroups.Static.Logical.Groups)
            {
                if (group.Nodes.All(grid => conditions.TrueForAll(func => func(grid.NodeData))))
                    foreach (var grid in group.Nodes)
                        yield return grid.NodeData;
            }
        }

        private bool NameMatches(MyCubeGrid grid, string str)
        {
            try
            {
                var regex = new Regex(str);
                return regex.IsMatch(grid.DisplayName);
            } catch( Exception e )
            {
                return false;
            }
        }

        private bool BlocksLessThan(MyCubeGrid grid, string str)
        {
            if (int.TryParse(str, out int count))
                return grid.BlocksCount < count;

            return false;
        }

        private bool BlocksGreaterThan(MyCubeGrid grid, string str)
        {
            if (int.TryParse(str, out int count))
                return grid.BlocksCount > count;

            return false;
        }

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
    }
}
