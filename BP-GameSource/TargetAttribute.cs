using System;

namespace BrokeProtocol.GameSource
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false , Inherited = false)]
    public class TargetAttribute : Attribute
    {
        public TargetAttribute(Type enumType, int target)
        {
            EnumType = enumType;
            Target = target;
        }

        public Type EnumType { get; }

        public int Target { get; }
    }
}
