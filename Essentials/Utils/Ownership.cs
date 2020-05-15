using Sandbox.Game.Entities;
using Sandbox.Game.World;

namespace Essentials.Utils
{
    public class Ownership
    {
        /// <summary>
        /// Get owner of the Grid.
        /// </summary>
        /// <param name="grid"></param>
        /// <returns>Id of the owner</returns>
        public static long GetOwner(MyCubeGrid grid)
        {
            if (grid.BigOwners.Count > 0 && grid.BigOwners[0] != 0)
                return grid.BigOwners[0];
            else if (grid.BigOwners.Count > 1)
                return grid.BigOwners[1];
            else
                return 0L;
        }

        /// <summary>
        /// Gets the type of the owner (NPC, Player or Nobody).
        /// </summary>
        /// <param name="grid"></param>
        public static OwnerType GetOwnerType(MyCubeGrid grid)
        {
            // Get the owner id of the grid.
            var ownerId = GetOwner(grid);

            // Return the right owner type.
            if (ownerId == 0L)
                return OwnerType.Nobody;
            else if (MySession.Static.Players.IdentityIsNpc(ownerId))
                return OwnerType.NPC;
            else
                return OwnerType.Player;
        }

        /// <summary>
        /// OwnerType Enumerator.
        /// </summary>
        public enum OwnerType
        {
            Nobody,
            NPC,
            Player
        }
    }
}
