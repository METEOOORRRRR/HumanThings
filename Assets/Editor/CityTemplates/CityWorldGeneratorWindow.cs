using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public sealed class CityWorldGeneratorWindow : EditorWindow
    {
        [SerializeField] HumanThingsAssetDatabase database;
        [SerializeField] CityWorldTemplate template;
        [SerializeField] int seed = 1234;
        [SerializeField] bool drawBounds = true, drawConnectors = true, drawGraph = true, drawDistricts = true;
        [SerializeField] bool drawPOIBounds = true, drawPOIDistance, drawCampRadius = true;
        Vector2 scroll;
        GeneratedWorldResult result;
        CityWorldValidationReport validation;
        string error;
        public const string StandardPath = "Assets/CityGeneration/WorldTemplates/StandardCity.asset";

        [MenuItem("Tools/HumanThings/City World Generator")]
        public static void Open() => GetWindow<CityWorldGeneratorWindow>("City World Generator");
        void OnEnable()
        {
            if (database == null) database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            if (template == null) template = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(StandardPath);
            SceneView.duringSceneGui += DrawScene;
        }
        void OnDisable() => SceneView.duringSceneGui -= DrawScene;
        public static CityWorldTemplate CreateStandard()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(StandardPath);
            if (existing != null) { SpecialPOIAssetBuilder.Configure(existing); return existing; }
            var t = CreateInstance<CityWorldTemplate>();
            try
            {
                void Add(string name, CityDistrictKind district, float weight)
                {
                    var chunk = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(CityTemplateAutoBuilder.DefaultFolder + "/" + name + ".asset");
                    if (chunk != null) t.chunkTemplates.Add(new CityChunkTemplateChoice { district = district, template = chunk, weight = weight });
                }
                Add("CatalogMixed", CityDistrictKind.Mixed, 2);
                Add("SparseBlocks", CityDistrictKind.Mixed, 1);
                Add("MarketBranches", CityDistrictKind.Commercial, 2);
                Add("OfficeBlocks", CityDistrictKind.Office, 1.5f);
                Add("TransitCorridor", CityDistrictKind.Transit, 1);
                Add("WideAvenue", CityDistrictKind.Downtown, 1);
                if (t.chunkTemplates.Select(c => c.district).Distinct().Count() < 2)
                    throw new InvalidOperationException("Build measured CityLayoutTemplates before creating StandardCity.");
                CityWorldLayout.Generate(t, 1234);
                if (!AssetDatabase.IsValidFolder("Assets/CityGeneration/WorldTemplates")) AssetDatabase.CreateFolder("Assets/CityGeneration", "WorldTemplates");
                AssetDatabase.CreateAsset(t, StandardPath); SpecialPOIAssetBuilder.Configure(t); AssetDatabase.SaveAssets(); return t;
            }
            catch { DestroyImmediate(t); throw; }
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            database = (HumanThingsAssetDatabase)EditorGUILayout.ObjectField("Asset Database", database, typeof(HumanThingsAssetDatabase), false);
            template = (CityWorldTemplate)EditorGUILayout.ObjectField("World Template", template, typeof(CityWorldTemplate), false);
            seed = EditorGUILayout.IntField("World Seed", seed);
            if (template != null)
            {
                EditorGUI.BeginChangeCheck();
                var db = (SpecialPOIDatabase)EditorGUILayout.ObjectField("Special POI Database", template.specialPOIDatabase, typeof(SpecialPOIDatabase), false);
                var camp = (BaseCampDefinition)EditorGUILayout.ObjectField("Base Camp", template.baseCamp, typeof(BaseCampDefinition), false);
                bool labels = EditorGUILayout.Toggle("Show POI Labels", template.specialPOISettings.showDebugLocationLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(template, "Edit world POI settings"); template.specialPOIDatabase = db; template.baseCamp = camp;
                    template.specialPOISettings.showDebugLocationLabels = labels; EditorUtility.SetDirty(template);
                    foreach (var marker in CityWorldGenerator.FindWorlds(EditorSceneManager.GetActiveScene())) POIDebugLabel.SetVisible(marker.gameObject, labels);
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }
            if (GUILayout.Button("Create / Select StandardCity")) Run(() => { template = CreateStandard(); Selection.activeObject = template; });
            using (new EditorGUI.DisabledScope(database == null || template == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Generate World")) Run(Generate);
                if (GUILayout.Button("Regenerate With New Seed")) Run(() => { seed = CityWorldLayout.DeriveChunkSeed(seed, 17, 31); Generate(); });
                if (GUILayout.Button("Validate 10 World Seeds")) Run(() =>
                {
                    validation = CityWorldValidation.Run(template, database, 10, (p, text) => EditorUtility.DisplayCancelableProgressBar("Validate worlds", text, p));
                    Debug.Log(JsonUtility.ToJson(validation, true));
                });
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Clear World")) Run(() =>
                {
                    Editable(); Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Clear city world");
                    foreach (var old in CityWorldGenerator.FindWorlds(EditorSceneManager.GetActiveScene())) Undo.DestroyObjectImmediate(old.gameObject);
                    Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); result = null;
                });
            drawBounds = EditorGUILayout.Toggle("Draw Chunk Bounds", drawBounds);
            drawConnectors = EditorGUILayout.Toggle("Draw Connectors", drawConnectors);
            drawGraph = EditorGUILayout.Toggle("Draw Global Road Graph", drawGraph);
            drawDistricts = EditorGUILayout.Toggle("Draw District Types", drawDistricts);
            drawPOIBounds = EditorGUILayout.Toggle("Draw POI Bounds", drawPOIBounds);
            drawPOIDistance = EditorGUILayout.Toggle("Draw POI Min Distance", drawPOIDistance);
            drawCampRadius = EditorGUILayout.Toggle("Draw Base Camp Clear Radius", drawCampRadius);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            result = CityWorldGenerator.FindWorlds(EditorSceneManager.GetActiveScene()).LastOrDefault()?.result;
            if (result?.root != null)
            {
                EditorGUILayout.LabelField($"Seed {result.worldSeed} | Chunks {result.chunks.Count} | Connector pairs {result.globalRoads.Count}");
                EditorGUILayout.LabelField($"Buildings {result.chunks.Sum(c => c.city.buildings.Count)} | Vehicles {result.chunks.Sum(c => c.city.vehicles.Count)} | Locations {result.locations.Count}");
                foreach (var c in result.chunks)
                    EditorGUILayout.LabelField(c.node.Id, $"{c.node.chunkTemplate.templateName} | Seed {c.node.chunkSeed} | Ports {c.node.connectors.Count}");
                EditorGUILayout.Space(); EditorGUILayout.LabelField($"Special POIs: {result.specialPOIs.Count} / 15 placed", EditorStyles.boldLabel);
                foreach (var p in result.specialPOIs)
                    EditorGUILayout.LabelField(p.displayName + " (" + p.id + ")", p.chunk.Id + " / " + p.bounds.center.ToString("F1"));
            }
            if (validation != null)
            {
                EditorGUILayout.HelpBox(validation.passed ? "All tested worlds passed structural validation." : "World validation failed; see results and Console.", validation.passed ? MessageType.Info : MessageType.Error);
                foreach (var s in validation.seeds)
                {
                    EditorGUILayout.LabelField($"Seed {s.seed}: {(s.passed ? "PASS" : "FAIL")}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Chunks {s.generatedChunks}/{s.requestedChunks}; ports {s.validConnectorPairs}/{s.connectorPairs}; reached {s.reachedChunks}");
                    EditorGUILayout.LabelField($"Buildings {s.placedBuildings}/{s.requestedBuildings}; empty {s.emptyChunks}; overlaps {s.overlapPairs}; road failures {s.roadFailures}");
                    EditorGUILayout.LabelField($"Special POIs {s.specialPOIs}/15; placement attempts {s.poiAttempts}");
                    foreach (string e in s.errors) EditorGUILayout.HelpBox(e, MessageType.Error);
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUI.changed) SceneView.RepaintAll();
        }

        static void Editable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Use an editable scene outside Play Mode / Prefab Mode.");
        }
        void Generate()
        {
            Editable();
            var plan = CityWorldGenerator.Generate(template, seed, database, (p, text) => EditorUtility.DisplayCancelableProgressBar("Generate world", text, p));
            var report = CityWorldValidation.Validate(plan); validation = new CityWorldValidationReport { passed = report.passed, seeds = new() { report } };
            if (!report.passed) throw new InvalidOperationException(string.Join("\n", report.errors));
            var scene = EditorSceneManager.GetActiveScene(); var old = CityWorldGenerator.FindWorlds(scene);
            // Build succeeds before any existing world is removed. One Undo restores the prior world.
            var built = CityWorldGenerator.Build(plan, scene, (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Generate city world");
            Undo.RegisterCreatedObjectUndo(built.root, "Generate city world");
            foreach (var marker in old) Undo.DestroyObjectImmediate(marker.gameObject);
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            result = built; Selection.activeGameObject = result.root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(plan.bounds.center, plan.bounds.size + Vector3.up * 40), false);
        }
        void Run(Action action)
        {
            error = null;
            try { action(); }
            catch (OperationCanceledException) { error = "Canceled. Existing world unchanged."; }
            catch (Exception e) { error = e.Message; Debug.LogException(e); }
            finally { EditorUtility.ClearProgressBar(); }
        }
        void DrawScene(SceneView view)
        {
            var world = CityWorldGenerator.FindWorlds(EditorSceneManager.GetActiveScene()).LastOrDefault()?.result;
            if (world?.root == null) return;
            Color previous = Handles.color;
            Handles.color = Color.green;
            if (drawCampRadius) Handles.DrawWireDisc(world.worldBounds.center, Vector3.up, world.baseCampClearRadius);
            foreach (var poi in world.specialPOIs)
            {
                Handles.color = Color.magenta;
                if (drawPOIBounds) Handles.DrawWireCube(poi.bounds.center, poi.bounds.size);
                if (drawPOIDistance) Handles.DrawWireDisc(poi.bounds.center, Vector3.up, Mathf.Max(world.minPOIDistance, poi.definition.minDistanceFromOtherPOI));
            }
            foreach (var c in world.chunks)
            {
                Handles.color = Color.HSVToRGB((int)c.node.district / 7f, .65f, 1);
                if (drawBounds) Handles.DrawWireCube(c.node.worldBounds.center, c.node.worldBounds.size);
                if (drawDistricts) Handles.Label(c.node.worldBounds.center + Vector3.up * 15, c.node.Id + "\n" + c.node.district + " / " + c.node.chunkTemplate.templateName);
                if (drawConnectors) foreach (var port in c.node.connectors)
                {
                    var point = port.WorldPoint(c.node.worldBounds);
                    Handles.color = port.type == ConnectorType.MainRoad ? Color.yellow : Color.cyan;
                    Handles.DrawWireDisc(point + Vector3.up, Vector3.up, port.roadWidth / 2);
                }
            }
            if (drawGraph) foreach (var edge in world.globalRoads)
            {
                var a = world.chunks.FirstOrDefault(c => c.node.gridPosition == edge.a);
                var b = world.chunks.FirstOrDefault(c => c.node.gridPosition == edge.b);
                if (a == null || b == null) continue;
                Handles.color = edge.globalMainRoute ? Color.yellow : Color.cyan;
                Handles.DrawAAPolyLine(3, a.node.worldBounds.center + Vector3.up * 4, edge.worldPoint + Vector3.up * 4, b.node.worldBounds.center + Vector3.up * 4);
            }
            Handles.color = previous;
        }
    }
}
