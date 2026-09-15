using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public sealed class CityTemplateAutoBuilderWindow : EditorWindow
    {
        [SerializeField] HumanThingsAssetDatabase database;
        [SerializeField] DefaultAsset outputFolder;
        CityAssetAnalysis analysis;
        CityTemplateBuildReport report;
        Vector2 scroll;
        string status;
        [MenuItem("Tools/HumanThings/Build City Templates From Asset Database")]
        public static void Open() => GetWindow<CityTemplateAutoBuilderWindow>("Measured Templates");
        void OnEnable() { if (database == null) database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset"); }
        void OnGUI()
        {
            var selected = (HumanThingsAssetDatabase)EditorGUILayout.ObjectField("Database", database, typeof(HumanThingsAssetDatabase), false);
            if (selected != database) { database = selected; analysis = null; report = null; }
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
            string folder = outputFolder == null ? CityTemplateAutoBuilder.DefaultFolder : AssetDatabase.GetAssetPath(outputFolder);
            EditorGUILayout.LabelField("Path", folder);
            using (new EditorGUI.DisabledScope(database == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Analyze Database")) Run(() => { analysis = CityAssetAnalysis.Analyze(database); CityTemplateAutoBuilder.Export(new CityTemplateBuildReport { analysis = analysis }, "Analysis"); });
                if (GUILayout.Button("Build Templates")) Run(() => Build(folder, false));
                if (GUILayout.Button("Validate Templates")) Run(() => { report = CityTemplateAutoBuilder.Validate(database, folder); analysis = report.analysis; });
                if (GUILayout.Button("Rebuild All") && EditorUtility.DisplayDialog("Rebuild measured templates", "Revalidate and replace unchanged generated templates. Manual or edited templates are preserved. Obsolete unchanged generated templates may be removed.", "Rebuild", "Cancel")) Run(() => Build(folder, true));
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (analysis != null) foreach (var c in analysis.categories)
            {
                EditorGUILayout.LabelField(c.category.ToString(), c.total + " registered / " + c.usable + " usable", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Join(", ", c.subCategories.Select(s => s.name + " " + s.total + "/" + s.usable)), EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Usable mean footprint", c.usableSizes.meanFootprint.ToString("F2"));
                EditorGUILayout.LabelField("Footprint min / max", c.usableSizes.minFootprint.ToString("F2") + " / " + c.usableSizes.maxFootprint.ToString("F2"));
            }
            if (report != null) foreach (var v in report.validations) EditorGUILayout.HelpBox(v.templateName + ": " + (v.passed ? "PASS" : "EXCLUDED") + "\nBuildings " + v.buildings + "\nVehicles " + v.vehicles + "\nProps " + v.props + "\nPOIs " + v.requiredPOIs, v.passed ? MessageType.Info : MessageType.Warning);
            if (analysis != null) foreach (var w in analysis.warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void Build(string folder, bool rebuild)
        {
            report = CityTemplateAutoBuilder.Build(database, folder, rebuild, (p, n) => EditorUtility.DisplayCancelableProgressBar("Validating templates", n, p));
            analysis = report.analysis;
        }
        void Run(Action action)
        {
            try { action(); status = "Finished. Reports: Docs/MeasuredCityTemplates"; }
            catch (Exception e) { status = e.Message; }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }
    }
}
