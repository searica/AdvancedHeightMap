using System.Collections.Generic;
using System.Linq;
using SoftReferenceableAssets;
using Logging;

namespace AdvancedHeightMap.Extensions;
internal static class AssetBundleLoaderExtensions
{
    private static readonly Dictionary<BundleLoader, HashSet<AssetID>> _bundleLoaderToAssetIDsMap = [];
    private static Dictionary<BundleLoader, HashSet<AssetID>> GetBundleLoaderToAssetIDDependeciesMap()
    {
        if (_bundleLoaderToAssetIDsMap.Count == 0)
        {
            foreach (AssetLoader assetLoader in AssetBundleLoader.Instance.m_assetLoaders)
            {
                var bundleLoader = AssetBundleLoader.Instance.GetBundleLoaderFromAssetID(assetLoader.m_assetID);
                if (!_bundleLoaderToAssetIDsMap.TryGetValue(bundleLoader, out HashSet<AssetID> assetIDs))
                {
                    assetIDs = [];
                    _bundleLoaderToAssetIDsMap[bundleLoader] = assetIDs;
                }
                assetIDs.Add(assetLoader.m_assetID);
            }
        }
        return _bundleLoaderToAssetIDsMap;
    }

    /// <summary>
    ///     Get the BundleLoader for this AssetID by getting the AssetLoader and then using it's bundleLoaderIndex
    /// </summary>
    /// <param name="assetBundleLoader"></param>
    /// <param name="assetID"></param>
    /// <returns></returns>
    internal static BundleLoader GetBundleLoaderFromAssetID(this AssetBundleLoader assetBundleLoader, AssetID assetID)
    {
        var assetLoader = assetBundleLoader.GetLoader(assetID);
        return assetBundleLoader.m_bundleLoaders[assetLoader.m_bundleLoaderIndex];
    }

    internal static HashSet<AssetID> GetDependenciesFromAssetID(this AssetBundleLoader assetBundleLoader, AssetID assetID)
    {
        BundleLoader bundleLoader = assetBundleLoader.GetBundleLoaderFromAssetID(assetID);
        List<AssetID> dependencies = [];
        Dictionary<BundleLoader, HashSet<AssetID>> bundleToDependenciesMap = GetBundleLoaderToAssetIDDependeciesMap();
        foreach (int i in bundleLoader.m_bundleLoaderIndicesOfThisAndDependencies)
        {
            if (bundleToDependenciesMap.TryGetValue(assetBundleLoader.m_bundleLoaders[i], out HashSet<AssetID> assetIds))
            {
                dependencies.AddRange(assetIds);
            }            
        }
        return dependencies.ToHashSet();
    }
}
