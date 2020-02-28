using BrokeProtocol.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

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

            var methods = GetType().Assembly.GetTypes()
                      .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                      .Where(m => m.GetCustomAttribute<TargetAttribute>() != null);

            HashSet<object> instances = new HashSet<object>();

            foreach (var method in methods)
            {
                object instance;
                if (instances.Contains(method.DeclaringType))
                {
                    instance = method.DeclaringType;
                }
                else
                {
                    instance = Activator.CreateInstance(method.DeclaringType);
                    instances.Add(instance);
                }

                var target = method.GetCustomAttribute<TargetAttribute>();
                var types = method.GetParameters().Select(p => p.ParameterType);

                Debug.Log($"[GS] Registering {method.Name} event");
                if (!GameSourceHandler.Add(target.EventID, Delegate.CreateDelegate(Expression.GetActionType(types.ToArray()), instance, method.Name)))
                {
                    Debug.LogWarning($"[GS] Event {target.EventID} added more than once");
                }
            }
        }
    }
}
