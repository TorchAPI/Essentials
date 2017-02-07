using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Torch;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Essentials
{
    public static class Utilities
    {
        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            return MyAPIGateway.Entities.TryGetEntityByName(nameOrId, out entity);
        }

        public static IMyPlayer GetPlayerByNameOrId(string nameOrSteamId)
        {
            if (ulong.TryParse(nameOrSteamId, out ulong id))
                return TorchBase.Instance.Multiplayer.GetPlayerBySteamId(id);

            return TorchBase.Instance.Multiplayer.GetPlayerByName(nameOrSteamId);
        }
    }
}
