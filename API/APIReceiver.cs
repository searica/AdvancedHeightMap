
namespace AdvancedHeightMap.API;

public static class APIReceiver
{
    internal const int ApiVersion = 1;

    public static int GetApiVersion() => ApiVersion;

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
