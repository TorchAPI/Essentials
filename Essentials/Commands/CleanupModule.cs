using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;

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

        private IEnumerable<MyCubeGrid> ScanConditions(List<string> args)
        {
            var conditions = new List<Func<MyCubeGrid, bool>>();

            for (var i = 0; i < args.Count; i += 2)
            {
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
                }
            }

            foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
            {
                if (conditions.TrueForAll(func => func(grid)))
                    yield return grid;
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
