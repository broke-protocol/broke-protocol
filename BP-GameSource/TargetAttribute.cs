using System;

namespace BrokeProtocol.GameSource
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false , Inherited = false)]
    public class TargetAttribute : Attribute
    {
        public int EventID { get; }

        public TargetAttribute(int eventID)
        {
            EventID = eventID;
        }
    }
}
