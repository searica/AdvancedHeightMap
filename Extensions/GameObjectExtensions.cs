using UnityEngine;

namespace AdvancedHeightMap.Extensions;
internal static class GameObjectExtensions
{

    public static bool IsRootPrefab(this GameObject go)
    {
        return !go.transform.parent;
    }
}
