using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class HumanThingsAssetScanner
    {
        public static bool ValidFolder(string path) => !string.IsNullOrEmpty(path)
            && !path.Contains('\\') && !path.Split('/').Any(p => p == "." || p == "..")
            && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)) && AssetDatabase.IsValidFolder(path);

        public static void ScanFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Argument(string key)
            {
                int i = Array.IndexOf(args, key);
                if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing argument: " + key);
                return args[i + 1];
            }
            string folder = Argument("-humanThingsScanFolder"), path = Argument("-humanThingsDatabase");
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal)
                || path.Contains('\\') || path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0))
                throw new ArgumentException("Database path must be an .asset under Assets");
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (main != null && !(main is HumanThingsAssetDatabase)) throw new ArgumentException("Database path already contains another asset");
            var db = main as HumanThingsAssetDatabase;
            var entries = Scan(folder, db != null ? db.entries : null);
            if (db == null)
            {
                string parent = "Assets";
                foreach (var segment in path.Split('/').Skip(1).SkipLast(1))
                {
                    if (!AssetDatabase.IsValidFolder(parent + "/" + segment)) AssetDatabase.CreateFolder(parent, segment);
                    parent += "/" + segment;
                }
                db = ScriptableObject.CreateInstance<HumanThingsAssetDatabase>(); AssetDatabase.CreateAsset(db, path);
            }
            db.entries = entries; db.schemaVersion = 2; db.sourceFolderPath = folder; db.sourceFolderGuid = AssetDatabase.AssetPathToGUID(folder);
            EditorUtility.SetDirty(db); AssetDatabase.SaveAssetIfDirty(db);
            int missingMaterials = entries.Sum(e => e.prefab.GetComponentsInChildren<Renderer>(true).Sum(r => r.sharedMaterials.Count(m => m == null || m.shader == null)));
            Debug.Log("ASSET_SCAN_PASS count=" + entries.Count + " unique=" + entries.Select(e => e.assetGuid).Distinct().Count() + " missingMaterialReferences=" + missingMaterials + " database=" + path);
            foreach (AssetCategory category in Enum.GetValues(typeof(AssetCategory))) Debug.Log("ASSET_SCAN_CATEGORY " + category + "=" + entries.Count(e => e.category == category));
            foreach (var entry in entries.Where(e => e.category == AssetCategory.Unknown)) Debug.Log("ASSET_SCAN_UNKNOWN " + entry.assetPath);
        }

        public static List<HumanThingsAssetEntry> Scan(string folder, IEnumerable<HumanThingsAssetEntry> previous = null, Func<int, int, string, bool> cancel = null)
        {
            if (!ValidFolder(folder)) throw new ArgumentException("Select a project folder under Assets.", nameof(folder));
            var existing = (previous ?? Array.Empty<HumanThingsAssetEntry>()).Where(e => e != null && !string.IsNullOrEmpty(e.assetGuid))
                .GroupBy(e => e.assetGuid).ToDictionary(g => g.Key, g => g.First());
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Distinct()
                .Select(g => (guid: g, path: AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a.path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.path, StringComparer.Ordinal).ToArray();
            var result = new List<HumanThingsAssetEntry>();
            for (int i = 0; i < paths.Length; i++)
            {
                var asset = paths[i];
                if (cancel?.Invoke(i, paths.Length, asset.path) == true) throw new OperationCanceledException();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.path);
                if (prefab == null) throw new InvalidOperationException("Could not load prefab: " + asset.path);
                var entry = existing.TryGetValue(asset.guid, out var saved) ? saved.Copy() : HumanThingsAssetClassifier.Classify(prefab.name, asset.path);
                entry.assetGuid = asset.guid; entry.assetPath = asset.path; entry.displayName = prefab.name; entry.prefab = prefab;
                if (entry.placementVersion == 0) HumanThingsPlacementMetadata.Initialize(entry);
                result.Add(entry);
            }
            return result;
        }
    }
}
