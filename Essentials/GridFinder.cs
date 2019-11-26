using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace Essentials
{
    public class GridFinder 
    {
        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(string gridName) 
        {
            var groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();

            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group => 
            {
                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes) 
                {
                    var grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindLookAtGridGroupMechanical(IMyCharacter controlledEntity) 
        {
            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups) 
            {
                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes) 
                {
                    IMyCubeGrid cubeGrid = groupNodes.NodeData;

                    if (cubeGrid == null || cubeGrid.Physics == null)
                        continue;

                    // check if the ray comes anywhere near the Grid before continuing.    
                    if (!ray.Intersects(cubeGrid.WorldAABB).HasValue)
                        continue;

                    Vector3I? hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                    if (!hit.HasValue)
                        continue;

                    double distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();

                    if (list.TryGetValue(group, out double oldDistance)) 
                    {
                        if (distance < oldDistance) 
                        {
                            list.Remove(group);
                            list.Add(group, distance);
                        }
                    } 
                    else 
                    {
                        list.Add(group, distance);
                    }
                }
            }

            var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();

            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }
    }
}
