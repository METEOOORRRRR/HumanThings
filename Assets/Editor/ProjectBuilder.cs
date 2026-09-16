using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EarthRecovery.Editor
{
    public static class ProjectBuilder
    {
        [MenuItem("Earth Recovery/Generate Startup Scene")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Resources");
            var rules = AssetDatabase.LoadAssetAtPath<GameRules>("Assets/Resources/GameRules.asset");
            if (rules == null) { rules = ScriptableObject.CreateInstance<GameRules>(); AssetDatabase.CreateAsset(rules, "Assets/Resources/GameRules.asset"); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Expedition.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Expedition.unity", true) };
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (!File.Exists("Assets/Resources/Greybox.mat")) AssetDatabase.CreateAsset(material, "Assets/Resources/Greybox.mat"); else Object.DestroyImmediate(material);
            if (!File.Exists("Assets/Resources/WorldText.mat")) AssetDatabase.CreateAsset(new Material(Shader.Find("EarthRecovery/WorldText")), "Assets/Resources/WorldText.mat");
            PlayerSettings.companyName = "EarthRecovery"; PlayerSettings.productName = "Human Things";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<HumanContent>("Assets/Resources/HumanContent.asset") == null) HumanContentBuilder.Build();
            Debug.Log("PROJECT_SETUP_PASS");
        }
        [MenuItem("Earth Recovery/Build Windows Prototype")]
        public static void Build()
        {
            var characters = PlayerCharacterCatalog.Load();
            if (characters == null) throw new System.InvalidOperationException("Build the player character catalog first.");
            characters.Validate();
            WaitingRoomSetup.Prepare();
            Setup();
            Directory.CreateDirectory("Builds/.staging");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Expedition.unity" }, locationPathName = "Builds/.staging/EarthRecovery.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Debug.Log("BUILD_RESULT " + report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows build failed");
            BuildRetention.Publish("Builds", "Builds/.staging");
        }
    }
}
