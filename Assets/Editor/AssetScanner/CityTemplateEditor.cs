using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EarthRecovery.Editor
{
    public static class CityTemplateEditor
    {
        // Retained for historical fixture scenes/tests; production creation uses the measured builder.
        public static void CreatePresets()
        {
            const string folder = "Assets/CityGeneration/Templates";
            if (!AssetDatabase.IsValidFolder("Assets/CityGeneration")) AssetDatabase.CreateFolder("Assets", "CityGeneration");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/CityGeneration", "Templates");
            foreach (CityTemplatePreset preset in Enum.GetValues(typeof(CityTemplatePreset)))
            {
                string path = folder + "/" + preset + ".asset";
                if (AssetDatabase.LoadAssetAtPath<CityLayoutTemplate>(path) != null) continue;
                var template = ScriptableObject.CreateInstance<CityLayoutTemplate>(); template.ApplyPreset(preset);
                AssetDatabase.CreateAsset(template, path);
            }
            AssetDatabase.SaveAssets();
        }
        static void Editable(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Use an editable scene outside Play Mode and Prefab Mode.");
        }
        public static GeneratedCityResult Build(ProceduralCityPlan plan, Scene scene)
        {
            Editable(scene);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Generate procedural city");
            GameObject root = null;
            try
            {
                var old = CitySceneBuilder.FindGenerated(scene);
                var result = CitySceneBuilder.Build(plan, scene, (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)); root = result.root;
                Undo.RegisterCreatedObjectUndo(root, "Generate procedural city");
                foreach (var marker in old) Undo.DestroyObjectImmediate(marker.gameObject);
                Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
                return result;
            }
            catch { Undo.RevertAllDownToGroup(group); if (root != null) UnityEngine.Object.DestroyImmediate(root); throw; }
        }
        public static void Clear(Scene scene)
        {
            Editable(scene); Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Clear procedural city");
            foreach (var marker in CitySceneBuilder.FindGenerated(scene)) Undo.DestroyObjectImmediate(marker.gameObject);
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
