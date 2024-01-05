using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Helpers;

namespace CelesteBot_2023;

internal static class ModUtils
{
    public static readonly Assembly VanillaAssembly = typeof(Player).Assembly;

    public static Type GetType(string modName, string name, bool throwOnError = false, bool ignoreCase = false)
    {
        return GetAssembly(modName)?.GetType(name, throwOnError, ignoreCase);
    }

    public static Type GetType(string name, bool throwOnError = false, bool ignoreCase = false)
    {
        return FakeAssembly.GetFakeEntryAssembly().GetType(name, throwOnError, ignoreCase);
    }

    public static Type[] GetTypes()
    {
        return FakeAssembly.GetFakeEntryAssembly().GetTypes();
    }

    public static EverestModule GetModule(string modName)
    {
        return Everest.Modules.FirstOrDefault(module => module.Metadata?.Name == modName);
    }

    public static bool IsInstalled(string modName)
    {
        return GetModule(modName) != null;
    }

    public static Assembly GetAssembly(string modName)
    {
        return GetModule(modName)?.GetType().Assembly;
    }
}
internal static class AttributeUtils
{
    private static readonly object[] Parameterless = { };
    private static readonly IDictionary<Type, IEnumerable<MethodInfo>> MethodInfos = new Dictionary<Type, IEnumerable<MethodInfo>>();

    public static void CollectMethods<T>() where T : Attribute
    {
        MethodInfos[typeof(T)] = typeof(AttributeUtils).Assembly.GetTypesSafe().SelectMany(type => type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(info => info.GetParameters().Length == 0 && info.GetCustomAttribute<T>() != null));
    }

    public static void Invoke<T>() where T : Attribute
    {
        if (MethodInfos.TryGetValue(typeof(T), out IEnumerable<MethodInfo> methodInfos))
        {
            foreach (MethodInfo methodInfo in methodInfos)
            {
                methodInfo.Invoke(null, Parameterless);
            }
        }
    }
}