using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace Prediction
{
    internal class Utils
    {
        public static List<Vector2> GetWaypoints(Obj_AI_Base unit)
        {
            var result = new List<Vector2>();

            result.Add(Geometry.To2D(unit.ServerPosition));

            foreach (var point in unit.Path)
                result.Add(Geometry.To2D(point));

            return result;
        }

        public static float GetPathLength(List<Vector2> path)
        {
            var result = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                result += Vector2.Distance(path[i], path[i + 1]);
            }
            return result;
        }

        public static List<Vector2> CutPath(List<Vector2> path, float Distance, float Width)
        {
            var ElongedPath = new List<Vector2>();
            var result = new List<Vector2>();
            var Direction = path[1] - path[0];
            Direction.Normalize();

            ElongedPath.AddRange(path);

            Direction = path[path.Count - 1] - path[path.Count - 2];
            Direction.Normalize();

            ElongedPath.Add(path[path.Count - 1] + Direction * Width);

            for (int i = 0; i < ElongedPath.Count - 1; i++)
            {
                var Dist = Vector2.Distance(ElongedPath[i], ElongedPath[i + 1]);

                if (Dist > Distance)
                {
                    Direction = ElongedPath[i + 1] - ElongedPath[i];
                    Direction.Normalize();

                    var FirstPoint = ElongedPath[i] + Direction * Distance;
                    result.Add(FirstPoint);

                    for (int j = i + 1; j < ElongedPath.Count; j++)
                        result.Add(ElongedPath[j]);

                    return result;
                }
                else
                {
                    Distance -= Dist;
                }
            }

            return ElongedPath;
        }
    }
}
