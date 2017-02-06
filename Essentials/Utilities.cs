using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
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
    }
}
