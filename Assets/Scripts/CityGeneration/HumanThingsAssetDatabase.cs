using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EarthRecovery
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "EarthRecovery.Editor", "EarthRecovery.Editor", "HumanThingsAssetDatabase")]
    [CreateAssetMenu(menuName = "HumanThings/Prefab Asset Database", fileName = "CityAssetDatabase")]
    public sealed class HumanThingsAssetDatabase : ScriptableObject
    {
        public int schemaVersion = 2;
        public string sourceFolderGuid = "";
        public string sourceFolderPath = "";
        public List<HumanThingsAssetEntry> entries = new();

        public IEnumerable<HumanThingsAssetEntry> FindAssets(AssetCategory? category = null, string subCategory = null,
            IEnumerable<string> tags = null, PlacementSurface? placementSurface = null)
        {
            var requested = (tags ?? System.Array.Empty<string>()).Select(HumanThingsAssetClassifier.Normalize).Where(t => t.Length > 0).ToArray();
            return (entries ?? new List<HumanThingsAssetEntry>()).Where(e => e != null
                && (!category.HasValue || e.category == category)
                && (string.IsNullOrWhiteSpace(subCategory) || string.Equals(e.subCategory, subCategory, System.StringComparison.OrdinalIgnoreCase))
                && (!placementSurface.HasValue || e.placementSurface == placementSurface)
                && requested.All(t => (e.tags ?? new List<string>()).Any(v => HumanThingsAssetClassifier.Normalize(v) == t)))
                .OrderBy(e => e.assetGuid, System.StringComparer.Ordinal).ThenBy(e => e.assetPath, System.StringComparer.Ordinal);
        }

        public IEnumerable<HumanThingsAssetEntry> Search(string query, AssetCategory? category = null)
        {
            var words = HumanThingsAssetClassifier.Normalize(query).Split(' ').Where(w => w.Length > 0).ToArray();
            return (entries ?? new List<HumanThingsAssetEntry>()).Where(e => e != null && (!category.HasValue || e.category == category))
                .Where(e =>
                {
                    var text = HumanThingsAssetClassifier.Normalize(string.Join(" ", e.displayName, e.assetPath, e.category.ToString(), e.subCategory,
                        string.Join(" ", e.tags ?? new List<string>())));
                    return words.All(word => text.Contains(word));
                });
        }
    }
}
