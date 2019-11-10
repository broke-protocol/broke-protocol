using BrokeProtocol.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace BrokeProtocol.GameSource
{
    public class Core : Resource
    {
        public Core()
        {
            Info = new ResourceInfo("GameSource", "game")
            {
                Description = "Default game source used by BP. May be modified.",
                Git = "https://github.com/broke-protocol/source"
            };
            RegisterEvents();
        }

        public List<object> Instances { get; } = new List<object>();

        public Enum GetEnumType(Type enumType, int id)
        {
            return (Enum)Enum.ToObject(enumType, id);
        }

        public void RegisterEvents()
        {
            var methods = GetType().Assembly.GetTypes()
                      .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                      .Where(m => m.GetCustomAttributes(typeof(TargetAttribute), false).Length > 0);

            foreach (var method in methods)
            {
                object instance;
                if (Instances.Contains(method.DeclaringType))
                {
                    instance = method.DeclaringType;
                }
                else
                {
                    instance = Activator.CreateInstance(method.DeclaringType);
                    Instances.Add(instance);
                }
                var target = (TargetAttribute)method.GetCustomAttributes(typeof(TargetAttribute), false)[0];
                var types = method.GetParameters().Select(p => p.ParameterType);
                GameSourceHandler.Add(GetEnumType(target.EnumType, target.Target),
                    Delegate.CreateDelegate(Expression.GetActionType(types.ToArray()), instance, method.Name));
            }
        }
    }
}
