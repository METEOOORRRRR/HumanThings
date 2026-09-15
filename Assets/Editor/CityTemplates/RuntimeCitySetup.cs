using System;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Editor
{
    public static class RuntimeCitySetup
    {
        public static void Build()
        {
            Configure();
            System.IO.Directory.CreateDirectory("Builds/.staging");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Expedition.unity" }, locationPathName = "Builds/.staging/EarthRecovery.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Runtime city build failed.");
            BuildRetention.Publish("Builds", "Builds/.staging");
            Debug.Log("RUNTIME_CITY_BUILD_PASS");
        }
        [MenuItem("Tools/HumanThings/Configure In-Game City")]
        public static void Configure()
        {
            const string path = "Assets/Resources/RuntimeCitySettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<RuntimeCitySettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RuntimeCitySettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.template = AssetDatabase.LoadAssetAtPath<CityWorldTemplate>(CityWorldGeneratorWindow.StandardPath);
            settings.database = AssetDatabase.LoadAssetAtPath<HumanThingsAssetDatabase>("Assets/CityGeneration/CityAssetDatabase.asset");
            if (settings.template == null || settings.database == null) throw new InvalidOperationException("Missing validated city assets.");
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Debug.Log("RUNTIME_CITY_CONFIGURED");
        }
    }
}
