using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Utils;
using Jotunn.Extensions;
using Configs;
using Logging;

namespace AdvancedHeightMap;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
[SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
internal sealed class AdvancedHeightMap : BaseUnityPlugin
{
    public const string PluginName = "AdvancedHeightMap";
    internal const string Author = "Searica";
    public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
    public const string PluginVersion = "0.1.0";

    internal static AdvancedHeightMap Instance;
    internal static ConfigFile ConfigFile;
    internal static ConfigFileWatcher ConfigFileWatcher;

    // Global settings
    internal const string MainConfigSection = PluginName;
    internal static ConfigEntry<float> MaxHeightChange;
    internal static ConfigEntry<float> MinHeightChange;
    internal static bool BoundConfigs = false;

    internal static float GetMaxHeightChange()
    {
        return MaxHeightChange.Value;
    }

    internal static float GetMinHeightChange()
    {
        return MinHeightChange.Value;
    }

    public void Awake()
    {
        Instance = this;
        ConfigFile = Config;
        Log.Init(Logger);

        Config.DisableSaveOnConfigSet();
        SetUpConfigEntries();
        Config.Save();
        Config.SaveOnConfigSet = true;

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);
        Game.isModded = true;

        ConfigFileWatcher = new(Config);  // check for changes to the config file
    }

    internal void SetUpConfigEntries()
    {
        Log.Verbosity = Config.BindConfigInOrder(
            MainConfigSection,
            "Verbosity",
            Log.InfoLevel.Low,
            "Low will log basic information about the mod. Medium will log information that " +
            "is useful for troubleshooting. High will log a lot of information, do not set " +
            "it to this without good reason as it will slow Down your game.",
            synced: false,
            sectionOrder: false
        );

        MaxHeightChange = Config.BindConfigInOrder(
            MainConfigSection, 
            nameof(MaxHeightChange), 
            200f, 
            "How high you can raise terrain up relative to the heightmap's original position. Vanilla default is 8",  
            acceptableValues: new AcceptableValueRange<float>(1f, 200f),
            synced: true,
            sectionOrder: false
        );

        MinHeightChange = Config.BindConfigInOrder(
            MainConfigSection,
            nameof(MinHeightChange),
            -200f,
            "How far you can dig down relative to the heightmap's original position. Vanilla default is -8",
            acceptableValues: new AcceptableValueRange<float>(-200f, -1f),
            synced: true,
            sectionOrder: false
        );

        BoundConfigs = true;
    }

    public void OnDestroy()
    {
        Config.Save();
    }
}

public static class AdvancedHeightMapAPI
{
    public static float GetMaxHeightChange()
    {
        if (AdvancedHeightMap.BoundConfigs)
        {
            return AdvancedHeightMap.MaxHeightChange.Value;
        }
        return 8f;
    }

    public static float GetMinHeightChange()
    {
        if (AdvancedHeightMap.BoundConfigs)
        {
            return AdvancedHeightMap.MinHeightChange.Value;
        }
        return 8f;
    }
}



