using BrokeProtocol.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace BrokeProtocol.GameSource
{
    public class Core : Plugin
    {
        public Core()
        {
            Info = new PluginInfo("GameSource", "game")
            {
                Description = "Default game source used by BP. May be modified.",
                Website = "https://github.com/broke-protocol/source"
            };
            RegisterEvents();
        }

        public List<object> Instances { get; } = new List<object>();

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

                var target = method.GetCustomAttribute<TargetAttribute>();
                var types = method.GetParameters().Select(p => p.ParameterType);
                GameSourceHandler.Add(
                    (Enum)Enum.ToObject(target.EnumType, target.Target),
                    Delegate.CreateDelegate(Expression.GetActionType(types.ToArray()), instance, method.Name));
            }
        }
    }
}
