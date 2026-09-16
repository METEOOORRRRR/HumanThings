using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace EarthRecovery.Editor
{
    public static class GraphicsUploadAudit
    {
        public static void MoveLegacyPrefab()
        {
            const string legacy = "Assets/Resources/PlayerCharacter.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacy) != null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterBuilder.PrefabPath) != null)
                    throw new System.InvalidOperationException("Both legacy and relocated player prefabs exist.");
                string error = AssetDatabase.MoveAsset(legacy, PlayerCharacterBuilder.PrefabPath);
                if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
                AssetDatabase.SaveAssets();
            }
            ValidateRuntimeReferences();
            Debug.Log("CHARACTER_LEGACY_RESOURCE_MIGRATION_PASS");
        }

        public static void ValidateRuntimeReferences()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PlayerCharacter.prefab") != null)
                throw new System.InvalidOperationException("Original comparison prefab must not live in Resources.");
            foreach (var entry in PlayerCharacterCatalog.Load().characters)
            {
                var visual = entry.Prefab.GetComponentInChildren<HumanThingsCharacterVisual>();
                if (visual == null || visual.humanThingsMaterial == null || visual.skin == null
                    || visual.skin.sharedMaterial != visual.humanThingsMaterial)
                    throw new System.InvalidOperationException("Gameplay material is not assigned: " + entry.id);
                var serialized = new SerializedObject(visual);
                if (serialized.FindProperty("originalMaterial") != null)
                    throw new System.InvalidOperationException("Direct original comparison reference: " + entry.id);
                foreach (var property in visual.humanThingsMaterial.GetTexturePropertyNames())
                {
                    var texture = visual.humanThingsMaterial.GetTexture(property);
                    if (texture != null && AssetDatabase.GetAssetPath(texture).EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase))
                        throw new System.InvalidOperationException("Embedded original texture on gameplay material: " + entry.id + " " + property);
                }
            }
        }

        public static void Capture()
        {
            var report = new StringBuilder();
            var catalog = PlayerCharacterCatalog.Load();
            foreach (var entry in catalog.characters)
            {
                var visual = entry.Prefab.GetComponentInChildren<HumanThingsCharacterVisual>();
                report.AppendLine("CHARACTER " + entry.id + " prefab=" + AssetDatabase.GetAssetPath(entry.Prefab));
                foreach (var material in new[] { visual.originalMaterial, visual.humanThingsMaterial })
                {
                    report.AppendLine("MATERIAL " + material.name + " path=" + AssetDatabase.GetAssetPath(material));
                    foreach (var property in material.GetTexturePropertyNames())
                    {
                        if (material.GetTexture(property) is not Texture2D texture) continue;
                        report.AppendLine(property + "=" + texture.name + " " + texture.width + "x" + texture.height
                            + " " + texture.format + " mips=" + texture.mipmapCount + " bytes=" + Profiler.GetRuntimeMemorySizeLong(texture)
                            + " path=" + AssetDatabase.GetAssetPath(texture));
                    }
                }
            }
            var path = AssetDatabase.GetAssetPath(catalog);
            foreach (var dependency in AssetDatabase.GetDependencies(path).Where(p => p.EndsWith(".glb")))
            {
                report.AppendLine("GLB_DEPENDENCY " + dependency);
                foreach (var texture in AssetDatabase.LoadAllAssetsAtPath(dependency).OfType<Texture2D>())
                    report.AppendLine("EMBEDDED " + texture.name + " " + texture.width + "x" + texture.height + " " + texture.format);
            }
            Directory.CreateDirectory("QA/GraphicsTrace");
            File.WriteAllText("QA/GraphicsTrace/asset-references.txt", report.ToString());
            Debug.Log("GRAPHICS_ASSET_AUDIT_PASS");
        }
    }
}
