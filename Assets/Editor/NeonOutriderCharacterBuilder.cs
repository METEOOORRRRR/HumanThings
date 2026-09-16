using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EarthRecovery.Editor
{
    public static class NeonOutriderCharacterBuilder
    {
        public const string Source = "Assets/Character/Meshy_AI_Neon_Outrider_All_Animations.glb";
        public const string Output = "Assets/Character/NeonOutrider/Generated";
        public const string OriginalPrefab = "Assets/Character/NeonOutrider/PlayerCharacter.prefab";
        public const string VisualPrefab = HumanThingsCharacterVisualBuilder.Folder + "/Prefabs/HumanThings_NeonOutrider_Visual.prefab";
        public const string VisualProfile = HumanThingsCharacterVisualBuilder.Folder + "/Profiles/NeonOutrider.asset";
        public const string CleanAlbedo = "Assets/Character/NeonOutriderClean/NeonOutrider_BaseColor_4K.png";
        public const string VisualMaterial = HumanThingsCharacterVisualBuilder.Folder + "/Materials/HT_NeonOutrider.mat";

        [MenuItem("Tools/HumanThings/Character/Import Neon Outrider")]
        public static void Import()
        {
            PlayerCharacterBuilder.BuildCharacter(Source, Output, OriginalPrefab, "Neon Outrider");
            PlayerCharacterBuilder.PreviewCharacter(OriginalPrefab, Output, "QA/CharacterRoster/NeonOutriderOriginal");
        }

        [MenuItem("Tools/HumanThings/Character/Build Neon Outrider Visual and Roster")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab) == null) Import();
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(ToxicBunnyCharacterBuilder.CatalogPath);
            if (catalog == null) throw new InvalidOperationException("The existing character catalog is required.");
            Directory.CreateDirectory(Path.GetDirectoryName(VisualProfile));
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(VisualProfile) == null)
            {
                var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(HumanThingsCharacterVisualBuilder.ProfilePath));
                profile.visualPrefab = null;
                AssetDatabase.CreateAsset(profile, VisualProfile);
            }
            HumanThingsCharacterVisualBuilder.BuildVariant(OriginalPrefab, "NeonOutrider", VisualPrefab, VisualProfile, Classify);
            var entries = new List<PlayerCharacterCatalog.Entry>(catalog.characters);
            var entry = entries.Find(c => c.id == PlayerCharacterCatalog.NeonOutriderId);
            if (entry == null)
            {
                entry = new PlayerCharacterCatalog.Entry { id = PlayerCharacterCatalog.NeonOutriderId };
                entries.Add(entry);
            }
            entry.displayName = "Neon Outrider";
            entry.visualProfile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(VisualProfile);
            catalog.characters = entries.ToArray();
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (File.Exists(CleanAlbedo)) ApplyCleanTextures();
            PlayerCharacterBuilder.PreviewCharacter(VisualPrefab, Output, "QA/CharacterRoster/NeonOutriderVisual");
            Debug.Log("NEON_OUTRIDER_ROSTER_BUILD_PASS count=" + catalog.characters.Length);
        }

        [MenuItem("Tools/HumanThings/Character/Apply Clean Neon Outrider Textures")]
        public static void ApplyCleanTextures()
        {
            AssetDatabase.ImportAsset(CleanAlbedo, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(CleanAlbedo) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing clean Neon Outrider albedo: " + CleanAlbedo);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.overridden = true; platform.maxTextureSize = 4096;
            platform.format = TextureImporterFormat.BC7; platform.compressionQuality = 100;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanAlbedo);
            var material = AssetDatabase.LoadAssetAtPath<Material>(VisualMaterial);
            var profile = AssetDatabase.LoadAssetAtPath<HumanThingsCharacterVisualProfile>(VisualProfile);
            if (texture == null || material == null || profile == null)
                throw new InvalidOperationException("Build the Neon Outrider visual variant before applying clean textures.");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            profile.saturation = profile.brightness = profile.skinSaturation = profile.skinBrightness = 1;
            profile.tint = Color.white;
            profile.whiteCompression = profile.charcoalFloor = 0;
            profile.skinSmoothness = profile.hairSmoothness = profile.clothSmoothness = profile.equipmentSmoothness = .18f;
            profile.metallicMultiplier = profile.normalStrength = profile.detailStrength = 0;
            profile.dirtStrength = profile.dustStrength = profile.wearStrength = profile.cavityStrength = profile.wetness = 0;
            profile.Apply(material);
            EditorUtility.SetDirty(material); EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var visual = PlayerCharacterCatalog.Load().Resolve(PlayerCharacterCatalog.NeonOutriderId).Prefab.GetComponent<HumanThingsCharacterVisual>();
            if (visual.humanThingsMaterial != material || texture.width != 4096 || texture.height != 4096
                || AssetDatabase.GetAssetPath(visual.originalMaterial) != Source)
                throw new InvalidOperationException("Clean Neon Outrider must retain its original comparison material and use the separate 4K texture.");
            PlayerCharacterBuilder.PreviewCharacter(VisualPrefab, Output, "QA/NeonOutriderTexture/Unity");
            Debug.Log("NEON_OUTRIDER_CLEAN_TEXTURE_PASS texture=" + CleanAlbedo + " originalPreserved=true");
        }

        public static Color Classify(Vector3 position, Color albedo, float metallic)
        {
            float x = Mathf.Abs(position.x), y = position.y;
            bool skinColor = albedo.r > .3f && albedo.r > albedo.g * 1.07f && albedo.r < albedo.g * 1.9f
                && albedo.b > albedo.g * .65f && albedo.b < albedo.r;
            bool face = y > 1.48f && y < 1.75f && x < .16f && position.z > -.03f;
            bool chest = y > 1.32f && y < 1.52f && x < .12f && position.z > .02f;
            bool waist = y > 1.08f && y < 1.24f && x < .18f;
            bool hands = y > .8f && y < 1.14f && x > .25f;
            if (skinColor && (face || chest || waist || hands)) return new Color(1, 0, 0, 0);
            // Pale hair wraps around the face; keep the cyan collar in the clothing region.
            bool pale = Mathf.Min(albedo.r, albedo.g, albedo.b) > .35f
                && Mathf.Max(albedo.r, albedo.g, albedo.b) - Mathf.Min(albedo.r, albedo.g, albedo.b) < .15f;
            bool hair = y > 1.72f || (pale && y > 1.5f && x < .18f);
            if (hair) return new Color(0, 1, 0, 0);
            float cloth = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, .4f, y))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .8f, metallic)));
            return new Color(0, 0, cloth, 1 - cloth);
        }
    }
}
