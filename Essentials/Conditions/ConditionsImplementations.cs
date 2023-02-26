using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vector3D = VRageMath.Vector3D;

namespace Essentials.Commands
{
    [ConditionModule]
    public static class ConditionsImplementations
    {
        [Condition("name", helpText: "Finds grids with a matching name. Accepts regex format.")]
        public static bool NameMatches(MyCubeGrid grid, string str)
        {
            if (string.IsNullOrEmpty(grid.DisplayName))
                return false;

            var regex = new Regex(str);
            return regex.IsMatch(grid.DisplayName);
        }

        [Condition("blockslessthan", helpText: "Finds grids with less than the given number of blocks.")]
        public static bool BlocksLessThan(MyCubeGrid grid, int count)
        {
            return grid.BlocksCount < count;
        }

        [Condition("pcugreaterthan", helpText: "Finds grids with more than the given number of PCU.")]
        public static bool PCUGreaterThan(MyCubeGrid grid, int pcu)
        {
            return grid.BlocksPCU > pcu;
        }

        [Condition("pculessthan", helpText: "Finds grids with less than the given number of PCU.")]
        public static bool PCULessThan(MyCubeGrid grid, int pcu)
        {
            return grid.BlocksPCU < pcu;
        }

        [Condition("hasgridtype", helpText: "Finds grids with the specified grid type (large | small | ship | static).")]
        public static bool HasGridType(MyCubeGrid grid, string gridType) 
        {
            if (string.IsNullOrEmpty(gridType))
                return false;

            if (string.Compare(gridType, "static", StringComparison.InvariantCultureIgnoreCase) == 0)
                return grid.IsStatic;

            if (string.Compare(gridType, "ship", StringComparison.InvariantCultureIgnoreCase) == 0) 
                return !grid.IsStatic;

            if (string.Compare(gridType, "large", StringComparison.InvariantCultureIgnoreCase) == 0)
                return grid.GridSizeEnum == VRage.Game.MyCubeSize.Large;

            if (string.Compare(gridType, "small", StringComparison.InvariantCultureIgnoreCase) == 0) 
                return grid.GridSizeEnum == VRage.Game.MyCubeSize.Small;

            // In all other cases, just return false.
            return false;
        }

        [Condition("hasownertype", helpText: "Finds grids with the specified owner type (npc | player | nobody).")]
        public static bool HasOwnerType(MyCubeGrid grid, string ownerType)
        {
            if (string.IsNullOrEmpty(ownerType))
                return false;

            // Get the owner type of the grid.
            var gridOwnerType = Utils.Ownership.GetOwnerType(grid);

            // Check provided input string.
            switch (ownerType.ToLower().Trim())
            {
                // Check if grid is owner by an NPC.
                case "npc":
                case "npcs":
                    return gridOwnerType == Utils.Ownership.OwnerType.NPC;

                // Check if the grid is owned by a Player.
                case "player":
                case "players":
                    return gridOwnerType == Utils.Ownership.OwnerType.Player;

                // Check if the grid is owned by Nobody.
                case "nobody":
                    return gridOwnerType == Utils.Ownership.OwnerType.Nobody;
            }

            // In all other cases, just return false.
            return false;
        }

        [Condition("blocksgreaterthan", helpText: "Finds grids with more than the given number of blocks.")]
        public static bool BlocksGreaterThan(MyCubeGrid grid, int count)
        {
            return grid.BlocksCount > count;
        }

        [Condition("haspower", "nopower", "Finds grids with, or without power.")]
        public static bool HasPower(MyCubeGrid grid)
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
        public static bool InsidePlanet(MyCubeGrid grid)
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
        public static bool PlayerDistanceLessThan(MyCubeGrid grid, double dist)
        {
            dist *= dist;
            foreach (var player in MySession.Static.Players.GetOnlinePlayers())
            {
                if (Vector3D.DistanceSquared(player.GetPosition(), grid.PositionComp.GetPosition()) < dist)
                    return true;
            }
            return false;
        }

        [Condition("centerdistancelessthan", "centerdistancegreaterthan", "Finds grids that are further than the given distance from center.")]
        public static bool CenterDistanceLessThan(MyCubeGrid grid, double dist)
        {
            dist *= dist;
            
            return Vector3D.DistanceSquared(Vector3D.Zero, grid.PositionComp.GetPosition()) < dist;
        }

        [Condition("ownedby", helpText: "Finds grids owned by the given player. Can specify player name, IdentityId, 'nobody', or 'pirates'.")]
        public static bool OwnedBy(MyCubeGrid grid, string str)
        {
            long identityId;

            if (string.Compare(str, "nobody", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return grid.BigOwners.Count == 0;
            }

            if (string.Compare(str, "npc", StringComparison.Ordinal) == 0)
            {
                return grid.BigOwners.Count > 0 &&
                       MySession.Static.Factions.IsNpcFaction(grid.BigOwners.FirstOrDefault());
            }
            

            if (string.Compare(str, "pirates", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                identityId = MyPirateAntennas.GetPiratesId();
                var pirateFaction = MySession.Static.Factions.GetPlayerFaction(identityId);
                if (pirateFaction != null && pirateFaction.Members.Count > 1)
                {
                    return grid.BigOwners.Count > 0 &&
                           pirateFaction.Members.ContainsKey(grid.BigOwners.FirstOrDefault());
                }

            }
            else
            {
                var player = Utilities.GetIdentityByNameOrIds(str);
                if (player == null)
                {
                    if (long.TryParse(str, out long NPCId))
                    {
                        if (MySession.Static.Players.IdentityIsNpc(NPCId))
                        {
                            return grid.BigOwners.Contains(NPCId);
                        }
                    }
                    return false;
                }
                identityId = player.IdentityId;
            }

            return grid.BigOwners.Contains(identityId);
        }
        

        [Condition("hastype", "notype", "Finds grids containing blocks of the given type.")]
        public static bool BlockType(MyCubeGrid grid, string str)
        {
            return grid.HasBlockType(str);
        }

        [Condition("hastype-fast", "notype-fast", "Finds grids containing blocks of the given type.")]
        public static bool BlockTypeFast(MyCubeGrid grid, string str)
        {
            return grid.HasBlockTypeFast(str);
        }
        
        [Condition("hassubtype", "nosubtype", "Finds grids containing blocks of the given subtype.")]
        public static bool BlockSubType(MyCubeGrid grid, string str)
        {
            return grid.HasBlockSubtype(str);
        }
        
        [Condition("hassubtype-fast", "nosubtype-fast", "Finds grids containing blocks of the given subtype.")]
        public static bool BlockSubTypeFast(MyCubeGrid grid, string str)
        {
            return grid.HasBlockSubtypeFast(str);
        }

        [Condition("haspilot", "Finds grids with pilots")]
        public static bool Piloted(MyCubeGrid grid)
        {
            return grid.GetFatBlocks().OfType<MyShipController>().Any(b => b.Pilot != null);
        }
    }
}