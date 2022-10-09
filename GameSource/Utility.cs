using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;


namespace BrokeProtocol.GameSource
{
    public static class Utility
    {
        public const float slowSpeedSqr = 6f * 6f;

        public const string adminPermission = "admin";
        public const string allPermission = "all";

        public static GameSourcePlayer GamePlayer(this ShPlayer player) => Manager.pluginPlayers[player];

        public static LimitQueue<ShPlayer> chatted = new LimitQueue<ShPlayer>(8, 20f);

        public static LimitQueue<string> tryRegister = new LimitQueue<string>(0, 5f);

        public static LimitQueue<ShPlayer> healed = new(3, 60f);

        public static LimitQueue<ShPlayer> unstuck = new(1, 45f);
    }
}
