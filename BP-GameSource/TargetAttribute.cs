using System;
using BrokeProtocol.API;

namespace BrokeProtocol.GameSource
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false , Inherited = false)]
    public class TargetAttribute : Attribute
    {
        public GameSourceEvent Event { get; }

        public TargetAttribute(GameSourceEvent eventEnum)
        {
            Event = eventEnum;
        }
    }
}
