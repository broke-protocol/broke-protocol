using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility;
using UnityEngine;

namespace BrokeProtocol.WarSource
{
    public static class Utility
    {
        public static bool GetSpawn(out Vector3 position, out Quaternion rotation, out Place place)
        {
            var territory = Manager.territories.GetRandom();

            if (territory)
            {
                var t = territory.mainT;
                const float offset = 0.5f;
                place = territory.GetPlace;

                for (int i = 0; i < 10; i++)
                {
                    var localPosition = new Vector3(Random.value - offset, 0f, Random.value - offset);
                    if(t.TransformPoint(localPosition).SafePosition(out var hit) && Mathf.Abs(hit.point.y - t.position.y) <= 10f)
                    {
                        position = hit.point;
                        rotation = (-position).SafeLookRotation(Vector3.up);
                        return true;
                    }
                }

                position = t.position;
                rotation = t.rotation;
                return true;
            }

            position = default;
            rotation = default;
            place = default;
            return false;
        }
    }
}
