using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public sealed class HumanThingsAssetScannerWindow : EditorWindow
    {
        [SerializeField] DefaultAsset scanFolder;
        [SerializeField] HumanThingsAssetDatabase database;
        [SerializeField] string query = "";
        [SerializeField] int categoryFilter, page;
        Vector2 scroll;
        string status = "";
        bool refresh = true;
        List<HumanThingsAssetEntry> results = new();
        readonly HashSet<string> selectedEntries = new();
        readonly HashSet<string> expandedEntries = new();
        PlacementSurface batchSurface;
        const int PageSize = 50;
        static readonly string[] Filters = new[] { "All Categories" }.Concat(Enum.GetNames(typeof(AssetCategory))).ToArray();

        [MenuItem("Tools/HumanThings/Asset Scanner")]
        public static void Open()
        {
            var window = GetWindow<HumanThingsAssetScannerWindow>("Asset Scanner");
            if (window.database == null)
            {
                var catalogs = AssetDatabase.FindAssets("t:HumanThingsAssetDatabase");
                var selected = Selection.activeObject as HumanThingsAssetDatabase;
                if (selected == null && catalogs.Length == 1) selected = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>(AssetDatabase.GUIDToAssetPath(catalogs[0]));
                if (selected != null) window.SelectDatabase(selected);
            }
        }
        void OnEnable() { minSize = new Vector2(650, 500); refresh = true; Undo.undoRedoPerformed += Refresh; EditorApplication.projectChanged += Refresh; }
        void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.projectChanged -= Refresh; }
        void Refresh() { refresh = true; Repaint(); }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            var selected = (HumanThingsAssetDatabase)EditorGUILayout.ObjectField("Database", database, typeof(HumanThingsAssetDatabase), false);
            if (selected != database)
            {
                SelectDatabase(selected);
            }
            if (GUILayout.Button("Create Database", GUILayout.Width(135))) CreateDatabase();
            EditorGUILayout.EndHorizontal();
            scanFolder = (DefaultAsset)EditorGUILayout.ObjectField("Scan Folder", scanFolder, typeof(DefaultAsset), false);
            string folder = AssetDatabase.GetAssetPath(scanFolder);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(database == null || !HumanThingsAssetScanner.ValidFolder(folder)))
                if (GUILayout.Button("Scan", GUILayout.Height(26))) ScanFolder(folder);
            using (new EditorGUI.DisabledScope(database == null))
                if (GUILayout.Button("Save Database", GUILayout.Height(26)))
                { AssetDatabase.SaveAssetIfDirty(database); status = "Saved: " + AssetDatabase.GetAssetPath(database); }
            EditorGUILayout.EndHorizontal();
            if (database == null) { EditorGUILayout.HelpBox("Create or select a database asset.", MessageType.Info); return; }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize Missing Metadata"))
            {
                Undo.RecordObject(database, "Initialize placement metadata");
                foreach (var e in database.entries.Where(e => e != null && e.placementVersion == 0)) HumanThingsPlacementMetadata.Initialize(e);
                database.schemaVersion = 2; EditorUtility.SetDirty(database);
            }
            batchSurface = (PlacementSurface)EditorGUILayout.EnumPopup(batchSurface);
            if (GUILayout.Button("Apply Surface to Selected (" + selectedEntries.Count + ")"))
            {
                Undo.RecordObject(database, "Batch placement surface");
                foreach (var e in database.entries.Where(e => e != null && selectedEntries.Contains(e.assetGuid))) e.placementSurface = batchSurface;
                EditorUtility.SetDirty(database);
            }
            EditorGUILayout.EndHorizontal();
            if (scanFolder != null && !HumanThingsAssetScanner.ValidFolder(folder)) EditorGUILayout.HelpBox("Scan Folder must be a folder under Assets.", MessageType.Warning);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);

            var entries = database.entries ?? new List<HumanThingsAssetEntry>();
            EditorGUILayout.LabelField("Total: " + entries.Count(e => e != null) + (EditorUtility.IsDirty(database) ? "  (unsaved changes)" : ""), EditorStyles.boldLabel);
            foreach (var group in new[] { new[] { AssetCategory.Building, AssetCategory.Road, AssetCategory.Vehicle, AssetCategory.StreetProp }, new[] { AssetCategory.Transit, AssetCategory.Nature, AssetCategory.Structure, AssetCategory.Unknown } })
                EditorGUILayout.LabelField(string.Join("    ", group.Select(c => c + ": " + entries.Count(e => e != null && e.category == c))));

            EditorGUI.BeginChangeCheck();
            query = EditorGUILayout.TextField("Search", query);
            categoryFilter = EditorGUILayout.Popup("Category Filter", categoryFilter, Filters);
            if (EditorGUI.EndChangeCheck()) { refresh = true; page = 0; scroll = Vector2.zero; }
            if (refresh)
            {
                results = database.Search(query, categoryFilter == 0 ? (AssetCategory?)null : (AssetCategory)(categoryFilter - 1)).ToList();
                refresh = false;
            }
            int pages = Mathf.Max(1, Mathf.CeilToInt(results.Count / (float)PageSize)); page = Mathf.Clamp(page, 0, pages - 1);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Results: " + results.Count + "    Page " + (page + 1) + " / " + pages);
            using (new EditorGUI.DisabledScope(page == 0))
                if (GUILayout.Button(new GUIContent("<", "Previous page"), GUILayout.Width(30))) { page--; scroll = Vector2.zero; }
            using (new EditorGUI.DisabledScope(page + 1 >= pages))
                if (GUILayout.Button(new GUIContent(">", "Next page"), GUILayout.Width(30))) { page++; scroll = Vector2.zero; }
            EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in results.Skip(page * PageSize).Take(PageSize)) DrawEntry(entry);
            EditorGUILayout.EndScrollView();
        }

        void DrawEntry(HumanThingsAssetEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (EditorGUILayout.ToggleLeft("Select for batch edit", selectedEntries.Contains(entry.assetGuid))) selectedEntries.Add(entry.assetGuid);
            else selectedEntries.Remove(entry.assetGuid);
            EditorGUILayout.LabelField(entry.displayName, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(entry.assetPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Prefab", entry.prefab, typeof(GameObject), false);
            if (entry.prefab == null) EditorGUILayout.HelpBox("Missing prefab. Rescan the source folder to refresh the catalog.", MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            var category = (AssetCategory)EditorGUILayout.EnumPopup("Category", entry.category);
            var subCategory = EditorGUILayout.TextField("Subcategory", entry.subCategory);
            var tags = EditorGUILayout.DelayedTextField("Tags", string.Join(", ", entry.tags ?? new List<string>()));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(database, "Edit prefab classification");
                entry.category = category; entry.subCategory = subCategory; entry.tags = HumanThingsAssetClassifier.ParseTags(tags);
                EditorUtility.SetDirty(database); refresh = true;
            }
            bool expanded = EditorGUILayout.Foldout(expandedEntries.Contains(entry.assetGuid), "Placement Metadata", true);
            if (expanded)
            {
                expandedEntries.Add(entry.assetGuid);
                var serialized = new SerializedObject(database);
                var item = serialized.FindProperty("entries").GetArrayElementAtIndex(database.entries.IndexOf(entry));
                foreach (string field in new[] { "placementEnabled", "boundsSize", "boundsCenter", "placementSurface", "frontFacesRoad", "cornerCompatible", "againstWall", "canRotate90", "allowRandomRotation", "localForward", "groundOffset", "footprintWidth", "footprintDepth", "companionPrefabs" })
                    EditorGUILayout.PropertyField(item.FindPropertyRelative(field), true);
                if (serialized.ApplyModifiedProperties()) refresh = true;
                if (GUILayout.Button("Recalculate Bounds Only"))
                {
                    Undo.RecordObject(database, "Recalculate prefab bounds");
                    var b = HumanThingsPlacementMetadata.MeasureEntry(entry);
                    entry.boundsSize = b.size; entry.boundsCenter = b.center;
                    EditorUtility.SetDirty(database);
                }
            }
            else expandedEntries.Remove(entry.assetGuid);
            EditorGUILayout.EndVertical();
        }

        void SelectDatabase(HumanThingsAssetDatabase selected)
        {
            database = selected; page = 0; refresh = true; scroll = Vector2.zero; status = "";
            selectedEntries.Clear(); expandedEntries.Clear();
            if (database == null) return;
            var path = AssetDatabase.GUIDToAssetPath(database.sourceFolderGuid);
            if (string.IsNullOrEmpty(path)) path = database.sourceFolderPath;
            scanFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        }

        void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Asset Database", "CityAssetDatabase", "asset", "Choose the database asset location.", "Assets/Editor");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            { EditorUtility.DisplayDialog("Asset already exists", "Choose a new path or select the existing database.", "OK"); return; }
            database = CreateInstance<HumanThingsAssetDatabase>();
            AssetDatabase.CreateAsset(database, path); AssetDatabase.SaveAssetIfDirty(database);
            page = 0; refresh = true; status = "Created: " + path;
        }

        void ScanFolder(string folder)
        {
            var sourceGuid = AssetDatabase.AssetPathToGUID(folder);
            if (!string.IsNullOrEmpty(database.sourceFolderGuid) && database.sourceFolderGuid != sourceGuid && database.entries != null && database.entries.Count > 0
                && !EditorUtility.DisplayDialog("Change scan folder?", "The catalog will contain only prefabs in the newly selected folder. You can undo the scan.", "Scan", "Cancel")) return;
            try
            {
                var scanned = HumanThingsAssetScanner.Scan(folder, database.entries,
                    (i, total, path) => EditorUtility.DisplayCancelableProgressBar("Prefab scan", path, total == 0 ? 1 : (float)i / total));
                Undo.RecordObject(database, "Scan prefab folder");
                database.entries = scanned; database.schemaVersion = 2; database.sourceFolderPath = folder; database.sourceFolderGuid = sourceGuid;
                EditorUtility.SetDirty(database); page = 0; scroll = Vector2.zero; refresh = true;
                status = "Scanned " + scanned.Count + " prefabs. Existing classifications preserved. Save Database to persist.";
            }
            catch (OperationCanceledException) { status = "Scan canceled. Database unchanged."; }
            catch (Exception ex) { status = "Scan failed: " + ex.Message; Debug.LogException(ex); }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
