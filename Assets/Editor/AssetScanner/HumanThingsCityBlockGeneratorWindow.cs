using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EarthRecovery.Editor
{
    public sealed class HumanThingsCityBlockGeneratorWindow : EditorWindow
    {
        [SerializeField] HumanThingsAssetDatabase database;
        [SerializeField] CityLayoutTemplate template;
        [SerializeField] int seed = 1234;
        Vector2 scroll;
        string status;
        RuntimeCityGenerationConfig sampled;

        [MenuItem("Tools/HumanThings/City Block Generator")]
        public static void Open() => GetWindow<HumanThingsCityBlockGeneratorWindow>("City Generator");
        void OnEnable()
        {
            if (database == null) database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            if (template == null) template = AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>("Assets/CityGeneration/AutoTemplates/CatalogMixed.asset");
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            database = (HumanThingsAssetDatabase)EditorGUILayout.ObjectField("Database", database, typeof(HumanThingsAssetDatabase), false);
            template = (CityLayoutTemplate)EditorGUILayout.ObjectField("Template", template, typeof(CityLayoutTemplate), false);
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Measured Template Builder")) CityTemplateAutoBuilderWindow.Open();
            using (new EditorGUI.DisabledScope(database == null || template == null || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null))
            {
                if (GUILayout.Button("Generate")) Run(() => GenerateCity(false));
                if (GUILayout.Button("Regenerate With New Seed")) Run(() => GenerateCity(true));
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null))
                if (GUILayout.Button("Clear Generated")) Run(() => { CityTemplateEditor.Clear(SceneManager.GetActiveScene()); status = "Generated city cleared."; sampled = null; });
            if (sampled != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Map", sampled.mapWidth.ToString("F1") + " x " + sampled.mapDepth.ToString("F1") + " m");
                EditorGUILayout.LabelField("Sampled roads", $"Main {sampled.mainRoadCount}, Side {sampled.sideRoadCount}, Alley {sampled.alleyCount}");
                EditorGUILayout.LabelField("Building density", sampled.buildingDensity.ToString("P0"));
                EditorGUILayout.LabelField("Sampled POIs", $"Shop {sampled.shopCount}, Subway {sampled.subwayEntranceCount}, Bus {sampled.busStopCount}, Public {sampled.publicBuildingCount}");
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void GenerateCity(bool newSeed)
        {
            if (newSeed)
            {
                int previous = seed; seed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
                if (seed == previous) seed = unchecked(seed + 1);
            }
            var plan = ProceduralCityGenerator.Generate(template, seed, database);
            var result = CityTemplateEditor.Build(plan, SceneManager.GetActiveScene());
            sampled = plan.config;
            status = $"Seed {seed}: {result.roads.Count} roads, {result.lots.Count} lots, {result.buildings.Count} buildings, {result.vehicles.Count} vehicles, {result.props.Count} props.\n"
                + string.Join("\n", result.warnings);
            Selection.activeGameObject = result.root;
        }
        void Run(Action action)
        {
            try { action(); Repaint(); }
            catch (Exception e) { status = e.Message; Repaint(); }
        }
    }
}
