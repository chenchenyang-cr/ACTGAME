using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CombatEditor
{
    // Keep a filename-matching compatibility type for Unity script binding.
    [Obsolete("Use AbilityEventObj_Method or AbilityEventObj_MethodContinuous instead.")]
    public class AbilityEventObj_CallMethod : AbilityEventObj_Method
    {
    }

    internal static class AbilityMethodInvoker
    {
        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();

        public static bool InvokeOnCharacter(CombatController controller, string scriptTypeName, string methodName, float normalizedTime)
        {
            if (controller == null || controller._animator == null || string.IsNullOrWhiteSpace(methodName))
            {
                return false;
            }

            MonoBehaviour[] components = GetCharacterComponents(controller);
            bool invoked = false;
            string requiredScript = scriptTypeName == null ? string.Empty : scriptTypeName.Trim();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(requiredScript))
                {
                    Type componentType = component.GetType();
                    if (!string.Equals(componentType.Name, requiredScript, StringComparison.Ordinal) &&
                        !string.Equals(componentType.FullName, requiredScript, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                MethodInfo method = ResolvePublicMethod(component.GetType(), methodName);
                if (method == null)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                {
                    method.Invoke(component, null);
                    invoked = true;
                    continue;
                }

                method.Invoke(component, new object[] { normalizedTime });
                invoked = true;
            }

            return invoked;
        }

        private static MonoBehaviour[] GetCharacterComponents(CombatController controller)
        {
            if (controller == null)
            {
                return Array.Empty<MonoBehaviour>();
            }

            return controller.GetComponentsInChildren<MonoBehaviour>(true);
        }

        private static MethodInfo ResolvePublicMethod(Type type, string methodName)
        {
            string cacheKey = type.FullName + "|" + methodName;
            if (MethodCache.TryGetValue(cacheKey, out MethodInfo cachedMethod))
            {
                return cachedMethod;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            MethodInfo target = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                {
                    target = method;
                    break;
                }

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(float))
                {
                    target = method;
                    break;
                }
            }

            MethodCache[cacheKey] = target;
            return target;
        }
    }

    public class AbilityEventEffect_Method : AbilityEventEffect
    {
        private readonly HashSet<string> _warnedMethods = new HashSet<string>();
        private AbilityEventObj_Method EventObj => (AbilityEventObj_Method)_EventObj;

        public AbilityEventEffect_Method(AbilityEventObj obj) : base(obj)
        {
            _EventObj = obj;
        }

        public override void StartEffect()
        {
            base.StartEffect();
            TryInvoke(eve.EventTime);
        }

        private void TryInvoke(float normalizedTime)
        {
            bool invoked = AbilityMethodInvoker.InvokeOnCharacter(_combatController, EventObj.ScriptTypeName, EventObj.MethodName, normalizedTime);
            if (!invoked && EventObj.LogMissingMethod && !string.IsNullOrWhiteSpace(EventObj.MethodName))
            {
                if (_warnedMethods.Add(EventObj.MethodName))
                {
                    Debug.LogWarning($"[Method] Method \"{EventObj.MethodName}\" not found on current character public methods.");
                }
            }
        }
    }

    public class AbilityEventEffect_MethodContinuous : AbilityEventEffect
    {
        private readonly HashSet<string> _warnedMethods = new HashSet<string>();
        private AbilityEventObj_MethodContinuous EventObj => (AbilityEventObj_MethodContinuous)_EventObj;

        public AbilityEventEffect_MethodContinuous(AbilityEventObj obj) : base(obj)
        {
            _EventObj = obj;
        }

        public override void EffectRunning(float currentTimePercentage)
        {
            base.EffectRunning(currentTimePercentage);
            TryInvoke(currentTimePercentage);
        }

        private void TryInvoke(float normalizedTime)
        {
            bool invoked = AbilityMethodInvoker.InvokeOnCharacter(_combatController, EventObj.ScriptTypeName, EventObj.MethodName, normalizedTime);
            if (!invoked && EventObj.LogMissingMethod && !string.IsNullOrWhiteSpace(EventObj.MethodName))
            {
                if (_warnedMethods.Add(EventObj.MethodName))
                {
                    Debug.LogWarning($"[MethodContinuous] Method \"{EventObj.MethodName}\" not found on current character public methods.");
                }
            }
        }
    }
}
