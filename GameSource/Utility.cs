using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using UnityEngine;

namespace BrokeProtocol.GameSource
{
    public static class Utility
    {
        public static readonly Vector2 defaultAnchor = new Vector2(0.5f, 0.15f);

        public const float slowSpeedSqr = 6f * 6f;

        public const string adminPermission = "admin";
        public const string allPermission = "all";

        public static GameSourcePlayer GamePlayer(this ShPlayer player) => Manager.pluginPlayers[player];

        public static LimitQueue<ShPlayer> chatted = new(8, 20f);

        public static LimitQueue<string> accountWipe = new(0, 5f);

        public static LimitQueue<ShPlayer> healed = new(3, 60f);

        public static LimitQueue<ShPlayer> unstuck = new(1, 45f);
    }
}
