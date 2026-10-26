using System;
using System.Collections.Generic;
using System.Reflection;

namespace AdvancedHeightMap.API;


public static class AdvanedHeightMapInfo
{
    /// <summary>
    ///     AdvancedHeightMap BepInEx GUID for soft dependencies.
    /// </summary>
    public const string PluginGUID = "Searica.Valheim.AdvancedHeightMap";
}

public static class AdvancedHeightMapAPI
{
    /// <summary>
    /// True when AdvancedHeightMap is installed. Decided the first time any member of
    /// this class is used, so do not touch it before the mod has loaded (see the soft
    /// dependency above).
    /// </summary>
    public static bool IsAvailable => APIReceiver != null;


    /// <summary>The API version the installed verion of AdvancedHeightMap provides, 0 when it is not installed.</summary>
    public static int ReceiverApiVersion => getApiVersion != null ? getApiVersion() : 0;


    private const string ReceiverTypeName = "AdvancedHeightMap.API.APIReceiver";
    private const string ReceiverAssemblyName = "AdvancedHeightMap";


    private static readonly Type APIReceiver;
    private static readonly List<string> Unbound = new List<string>();

    private static readonly Func<int> getApiVersion;
    private static readonly Func<float> getMaxHeightChange;
    private static readonly Func<float> getMinHeightChange;

    static AdvancedHeightMapAPI()
    {
        APIReceiver = FindReceiver();
        if (APIReceiver == null) { return; }

        getApiVersion = Bind<Func<int>>("GetApiVersion");
        getMaxHeightChange = Bind<Func<float>>("GetMaxHeightChange");
        getMinHeightChange = Bind<Func<float>>("GetMinHeightChange");
    }

    private static Type FindReceiver()
    {
        Type type = null;
        try
        {
            type = Type.GetType(ReceiverTypeName + ", " + ReceiverAssemblyName, false);
        }
        catch (Exception)
        {
            // Some runtimes still throw while resolving the assembly; the scan below covers it.
        }

        if (type != null) { return type; }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != ReceiverAssemblyName) { continue; }

            type = assembly.GetType(ReceiverTypeName, false);
            if (type != null) { return type; }
        }

        return null;
    }

    // Binds by name and by the delegate's exact signature, once. A member the receiver lacks,
    // or has with another shape, stays null and is listed in UnboundMembers.
    private static T Bind<T>(string name) where T : class
    {
        MethodInfo invoke = typeof(T).GetMethod("Invoke");
        ParameterInfo[] parameters = invoke.GetParameters();
        Type[] types = new Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++) { types[i] = parameters[i].ParameterType; }

        MethodInfo method = APIReceiver.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, types, null);
        T bound = method != null && method.ReturnType == invoke.ReturnType
            ? Delegate.CreateDelegate(typeof(T), method, false) as T
            : null;

        if (bound == null) { Unbound.Add(name); }
        return bound;
    }

    /// <summary>
    ///     Returns the value of the maximum allowable increase in terrain height from AdvancedHeightMap if
    ///     it is installed, otherwise returns the default vanilla value of +8f.
    /// </summary>
    /// <returns></returns>
    public static float GetMaxHeightChange()
    {
        return getMaxHeightChange != null ? getMaxHeightChange() : 8f;
    }


    /// <summary>
    ///     Returns the value of the maximum allowable decrease in terrain height from AdvancedHeightMap if
    ///     it is installed, otherwise returns the default vanilla value of -8f.
    /// </summary>
    /// <returns></returns>
    public static float GetMinHeightChange()
    {
        return getMinHeightChange != null ? getMinHeightChange() : -8f;
    }
}
