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
                var localPosition = new Vector3(Random.value - offset, 0f, Random.value - offset);
                position = Util.SafePosition(t.TransformPoint(localPosition), 100f);
                rotation = (-position).SafeLookRotation(Vector3.up);
                place = territory.GetPlace;
                return true;
            }

            position = default;
            rotation = default;
            place = default;
            return false;
        }
    }
}
