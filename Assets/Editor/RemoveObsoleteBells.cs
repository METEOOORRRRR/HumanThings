using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class RemoveObsoleteBells
    {
        public static void Run()
        {
            int removed = 0;
            foreach (var location in HumanContent.Load().locations)
            {
                var path = AssetDatabase.GetAssetPath(location.prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var bells = root.GetComponentsInChildren<WorldBehaviorAnchor>(true)
                        .Where(a => a.tagId == "CounterBell").ToArray();
                    foreach (var bell in bells) Object.DestroyImmediate(bell.gameObject);
                    if (bells.Length > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    removed += bells.Length;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BELL_REMOVAL_PASS removed=" + removed);
        }
    }
}
