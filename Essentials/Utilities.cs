using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
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

            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent.DisplayName == nameOrId)
                {
                    entity = ent;
                    return true;
                }
            }

            entity = null;
            return false;
        }

        public static IMyPlayer GetPlayerByNameOrId(string nameOrSteamId)
        {
            if (ulong.TryParse(nameOrSteamId, out ulong id))
                return TorchBase.Instance.Multiplayer.GetPlayerBySteamId(id);

            return TorchBase.Instance.Multiplayer.GetPlayerByName(nameOrSteamId);
        }
    }
}
