using BrokeProtocol.Collections;
using BrokeProtocol.Entities;


namespace BrokeProtocol.GameSource
{
    public static class Utility
    {
        public static LimitQueue<ShPlayer> chatted = new LimitQueue<ShPlayer>(8, 20f);

        public static LimitQueue<ShPlayer> trySell = new LimitQueue<ShPlayer>(0, 5f);

        public static LimitQueue<string> tryRegister = new LimitQueue<string>(0, 5f);
    }
}
